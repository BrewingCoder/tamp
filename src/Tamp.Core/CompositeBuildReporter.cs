namespace Tamp;

/// <summary>
/// Fans out <see cref="IBuildReporter"/> events to a fixed list of inner
/// reporters in registration order. Used by the framework to combine the
/// CLI-selected default reporter (Noop or Json) with any
/// <see cref="BuildReporterAttribute"/>-registered reporters the adopter
/// provides on their <see cref="TampBuild"/>.
/// </summary>
/// <remarks>
/// Exceptions thrown by one inner reporter do not stop the rest from
/// receiving the event — the framework would rather fire 4-of-5 reporters
/// than swallow a build event entirely. Failures are written to
/// <see cref="System.Console.Error"/> with the offending reporter's type
/// name so an operator can chase the cause without losing the build's
/// other observability signals.
/// </remarks>
public sealed class CompositeBuildReporter : IBuildReporter
{
    private readonly System.Collections.Generic.IReadOnlyList<IBuildReporter> _inner;

    public CompositeBuildReporter(params IBuildReporter[] inner)
        : this((System.Collections.Generic.IReadOnlyList<IBuildReporter>)inner) { }

    public CompositeBuildReporter(System.Collections.Generic.IReadOnlyList<IBuildReporter> inner)
    {
        _inner = inner ?? throw new System.ArgumentNullException(nameof(inner));
    }

    /// <summary>The inner reporters this composite fans events out to, in registration order.</summary>
    public System.Collections.Generic.IReadOnlyList<IBuildReporter> InnerReporters => _inner;

    public void OnBuildStart(string buildId, System.Collections.Generic.IReadOnlyList<string> requestedTargets, System.Collections.Generic.IReadOnlyList<string> executionClosure)
        => DispatchAll(r => r.OnBuildStart(buildId, requestedTargets, executionClosure), nameof(OnBuildStart));

    public void OnTargetStart(string name)
        => DispatchAll(r => r.OnTargetStart(name), nameof(OnTargetStart));

    public void OnTargetSucceeded(string name, System.TimeSpan duration)
        => DispatchAll(r => r.OnTargetSucceeded(name, duration), nameof(OnTargetSucceeded));

    public void OnTargetFailed(TargetFailureDetail detail)
        => DispatchAll(r => r.OnTargetFailed(detail), nameof(OnTargetFailed));

    public void OnTargetSkipped(string name, string reason)
        => DispatchAll(r => r.OnTargetSkipped(name, reason), nameof(OnTargetSkipped));

    public void OnTargetNotRun(string name, string reason)
        => DispatchAll(r => r.OnTargetNotRun(name, reason), nameof(OnTargetNotRun));

    public void OnBuildEnd(string status, string? firstFailedTarget, int exitCode, System.TimeSpan totalDuration)
        => DispatchAll(r => r.OnBuildEnd(status, firstFailedTarget, exitCode, totalDuration), nameof(OnBuildEnd));

    private void DispatchAll(System.Action<IBuildReporter> dispatch, string eventName)
    {
        for (var i = 0; i < _inner.Count; i++)
        {
            var reporter = _inner[i];
            try
            {
                dispatch(reporter);
            }
            catch (System.Exception ex)
            {
                System.Console.Error.WriteLine(
                    $"[tamp] reporter '{reporter.GetType().FullName}' threw on {eventName}: {ex.GetType().Name}: {ex.Message}");
            }
        }
    }
}
