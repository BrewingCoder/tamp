namespace Tamp;

/// <summary>
/// Runs a target graph in topological order. v0 is sequential; resource
/// scheduling, parallelism, and per-target retry are recorded on the spec
/// but not yet honored here. (Filed in TAM-25 follow-ups.)
/// </summary>
/// <remarks>
/// The executor honors <see cref="ITargetDefinition.OnlyWhen"/> conditions
/// and <see cref="ITargetDefinition.OnFailureOf"/> failure handlers. When a
/// target fails, the executor:
/// <list type="number">
///   <item>Records the failure (target name + exit code or exception).</item>
///   <item>Looks up handlers via <see cref="TargetGraph.HandlersFor"/>.</item>
///   <item>Builds a sub-plan for each handler (handler + its own deps) and
///         runs it sequentially.</item>
///   <item>Returns the original failure's exit code regardless of whether
///         the handler succeeded — handlers do not reverse the failure.</item>
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
            // Pure-action targets without command plans are silent in dry-run
            // — by definition there's no spawn to print. They'll still run in
            // Run mode.
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
        var traversed = 0;
        var skipped = new List<string>();
        foreach (var spec in order)
        {
            traversed++;

            if (CheckSkippedByCondition(spec) is { } skipReason)
            {
                _redactedOutput.WriteLine($"==> {spec.Name} (skipped: {skipReason})");
                skipped.Add(spec.Name);
                continue;
            }

            _redactedOutput.WriteLine($"==> {spec.Name}");

            // Capability preflight: fail fast on missing capabilities the
            // target declared as required.
            // TODO: invoke real preflight via HostProfile and tool-availability checks.

            try
            {
                foreach (var action in spec.Actions)
                    action();

                foreach (var factory in spec.PlanFactories)
                {
                    foreach (var plan in factory())
                    {
                        _redactionTable.RegisterAll(plan);
                        var exit = ProcessRunner.Execute(plan, _redactedOutput, _redactedOutput);
                        if (exit != 0)
                        {
                            _redactedOutput.WriteLine($"==> {spec.Name} FAILED (exit {exit})");
                            if (spec.FailureMode == FailureMode.Continue) continue;
                            return Fail(spec.Name, exit, traversed, skipped);
                        }
                    }
                }
            }
            catch (Exception ex) when (spec.FailureMode == FailureMode.Continue)
            {
                _redactedOutput.WriteLine($"==> {spec.Name} threw {ex.GetType().Name}; continuing per FailureMode.Continue: {ex.Message}");
            }
            catch (Exception ex)
            {
                _redactedOutput.WriteLine($"==> {spec.Name} threw {ex.GetType().Name}: {ex.Message}");
                return Fail(spec.Name, exitCode: 1, traversed, skipped);
            }
        }

        _redactedOutput.Flush();
        return new ExecutionResult { Mode = Mode, ExitCode = 0, TargetsTraversed = traversed, SkippedTargets = skipped };
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

    /// <summary>
    /// Common failure path. Logs the failure, dispatches any registered
    /// <see cref="ITargetDefinition.OnFailureOf"/> handlers (including their
    /// own dep trees), and returns the ExecutionResult with the original
    /// failure's exit code preserved.
    /// </summary>
    private ExecutionResult Fail(string failedTargetName, int exitCode, int traversed, IReadOnlyList<string> skipped)
    {
        var handlersInvoked = new List<string>();
        try
        {
            var handlers = Graph.HandlersFor(failedTargetName);
            if (handlers.Count > 0)
            {
                _redactedOutput.WriteLine();
                _redactedOutput.WriteLine($"Running {handlers.Count} failure handler{(handlers.Count == 1 ? "" : "s")} for {failedTargetName}:");
            }
            foreach (var handler in handlers)
            {
                handlersInvoked.Add(handler.Name);
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
        finally
        {
            _redactedOutput.Flush();
        }
        return new ExecutionResult
        {
            Mode = Mode,
            ExitCode = exitCode,
            TargetsTraversed = traversed,
            FailedTarget = failedTargetName,
            FailureHandlersInvoked = handlersInvoked,
            SkippedTargets = skipped,
        };
    }
}

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
}
