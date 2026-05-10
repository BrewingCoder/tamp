using System.Diagnostics;

namespace Tamp;

/// <summary>
/// Runs a target graph in topological order. v0 is sequential; resource
/// scheduling, parallelism, and per-target retry are recorded on the spec
/// but not yet honored here. (Filed in TAM-25 follow-ups.)
/// </summary>
/// <remarks>
/// The executor honors <see cref="ITargetDefinition.OnlyWhen"/> conditions,
/// <see cref="ITargetDefinition.Requires"/> hard preconditions,
/// <see cref="ITargetDefinition.AssuredAfterFailure"/> cleanup semantics,
/// and <see cref="ITargetDefinition.OnFailureOf"/> failure handlers.
///
/// On a target failure, the executor:
/// <list type="number">
///   <item>Records the failure (target name + exit code or exception).</item>
///   <item>Looks up handlers via <see cref="TargetGraph.HandlersFor"/> and
///         runs each handler's full sub-tree (handler + its own deps).</item>
///   <item>Continues iterating the main plan, but only runs targets that
///         declared <see cref="ITargetDefinition.AssuredAfterFailure"/>;
///         everything else is marked <see cref="TargetStatus.NotRun"/>.</item>
///   <item>Returns the original failure's exit code regardless of whether
///         handlers or assured-after-failure cleanups succeeded.</item>
/// </list>
/// </remarks>
public sealed class Executor
{
    private readonly RedactionTable _redactionTable;
    private readonly RedactingTextWriter _redactedOutput;

    public Executor(TargetGraph graph, ExecutionMode mode = ExecutionMode.Run, TextWriter? output = null)
    {
        Graph = graph ?? throw new ArgumentNullException(nameof(graph));
        Mode = mode;
        Output = output ?? Console.Out;
        _redactionTable = new RedactionTable();
        _redactedOutput = new RedactingTextWriter(Output, _redactionTable);
    }

    public TargetGraph Graph { get; }
    public ExecutionMode Mode { get; }
    public TextWriter Output { get; }

    /// <summary>
    /// The redaction table populated as targets run. Exposed for tests and
    /// for callers who want to register additional secrets ahead of time.
    /// </summary>
    public RedactionTable RedactionTable => _redactionTable;

    /// <summary>Run / dry-run / plan one or more invoked targets.</summary>
    public ExecutionResult Run(params string[] rootTargetNames)
    {
        var order = Graph.ComputeExecutionOrder(rootTargetNames);

        return Mode switch
        {
            ExecutionMode.Plan => RunPlan(order, rootTargetNames),
            ExecutionMode.DryRun => RunDryRun(order),
            ExecutionMode.Run => RunActual(order),
            _ => throw new InvalidOperationException($"Unknown execution mode: {Mode}"),
        };
    }

    private ExecutionResult RunPlan(IReadOnlyList<TargetSpec> order, IReadOnlyList<string> roots)
    {
        var rootLabel = string.Join(", ", roots);
        _redactedOutput.WriteLine($"Plan for '{rootLabel}' ({order.Count} target{(order.Count == 1 ? "" : "s")}):");
        _redactedOutput.WriteLine();
        foreach (var spec in order)
        {
            var phase = spec.Phase == Phase.None ? string.Empty : $" [{spec.Phase}]";
            _redactedOutput.WriteLine($"  {spec.Name}{phase}");
            if (spec.Dependencies.Count > 0)
                _redactedOutput.WriteLine($"    depends on: {string.Join(", ", spec.Dependencies)}");
            if (spec.OrderAfter.Count > 0)
                _redactedOutput.WriteLine($"    after: {string.Join(", ", spec.OrderAfter)}");
            if (spec.OrderBefore.Count > 0)
                _redactedOutput.WriteLine($"    before: {string.Join(", ", spec.OrderBefore)}");
            if (spec.Triggers.Count > 0)
                _redactedOutput.WriteLine($"    triggers: {string.Join(", ", spec.Triggers)}");
            if (spec.TriggeredBy.Count > 0)
                _redactedOutput.WriteLine($"    triggered by: {string.Join(", ", spec.TriggeredBy)}");
            if (spec.OnlyWhenConditions.Count > 0)
                foreach (var c in spec.OnlyWhenConditions)
                    _redactedOutput.WriteLine($"    only when: {c.ExpressionText}");
            if (spec.Requirements.Count > 0)
                foreach (var c in spec.Requirements)
                    _redactedOutput.WriteLine($"    requires: {c.ExpressionText}");
            if (spec.AssuredAfterFailure)
                _redactedOutput.WriteLine($"    assured-after-failure: yes");
            if (spec.RequiresNetwork || spec.RequiresDocker || spec.RequiresAdmin)
            {
                var caps = new List<string>();
                if (spec.RequiresNetwork) caps.Add("network");
                if (spec.RequiresDocker) caps.Add("docker");
                if (spec.RequiresAdmin) caps.Add("admin");
                _redactedOutput.WriteLine($"    requires: {string.Join(", ", caps)}");
            }
        }
        _redactedOutput.Flush();
        return new ExecutionResult { Mode = Mode, ExitCode = 0, TargetsTraversed = order.Count };
    }

    private ExecutionResult RunDryRun(IReadOnlyList<TargetSpec> order)
    {
        _redactedOutput.WriteLine("[DRY RUN] No commands will execute.");
        _redactedOutput.WriteLine();
        var planCount = 0;
        foreach (var spec in order)
        {
            if (CheckSkippedByCondition(spec) is { } skipReason)
            {
                _redactedOutput.WriteLine($"[skipped] {spec.Name} — only-when condition false: {skipReason}");
                _redactedOutput.WriteLine();
                continue;
            }
            foreach (var factory in spec.PlanFactories)
            {
                foreach (var plan in factory())
                {
                    _redactionTable.RegisterAll(plan);
                    ProcessRunner.Print(plan, spec.Name, sourceModule: null, _redactedOutput);
                    planCount++;
                }
            }
        }
        _redactedOutput.Flush();
        return new ExecutionResult { Mode = Mode, ExitCode = 0, TargetsTraversed = order.Count, CommandPlansPrinted = planCount };
    }

    private ExecutionResult RunActual(IReadOnlyList<TargetSpec> order)
    {
        var records = new List<TargetExecutionRecord>(order.Count);
        var skipped = new List<string>();
        var handlersInvoked = new List<string>();
        (string Name, int ExitCode)? buildFailedAt = null;
        var buildSw = Stopwatch.StartNew();

        foreach (var spec in order)
        {
            // After a failure, only AssuredAfterFailure targets keep running.
            if (buildFailedAt.HasValue && !spec.AssuredAfterFailure)
            {
                _redactedOutput.WriteLine($"==> {spec.Name} (not run: build already failed)");
                records.Add(TargetExecutionRecord.NotRun(spec.Name));
                continue;
            }

            if (CheckSkippedByCondition(spec) is { } skipReason)
            {
                _redactedOutput.WriteLine($"==> {spec.Name} (skipped: {skipReason})");
                skipped.Add(spec.Name);
                records.Add(TargetExecutionRecord.Skipped(spec.Name, skipReason));
                continue;
            }

            // Hard preconditions (Requires) — failure here aborts the build.
            if (CheckRequirementsFailed(spec) is { } reqFail)
            {
                _redactedOutput.WriteLine($"==> {spec.Name} REQUIRES failed: {reqFail}");
                records.Add(TargetExecutionRecord.Failed(spec.Name, TimeSpan.Zero, $"Requires failed: {reqFail}"));
                if (!buildFailedAt.HasValue)
                {
                    buildFailedAt = (spec.Name, 1);
                    handlersInvoked.AddRange(DispatchFailureHandlers(spec.Name));
                }
                continue;
            }

            _redactedOutput.WriteLine($"==> {spec.Name}");
            var sw = Stopwatch.StartNew();

            try
            {
                foreach (var action in spec.Actions)
                    action();

                var exit = 0;
                foreach (var factory in spec.PlanFactories)
                {
                    foreach (var plan in factory())
                    {
                        _redactionTable.RegisterAll(plan);
                        exit = ProcessRunner.Execute(plan, _redactedOutput, _redactedOutput);
                        if (exit != 0)
                        {
                            _redactedOutput.WriteLine($"==> {spec.Name} FAILED (exit {exit})");
                            if (spec.FailureMode == FailureMode.Continue) continue;
                            sw.Stop();
                            records.Add(TargetExecutionRecord.Failed(spec.Name, sw.Elapsed, $"exit {exit}"));
                            if (!buildFailedAt.HasValue)
                            {
                                buildFailedAt = (spec.Name, exit);
                                handlersInvoked.AddRange(DispatchFailureHandlers(spec.Name));
                            }
                            goto nextSpec;
                        }
                    }
                }

                sw.Stop();
                records.Add(TargetExecutionRecord.Done(spec.Name, sw.Elapsed));
            }
            catch (Exception ex) when (spec.FailureMode == FailureMode.Continue)
            {
                sw.Stop();
                _redactedOutput.WriteLine($"==> {spec.Name} threw {ex.GetType().Name}; continuing per FailureMode.Continue: {ex.Message}");
                records.Add(TargetExecutionRecord.Done(spec.Name, sw.Elapsed));
            }
            catch (Exception ex)
            {
                sw.Stop();
                _redactedOutput.WriteLine($"==> {spec.Name} threw {ex.GetType().Name}: {ex.Message}");
                records.Add(TargetExecutionRecord.Failed(spec.Name, sw.Elapsed, ex.Message));
                if (!buildFailedAt.HasValue)
                {
                    buildFailedAt = (spec.Name, 1);
                    handlersInvoked.AddRange(DispatchFailureHandlers(spec.Name));
                }
            }

            nextSpec:;
        }

        buildSw.Stop();
        WriteBuildSummary(records, buildSw.Elapsed, buildFailedAt?.Name);
        _redactedOutput.Flush();

        var traversed = records.Count(r => r.Status is not TargetStatus.NotRun);
        return new ExecutionResult
        {
            Mode = Mode,
            ExitCode = buildFailedAt?.ExitCode ?? 0,
            TargetsTraversed = traversed,
            FailedTarget = buildFailedAt?.Name,
            FailureHandlersInvoked = handlersInvoked,
            SkippedTargets = skipped,
            ExecutionRecords = records,
            Duration = buildSw.Elapsed,
        };
    }

    /// <summary>Returns the failure reason (for human display) when an OnlyWhen condition rejects a target; null otherwise.</summary>
    private static string? CheckSkippedByCondition(TargetSpec spec)
    {
        foreach (var c in spec.OnlyWhenConditions)
        {
            bool result;
            try { result = c.Predicate(); }
            catch (Exception ex) { return $"{c.ExpressionText} threw {ex.GetType().Name}: {ex.Message}"; }
            if (!result) return c.ExpressionText;
        }
        return null;
    }

    /// <summary>Returns the failed expression text when a Requires precondition fails; null when all hold.</summary>
    private static string? CheckRequirementsFailed(TargetSpec spec)
    {
        foreach (var c in spec.Requirements)
        {
            bool result;
            try { result = c.Predicate(); }
            catch (Exception ex) { return $"{c.ExpressionText} threw {ex.GetType().Name}: {ex.Message}"; }
            if (!result) return c.ExpressionText;
        }
        return null;
    }

    private IEnumerable<string> DispatchFailureHandlers(string failedTargetName)
    {
        var handlers = Graph.HandlersFor(failedTargetName);
        if (handlers.Count == 0) yield break;

        _redactedOutput.WriteLine();
        _redactedOutput.WriteLine($"Running {handlers.Count} failure handler{(handlers.Count == 1 ? "" : "s")} for {failedTargetName}:");
        foreach (var handler in handlers)
        {
            yield return handler.Name;
            try
            {
                var subOrder = Graph.ComputeExecutionOrder(handler.Name);
                foreach (var sub in subOrder)
                {
                    if (CheckSkippedByCondition(sub) is { } skip)
                    {
                        _redactedOutput.WriteLine($"==> {sub.Name} (skipped: {skip})");
                        continue;
                    }
                    _redactedOutput.WriteLine($"==> {sub.Name} (failure handler for {failedTargetName})");
                    foreach (var action in sub.Actions) action();
                    foreach (var factory in sub.PlanFactories)
                    {
                        foreach (var plan in factory())
                        {
                            _redactionTable.RegisterAll(plan);
                            var subExit = ProcessRunner.Execute(plan, _redactedOutput, _redactedOutput);
                            if (subExit != 0)
                            {
                                _redactedOutput.WriteLine($"==> {sub.Name} (handler) FAILED (exit {subExit}); original failure stands");
                                break;
                            }
                        }
                    }
                }
            }
            catch (Exception hex)
            {
                _redactedOutput.WriteLine($"==> {handler.Name} (handler) threw {hex.GetType().Name}: {hex.Message}; original failure stands");
            }
        }
    }

    private void WriteBuildSummary(IReadOnlyList<TargetExecutionRecord> records, TimeSpan duration, string? failedTargetName)
    {
        if (records.Count == 0) return;

        _redactedOutput.WriteLine();
        _redactedOutput.WriteLine("─── Build Summary ───");
        var nameWidth = Math.Max("Target".Length, records.Max(r => r.Name.Length));
        var statusWidth = Math.Max("Status".Length, records.Max(r => r.Status.ToString().Length));

        _redactedOutput.WriteLine($"  {"Target".PadRight(nameWidth)}   {"Status".PadRight(statusWidth)}   Duration");
        foreach (var r in records)
        {
            var statusGlyph = r.Status switch
            {
                TargetStatus.Done => "✓",
                TargetStatus.Failed => "✗",
                TargetStatus.Skipped => "·",
                TargetStatus.NotRun => "—",
                _ => "?",
            };
            var status = $"{statusGlyph} {r.Status}";
            var durationText = r.Status is TargetStatus.Skipped or TargetStatus.NotRun
                ? string.Empty
                : FormatDuration(r.Duration);
            _redactedOutput.WriteLine($"  {r.Name.PadRight(nameWidth)}   {status.PadRight(statusWidth + 2)}   {durationText}");
        }
        _redactedOutput.WriteLine($"  {"".PadRight(nameWidth)}   {"".PadRight(statusWidth + 2)}   ─────");
        _redactedOutput.WriteLine($"  {"Total".PadRight(nameWidth)}   {"".PadRight(statusWidth + 2)}   {FormatDuration(duration)}");

        if (failedTargetName is not null)
        {
            _redactedOutput.WriteLine();
            _redactedOutput.WriteLine($"BUILD FAILED — first failed target: {failedTargetName}");
        }
    }

    private static string FormatDuration(TimeSpan d)
    {
        if (d.TotalMilliseconds < 1000) return $"{d.TotalMilliseconds:F0} ms";
        if (d.TotalSeconds < 60) return $"{d.TotalSeconds:F1} s";
        return $"{(int)d.TotalMinutes}m {d.Seconds}s";
    }
}

/// <summary>Per-target outcome inside a build run.</summary>
public sealed record TargetExecutionRecord
{
    public required string Name { get; init; }
    public required TargetStatus Status { get; init; }
    public TimeSpan Duration { get; init; }
    public string? FailureReason { get; init; }

    public static TargetExecutionRecord Done(string name, TimeSpan duration)
        => new() { Name = name, Status = TargetStatus.Done, Duration = duration };
    public static TargetExecutionRecord Failed(string name, TimeSpan duration, string reason)
        => new() { Name = name, Status = TargetStatus.Failed, Duration = duration, FailureReason = reason };
    public static TargetExecutionRecord Skipped(string name, string reason)
        => new() { Name = name, Status = TargetStatus.Skipped, FailureReason = reason };
    public static TargetExecutionRecord NotRun(string name)
        => new() { Name = name, Status = TargetStatus.NotRun };
}

public enum TargetStatus { Done, Failed, Skipped, NotRun }

/// <summary>Outcome of a single executor run.</summary>
public sealed record ExecutionResult
{
    public required ExecutionMode Mode { get; init; }
    public required int ExitCode { get; init; }
    public int TargetsTraversed { get; init; }
    public int CommandPlansPrinted { get; init; }
    public string? FailedTarget { get; init; }
    public IReadOnlyList<string> FailureHandlersInvoked { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> SkippedTargets { get; init; } = Array.Empty<string>();
    public IReadOnlyList<TargetExecutionRecord> ExecutionRecords { get; init; } = Array.Empty<TargetExecutionRecord>();
    public TimeSpan Duration { get; init; }
}

