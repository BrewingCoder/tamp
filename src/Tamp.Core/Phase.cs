namespace Tamp;

/// <summary>
/// Lifecycle bucket for a target. Used for grouping in build summaries and
/// for default scheduling preferences. Custom phases are permitted via the
/// <see cref="PhaseDescriptor"/> escape hatch.
/// </summary>
public enum Phase
{
    /// <summary>Default; the target has not declared a phase.</summary>
    None = 0,
    Restore,
    Build,
    Test,
    Pack,
    Publish,
    Deploy,
    /// <summary>User-defined phase. Pair with <see cref="PhaseDescriptor"/> to give it a name.</summary>
    Custom,
}

/// <summary>
/// Optional descriptor for a custom phase, used when <see cref="Phase.Custom"/>
/// is chosen and a human-readable phase name is wanted in summaries.
/// </summary>
public sealed record PhaseDescriptor(string Name);
