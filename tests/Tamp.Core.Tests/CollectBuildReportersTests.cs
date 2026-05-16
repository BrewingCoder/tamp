using System.Collections.Generic;
using Xunit;

namespace Tamp.Core.Tests;

/// <summary>
/// TAM-230 — TampBuild.CollectBuildReporters walks the build class for
/// [BuildReporter]-marked fields/properties and returns the non-null
/// IBuildReporter values for fan-out via CompositeBuildReporter.
/// </summary>
public sealed class CollectBuildReportersTests
{
    [Fact]
    public void Collects_Reporter_Marked_Field()
    {
        var build = new BuildWithFieldReporter();
        var collected = TampBuild.CollectBuildReporters(build);

        var single = Assert.Single(collected);
        Assert.Same(build.Notifier, single);
    }

    [Fact]
    public void Collects_Reporter_Marked_Property()
    {
        var build = new BuildWithPropertyReporter();
        var collected = TampBuild.CollectBuildReporters(build);

        var single = Assert.Single(collected);
        Assert.Same(build.Notifier, single);
    }

    [Fact]
    public void Skips_Null_Reporter_Value()
    {
        var build = new BuildWithNullReporter();
        var collected = TampBuild.CollectBuildReporters(build);

        Assert.Empty(collected);
    }

    [Fact]
    public void Multiple_Reporters_Are_All_Collected()
    {
        var build = new BuildWithTwoReporters();
        var collected = TampBuild.CollectBuildReporters(build);

        Assert.Equal(2, collected.Count);
        Assert.Contains(build.Telegram, collected);
        Assert.Contains(build.Slack, collected);
    }

    [Fact]
    public void Returns_Empty_When_No_BuildReporter_Members()
    {
        var build = new BuildWithoutReporters();
        var collected = TampBuild.CollectBuildReporters(build);

        Assert.Empty(collected);
    }

    [Fact]
    public void Non_IBuildReporter_Typed_Member_Throws_Actionable()
    {
        var build = new BuildWithWrongTypeReporter();
        var ex = Assert.Throws<System.InvalidOperationException>(() => TampBuild.CollectBuildReporters(build));
        Assert.Contains("requires an IBuildReporter-typed", ex.Message);
        Assert.Contains("BadlyTypedReporter", ex.Message);
    }

    // ── Test fixtures ───────────────────────────────────────────────────

    private sealed class BuildWithFieldReporter : TampBuild
    {
        [BuildReporter] internal readonly IBuildReporter Notifier = new StubReporter("notifier");
    }

    private sealed class BuildWithPropertyReporter : TampBuild
    {
        [BuildReporter] internal IBuildReporter Notifier { get; } = new StubReporter("notifier");
    }

    private sealed class BuildWithNullReporter : TampBuild
    {
        [BuildReporter] internal readonly IBuildReporter? Notifier = null;
    }

    private sealed class BuildWithTwoReporters : TampBuild
    {
        [BuildReporter] internal readonly IBuildReporter Telegram = new StubReporter("telegram");
        [BuildReporter] internal readonly IBuildReporter Slack = new StubReporter("slack");
    }

    private sealed class BuildWithoutReporters : TampBuild
    {
        internal readonly IBuildReporter NotMarked = new StubReporter("not-marked");
    }

    private sealed class BuildWithWrongTypeReporter : TampBuild
    {
        [BuildReporter] internal readonly string BadlyTypedReporter = "this is not an IBuildReporter";
    }

    private sealed class StubReporter : IBuildReporter
    {
        public StubReporter(string label) { Label = label; }
        public string Label { get; }
        public void OnBuildStart(string buildId, IReadOnlyList<string> requestedTargets, IReadOnlyList<string> executionClosure) { }
        public void OnTargetStart(string name) { }
        public void OnTargetSucceeded(string name, System.TimeSpan duration) { }
        public void OnTargetFailed(TargetFailureDetail detail) { }
        public void OnTargetSkipped(string name, string reason) { }
        public void OnTargetNotRun(string name, string reason) { }
        public void OnBuildEnd(string status, string? firstFailedTarget, int exitCode, System.TimeSpan totalDuration) { }
    }
}
