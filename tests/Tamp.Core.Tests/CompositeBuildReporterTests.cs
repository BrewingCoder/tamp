using System.Collections.Generic;
using Xunit;

namespace Tamp.Core.Tests;

/// <summary>
/// TAM-230 — CompositeBuildReporter fans events out to every inner reporter
/// in registration order, and isolates failures so a misbehaving reporter
/// doesn't starve the rest.
/// </summary>
public sealed class CompositeBuildReporterTests
{
    [Fact]
    public void Each_Event_Fans_Out_To_Every_Inner_In_Registration_Order()
    {
        var a = new RecordingReporter("a");
        var b = new RecordingReporter("b");
        var c = new RecordingReporter("c");
        var composite = new CompositeBuildReporter(a, b, c);

        composite.OnBuildStart("id", new[] { "X" }, new[] { "X" });
        composite.OnTargetStart("X");
        composite.OnTargetSucceeded("X", System.TimeSpan.FromSeconds(1));
        composite.OnTargetFailed(new TargetFailureDetail
        {
            TargetName = "Y", Duration = System.TimeSpan.FromSeconds(2), FailureReason = "exit 1",
        });
        composite.OnTargetSkipped("Z", "by user");
        composite.OnTargetNotRun("W", "upstream failed");
        composite.OnBuildEnd("failed", "Y", 1, System.TimeSpan.FromSeconds(5));

        var expected = new[]
        {
            "BuildStart id",
            "TargetStart X",
            "TargetSucceeded X",
            "TargetFailed Y exit 1",
            "TargetSkipped Z",
            "TargetNotRun W",
            "BuildEnd failed",
        };
        Assert.Equal(expected, a.Events);
        Assert.Equal(expected, b.Events);
        Assert.Equal(expected, c.Events);
    }

    [Fact]
    public void Throwing_Reporter_Does_Not_Starve_The_Rest()
    {
        var a = new RecordingReporter("a");
        var b = new ThrowingReporter();
        var c = new RecordingReporter("c");
        var composite = new CompositeBuildReporter(a, b, c);

        composite.OnTargetStart("X");
        composite.OnTargetSucceeded("X", System.TimeSpan.FromSeconds(1));

        Assert.Equal(new[] { "TargetStart X", "TargetSucceeded X" }, a.Events);
        Assert.Equal(new[] { "TargetStart X", "TargetSucceeded X" }, c.Events);
    }

    [Fact]
    public void Inner_Reporters_Are_Exposed_Read_Only()
    {
        var a = new RecordingReporter("a");
        var b = new RecordingReporter("b");
        var composite = new CompositeBuildReporter(a, b);

        Assert.Equal(2, composite.InnerReporters.Count);
        Assert.Same(a, composite.InnerReporters[0]);
        Assert.Same(b, composite.InnerReporters[1]);
    }

    private sealed class RecordingReporter : IBuildReporter
    {
        public RecordingReporter(string label) { Label = label; }
        public string Label { get; }
        public List<string> Events { get; } = new();
        public void OnBuildStart(string buildId, IReadOnlyList<string> requestedTargets, IReadOnlyList<string> executionClosure) => Events.Add($"BuildStart {buildId}");
        public void OnTargetStart(string name) => Events.Add($"TargetStart {name}");
        public void OnTargetSucceeded(string name, System.TimeSpan duration) => Events.Add($"TargetSucceeded {name}");
        public void OnTargetFailed(TargetFailureDetail detail) => Events.Add($"TargetFailed {detail.TargetName} {detail.FailureReason}");
        public void OnTargetSkipped(string name, string reason) => Events.Add($"TargetSkipped {name}");
        public void OnTargetNotRun(string name, string reason) => Events.Add($"TargetNotRun {name}");
        public void OnBuildEnd(string status, string? firstFailedTarget, int exitCode, System.TimeSpan totalDuration) => Events.Add($"BuildEnd {status}");
    }

    private sealed class ThrowingReporter : IBuildReporter
    {
        public void OnBuildStart(string buildId, IReadOnlyList<string> requestedTargets, IReadOnlyList<string> executionClosure) => throw new System.Exception("nope");
        public void OnTargetStart(string name) => throw new System.Exception("nope");
        public void OnTargetSucceeded(string name, System.TimeSpan duration) => throw new System.Exception("nope");
        public void OnTargetFailed(TargetFailureDetail detail) => throw new System.Exception("nope");
        public void OnTargetSkipped(string name, string reason) => throw new System.Exception("nope");
        public void OnTargetNotRun(string name, string reason) => throw new System.Exception("nope");
        public void OnBuildEnd(string status, string? firstFailedTarget, int exitCode, System.TimeSpan totalDuration) => throw new System.Exception("nope");
    }
}
