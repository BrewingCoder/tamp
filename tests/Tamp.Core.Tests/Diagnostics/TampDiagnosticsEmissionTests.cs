using System.Diagnostics;
using System.Linq;
using Tamp.Diagnostics;
using Xunit;

namespace Tamp.Core.Tests.Diagnostics;

/// <summary>
/// Pin the ActivitySource emission contract that consumers' OTel pipelines
/// will depend on. Subscribes a listener at the start of each test, runs a
/// tiny build, and asserts shape (source names, span names, tag keys).
/// Source names + tag keys are stability contract — ADR 0018.
/// </summary>
[Collection(nameof(DiagnosticsCollection))]
public sealed class TampDiagnosticsEmissionTests : IDisposable
{
    private readonly System.Collections.Generic.List<Activity> _activities = [];
    private readonly ActivityListener _listener;

    public TampDiagnosticsEmissionTests()
    {
        _listener = new ActivityListener
        {
            ShouldListenTo = src => src.Name.StartsWith("Tamp.Build", System.StringComparison.Ordinal),
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = a => { lock (_activities) _activities.Add(a); },
        };
        ActivitySource.AddActivityListener(_listener);
    }

    public void Dispose() => _listener.Dispose();

    private sealed class TrivialBuild : TampBuild
    {
        public Target Compile => _ => _.Executes(() => { });
        public Target Default => _ => _.Default().DependsOn(nameof(Compile));
    }

    [Fact]
    public void Build_Emits_Root_Build_Span_On_Tamp_Build_Source()
    {
        var exit = TampBuild.Execute<TrivialBuild>(["Compile"]);
        Assert.Equal(0, exit);

        var build = _activities.SingleOrDefault(a => a.Source.Name == "Tamp.Build" && a.OperationName == "build");
        Assert.NotNull(build);
        Assert.Equal(ActivityStatusCode.Ok, build!.Status);
    }

    [Fact]
    public void Build_Span_Carries_The_Stability_Contract_Tags()
    {
        TampBuild.Execute<TrivialBuild>(["Compile"]);
        var build = _activities.Single(a => a.Source.Name == "Tamp.Build" && a.OperationName == "build");
        var tags = build.TagObjects.ToDictionary(t => t.Key, t => t.Value);

        // Build identity + outcome
        Assert.Contains(TampDiagnostics.Tags.BuildTargets, tags.Keys);
        Assert.Contains(TampDiagnostics.Tags.BuildCliVersion, tags.Keys);
        Assert.Contains(TampDiagnostics.Tags.BuildExitCode, tags.Keys);
        Assert.Contains(TampDiagnostics.Tags.OutcomeKey, tags.Keys);
        Assert.Equal(TampDiagnostics.Tags.OutcomeSuccess, tags[TampDiagnostics.Tags.OutcomeKey]);

        // High-res timing + memory
        Assert.Contains(TampDiagnostics.Tags.BuildDurationNs, tags.Keys);
        Assert.Contains(TampDiagnostics.Tags.BuildPeakWorkingSetBytes, tags.Keys);

        // Outcome counts
        Assert.Contains(TampDiagnostics.Tags.BuildTargetsTotal, tags.Keys);
        Assert.Contains(TampDiagnostics.Tags.BuildTargetsSucceeded, tags.Keys);
        Assert.Contains(TampDiagnostics.Tags.BuildTargetsFailed, tags.Keys);
        Assert.Contains(TampDiagnostics.Tags.BuildCommandsTotal, tags.Keys);

        // Host facets
        Assert.Contains(TampDiagnostics.Tags.HostOs, tags.Keys);
        Assert.Contains(TampDiagnostics.Tags.HostArch, tags.Keys);
        Assert.Contains(TampDiagnostics.Tags.HostCpuCount, tags.Keys);
        Assert.Contains(TampDiagnostics.Tags.DotnetRuntimeDescription, tags.Keys);
        Assert.Contains(TampDiagnostics.Tags.CiIsCi, tags.Keys);
        Assert.Contains(TampDiagnostics.Tags.CiVendor, tags.Keys);
    }

    [Fact]
    public void Build_Span_Emits_The_Build_Summary_Event()
    {
        TampBuild.Execute<TrivialBuild>(["Compile"]);
        var build = _activities.Single(a => a.Source.Name == "Tamp.Build" && a.OperationName == "build");
        var summary = build.Events.SingleOrDefault(e => e.Name == "tamp.build.summary");
        Assert.NotEqual(default, summary);
        var summaryTags = summary.Tags.ToDictionary(t => t.Key, t => t.Value);
        Assert.Contains(TampDiagnostics.Tags.BuildTargetsTotal, summaryTags.Keys);
        Assert.Contains(TampDiagnostics.Tags.BuildExitCode, summaryTags.Keys);
        Assert.Contains(TampDiagnostics.Tags.OutcomeKey, summaryTags.Keys);
    }

    [Fact]
    public void Target_Spans_Are_Emitted_On_Tamp_Build_Targets_Source()
    {
        TampBuild.Execute<TrivialBuild>(["Compile"]);

        var targetSpans = _activities.Where(a => a.Source.Name == "Tamp.Build.Targets").ToList();
        Assert.NotEmpty(targetSpans);
        Assert.Contains(targetSpans, a => a.OperationName == "target:Compile");
    }

    [Fact]
    public void Target_Span_Carries_The_Stability_Contract_Tags()
    {
        TampBuild.Execute<TrivialBuild>(["Compile"]);
        var compile = _activities.Single(a => a.Source.Name == "Tamp.Build.Targets" && a.OperationName == "target:Compile");
        var tags = compile.TagObjects.ToDictionary(t => t.Key, t => t.Value);

        Assert.Equal("Compile", tags[TampDiagnostics.Tags.TargetName]);
        Assert.Equal(TampDiagnostics.Tags.OutcomeSuccess, tags[TampDiagnostics.Tags.TargetStatus]);
        Assert.Contains(TampDiagnostics.Tags.TargetPhase, tags.Keys);
        Assert.Contains(TampDiagnostics.Tags.TargetDurationNs, tags.Keys);
        Assert.Contains(TampDiagnostics.Tags.TargetStartWorkingSetBytes, tags.Keys);
        Assert.Contains(TampDiagnostics.Tags.TargetEndWorkingSetBytes, tags.Keys);
        Assert.Contains(TampDiagnostics.Tags.TargetGcAllocatedBytes, tags.Keys);
        Assert.Contains(TampDiagnostics.Tags.TargetGcGen0Collections, tags.Keys);
        Assert.Contains(TampDiagnostics.Tags.TargetGcGen1Collections, tags.Keys);
        Assert.Contains(TampDiagnostics.Tags.TargetGcGen2Collections, tags.Keys);
        Assert.Contains(TampDiagnostics.Tags.TargetCpuTimeMs, tags.Keys);
        Assert.Contains(TampDiagnostics.Tags.TargetActionsCount, tags.Keys);
        Assert.Contains(TampDiagnostics.Tags.TargetCommandsCount, tags.Keys);
        Assert.Contains(TampDiagnostics.Tags.TargetFailureMode, tags.Keys);
        Assert.Contains(TampDiagnostics.Tags.TargetAttempt, tags.Keys);
    }

    private sealed class FailingBuild : TampBuild
    {
        public Target Boom => _ => _.Executes((System.Action)(() => throw new System.InvalidOperationException("nope")));
    }

    [Fact]
    public void Failing_Build_Sets_Error_Status_And_Failure_Tags()
    {
        var exit = TampBuild.Execute<FailingBuild>(["Boom"]);
        Assert.NotEqual(0, exit);

        var build = _activities.Single(a => a.Source.Name == "Tamp.Build" && a.OperationName == "build");
        Assert.Equal(ActivityStatusCode.Error, build.Status);
        var tags = build.TagObjects.ToDictionary(t => t.Key, t => t.Value);
        Assert.Equal(TampDiagnostics.Tags.OutcomeFailure, tags[TampDiagnostics.Tags.OutcomeKey]);
        Assert.Equal("Boom", tags[TampDiagnostics.Tags.BuildFailureTarget]);

        var boom = _activities.Single(a => a.Source.Name == "Tamp.Build.Targets" && a.OperationName == "target:Boom");
        Assert.Equal(ActivityStatusCode.Error, boom.Status);
    }

    [Fact]
    public void All_Three_Activity_Sources_Are_Registered_With_Expected_Names()
    {
        // Pin the names directly — these strings are the contract.
        Assert.Equal("Tamp.Build", TampDiagnostics.BuildSource.Name);
        Assert.Equal("Tamp.Build.Targets", TampDiagnostics.TargetsSource.Name);
        Assert.Equal("Tamp.Build.Commands", TampDiagnostics.CommandsSource.Name);
        Assert.Equal("Tamp.Build", TampDiagnostics.Meter.Name);
    }

    [Fact]
    public void Ci_Vendor_Detection_Returns_Known_Vocabulary()
    {
        // The current process either has one of the recognized env vars or not.
        // Whichever it is, the result must be in the pinned vocabulary.
        var v = TampDiagnostics.DetectCiVendor();
        Assert.Contains(v, new[] { "github-actions", "azure-devops", "teamcity", "gitlab-ci", "circleci", "buildkite", "generic", "local" });
    }
}
