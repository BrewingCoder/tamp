namespace Tamp;

/// <summary>How the executor processes targets.</summary>
public enum ExecutionMode
{
    /// <summary>Default: dispatch every action and command plan.</summary>
    Run,

    /// <summary>
    /// Print each <see cref="CommandPlan"/> exactly as it would run, with
    /// secrets redacted, but execute nothing. Pure functions (Actions that
    /// don't spawn processes) are skipped — by definition they have no side
    /// effects to dispatch.
    /// </summary>
    DryRun,

    /// <summary>
    /// Render the target DAG (dependency order, parallelism opportunities)
    /// and exit. No work is performed.
    /// </summary>
    Plan,
}
