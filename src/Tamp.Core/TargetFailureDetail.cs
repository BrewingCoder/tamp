namespace Tamp;

/// <summary>
/// Payload passed to <see cref="IBuildReporter.OnTargetFailed(TargetFailureDetail)"/>.
/// Wrapping the failure context in a record (vs. a long positional parameter list)
/// keeps the <see cref="IBuildReporter"/> contract additive — new failure-context
/// fields land here without breaking implementers.
/// </summary>
public sealed record TargetFailureDetail
{
    /// <summary>The failing target's name.</summary>
    public required string TargetName { get; init; }

    /// <summary>Wall-clock time the target ran before failing.</summary>
    public required System.TimeSpan Duration { get; init; }

    /// <summary>
    /// Human-readable cause: <c>"exit {n}"</c> for a non-zero <c>CommandPlan</c>, or
    /// the exception message for an action that threw, or the unmet <c>Requires</c>
    /// expression text when a precondition failed.
    /// </summary>
    public required string FailureReason { get; init; }

    /// <summary>
    /// Last N lines of merged stdout+stderr the target emitted before failing
    /// (default ring-buffer capacity = 50). May be empty when the target failed
    /// before producing any output (a thrown <c>Requires</c>, for example).
    /// Newest line last; each entry is a single text line with the trailing
    /// newline stripped.
    /// </summary>
    public System.Collections.Generic.IReadOnlyList<string> OutputTail { get; init; } =
        System.Array.Empty<string>();
}
