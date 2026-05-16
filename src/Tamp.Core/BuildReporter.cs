using System.Text.Json;
using System.Text.Json.Serialization;

namespace Tamp;

/// <summary>
/// Build-lifecycle event sink. Implementations receive structured events at
/// well-defined hooks in the Executor's run loop, independent of the
/// human-readable text emit. TAM-140.
/// </summary>
/// <remarks>
/// The default <see cref="NoopBuildReporter"/> is a no-op — the Executor's
/// existing <c>_log.WriteRaw(...)</c> calls handle human-facing text output
/// as before. Pass <see cref="JsonBuildReporter"/> via Executor's constructor
/// (typically wired from <c>--reporter=json</c>) to emit NDJSON events on
/// a dedicated writer; when active, the Logger's output is routed to
/// <see cref="System.IO.TextWriter.Null"/> so stdout carries only NDJSON.
/// </remarks>
public interface IBuildReporter
{
    void OnBuildStart(string buildId, IReadOnlyList<string> requestedTargets, IReadOnlyList<string> executionClosure);

    /// <summary>Called when a target's `Executes` block is about to run.</summary>
    void OnTargetStart(string name);

    /// <summary>Called when a target completed successfully.</summary>
    void OnTargetSucceeded(string name, TimeSpan duration);

    /// <summary>
    /// Called when a target failed — Executes threw, the wrapped CommandPlan
    /// exited non-zero, or a Requires precondition was unmet. The
    /// <see cref="TargetFailureDetail.OutputTail"/> on the payload carries
    /// the last N lines of merged stdout+stderr the target emitted before
    /// failing (TAM-230 background — adopter notify reporters use this for
    /// rich failure messages without parsing terminal output themselves).
    /// </summary>
    void OnTargetFailed(TargetFailureDetail detail);

    /// <summary>Called when a target was skipped (user --skip / OnlyWhen / Requires-failed / upstream-failure).</summary>
    void OnTargetSkipped(string name, string reason);

    /// <summary>Called when a target wasn't run at all (upstream failure, build aborted).</summary>
    void OnTargetNotRun(string name, string reason);

    void OnBuildEnd(string status, string? firstFailedTarget, int exitCode, TimeSpan totalDuration);
}

/// <summary>Default no-op reporter. Used when no <see cref="IBuildReporter"/> is supplied.</summary>
public sealed class NoopBuildReporter : IBuildReporter
{
    public static readonly NoopBuildReporter Instance = new();
    private NoopBuildReporter() { }
    public void OnBuildStart(string buildId, IReadOnlyList<string> requestedTargets, IReadOnlyList<string> executionClosure) { }
    public void OnTargetStart(string name) { }
    public void OnTargetSucceeded(string name, TimeSpan duration) { }
    public void OnTargetFailed(TargetFailureDetail detail) { }
    public void OnTargetSkipped(string name, string reason) { }
    public void OnTargetNotRun(string name, string reason) { }
    public void OnBuildEnd(string status, string? firstFailedTarget, int exitCode, TimeSpan totalDuration) { }
}

/// <summary>
/// NDJSON event reporter. Emits one JSON object per line to the configured
/// writer (typically stdout). Used by the <c>--reporter=json</c> CLI flag for
/// IDE-extension consumption of build progress. TAM-140.
/// </summary>
/// <remarks>
/// Event schema mirrors the contract documented in TAM-140: <c>build.start</c>,
/// <c>target.start</c>, <c>target.end</c> (status = succeeded | failed),
/// <c>target.skipped</c>, <c>target.not_run</c>, <c>build.end</c>. Timestamps
/// are ISO-8601 UTC with millisecond precision. Each line is independently
/// parseable; no trailing comma, no array wrapper.
/// </remarks>
public sealed class JsonBuildReporter : IBuildReporter
{
    private static readonly JsonSerializerOptions Options = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly TextWriter _writer;
    private readonly object _writeLock = new();

    public JsonBuildReporter(TextWriter writer)
    {
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));
    }

    private static string NowIso() =>
        DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", System.Globalization.CultureInfo.InvariantCulture);

    private void Emit<T>(T payload)
    {
        lock (_writeLock)
        {
            _writer.WriteLine(JsonSerializer.Serialize(payload, Options));
            _writer.Flush();
        }
    }

    public void OnBuildStart(string buildId, IReadOnlyList<string> requestedTargets, IReadOnlyList<string> executionClosure)
        => Emit(new
        {
            @event = "build.start",
            build_id = buildId,
            started_at = NowIso(),
            requested_targets = requestedTargets,
            closure = executionClosure,
        });

    public void OnTargetStart(string name)
        => Emit(new
        {
            @event = "target.start",
            name,
            started_at = NowIso(),
        });

    public void OnTargetSucceeded(string name, TimeSpan duration)
        => Emit(new
        {
            @event = "target.end",
            name,
            status = "succeeded",
            duration_ms = (long)duration.TotalMilliseconds,
            ended_at = NowIso(),
        });

    public void OnTargetFailed(TargetFailureDetail detail)
        => Emit(new
        {
            @event = "target.end",
            name = detail.TargetName,
            status = "failed",
            duration_ms = (long)detail.Duration.TotalMilliseconds,
            failure_reason = detail.FailureReason,
            output_tail = detail.OutputTail,
            ended_at = NowIso(),
        });

    public void OnTargetSkipped(string name, string reason)
        => Emit(new
        {
            @event = "target.skipped",
            name,
            reason,
        });

    public void OnTargetNotRun(string name, string reason)
        => Emit(new
        {
            @event = "target.not_run",
            name,
            reason,
        });

    public void OnBuildEnd(string status, string? firstFailedTarget, int exitCode, TimeSpan totalDuration)
        => Emit(new
        {
            @event = "build.end",
            status,
            first_failed_target = firstFailedTarget,
            exit_code = exitCode,
            total_duration_ms = (long)totalDuration.TotalMilliseconds,
            ended_at = NowIso(),
        });
}
