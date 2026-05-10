namespace Tamp;

/// <summary>
/// Runs a target graph in topological order. v0 is sequential; resource
/// scheduling, retry, and parallelism are recorded on the spec but not
/// yet honored here. (Filed in TAM-25 follow-ups.)
/// </summary>
public sealed class Executor
{
    public Executor(TargetGraph graph, ExecutionMode mode = ExecutionMode.Run, TextWriter? output = null)
    {
        Graph = graph ?? throw new ArgumentNullException(nameof(graph));
        Mode = mode;
        Output = output ?? Console.Out;
    }

    public TargetGraph Graph { get; }
    public ExecutionMode Mode { get; }
    public TextWriter Output { get; }

    /// <summary>Run / dry-run / plan the target named <paramref name="rootTargetName"/>.</summary>
    public ExecutionResult Run(string rootTargetName)
    {
        var order = Graph.TopologicalOrderFor(rootTargetName);

        return Mode switch
        {
            ExecutionMode.Plan => RunPlan(order, rootTargetName),
            ExecutionMode.DryRun => RunDryRun(order),
            ExecutionMode.Run => RunActual(order),
            _ => throw new InvalidOperationException($"Unknown execution mode: {Mode}"),
        };
    }

    private ExecutionResult RunPlan(IReadOnlyList<TargetSpec> order, string rootName)
    {
        Output.WriteLine($"Plan for '{rootName}' ({order.Count} target{(order.Count == 1 ? "" : "s")}):");
        Output.WriteLine();
        foreach (var spec in order)
        {
            var phase = spec.Phase == Phase.None ? string.Empty : $" [{spec.Phase}]";
            Output.WriteLine($"  {spec.Name}{phase}");
            if (spec.Dependencies.Count > 0)
                Output.WriteLine($"    depends on: {string.Join(", ", spec.Dependencies)}");
            if (spec.RequiresNetwork || spec.RequiresDocker || spec.RequiresAdmin)
            {
                var caps = new List<string>();
                if (spec.RequiresNetwork) caps.Add("network");
                if (spec.RequiresDocker) caps.Add("docker");
                if (spec.RequiresAdmin) caps.Add("admin");
                Output.WriteLine($"    requires: {string.Join(", ", caps)}");
            }
        }
        return new ExecutionResult { Mode = Mode, ExitCode = 0, TargetsTraversed = order.Count };
    }

    private ExecutionResult RunDryRun(IReadOnlyList<TargetSpec> order)
    {
        Output.WriteLine("[DRY RUN] No commands will execute.");
        Output.WriteLine();
        var planCount = 0;
        foreach (var spec in order)
        {
            // Pure-action targets without command plans are silent in dry-run
            // — by definition there's no spawn to print. They'll still run in
            // Run mode.
            foreach (var factory in spec.PlanFactories)
            {
                foreach (var plan in factory())
                {
                    ProcessRunner.Print(plan, spec.Name, sourceModule: null, Output);
                    planCount++;
                }
            }
        }
        return new ExecutionResult { Mode = Mode, ExitCode = 0, TargetsTraversed = order.Count, CommandPlansPrinted = planCount };
    }

    private ExecutionResult RunActual(IReadOnlyList<TargetSpec> order)
    {
        var traversed = 0;
        foreach (var spec in order)
        {
            traversed++;
            Output.WriteLine($"==> {spec.Name}");

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
                        var exit = ProcessRunner.Execute(plan, Output, Output);
                        if (exit != 0)
                        {
                            Output.WriteLine($"==> {spec.Name} FAILED (exit {exit})");
                            if (spec.FailureMode == FailureMode.Continue) continue;
                            return new ExecutionResult { Mode = Mode, ExitCode = exit, TargetsTraversed = traversed, FailedTarget = spec.Name };
                        }
                    }
                }
            }
            catch (Exception ex) when (spec.FailureMode == FailureMode.Continue)
            {
                Output.WriteLine($"==> {spec.Name} threw {ex.GetType().Name}; continuing per FailureMode.Continue: {ex.Message}");
            }
            catch (Exception ex)
            {
                Output.WriteLine($"==> {spec.Name} threw {ex.GetType().Name}: {ex.Message}");
                return new ExecutionResult { Mode = Mode, ExitCode = 1, TargetsTraversed = traversed, FailedTarget = spec.Name };
            }
        }

        return new ExecutionResult { Mode = Mode, ExitCode = 0, TargetsTraversed = traversed };
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
}
