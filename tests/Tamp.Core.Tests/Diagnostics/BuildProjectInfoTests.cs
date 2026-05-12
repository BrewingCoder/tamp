using System.Diagnostics;
using System.IO;
using System.Linq;
using Tamp.Diagnostics;
using Xunit;

namespace Tamp.Core.Tests.Diagnostics;

[Collection(nameof(DiagnosticsCollection))]
public sealed class BuildProjectInfoTests : System.IDisposable
{
    private readonly System.Collections.Generic.List<Activity> _activities = [];
    private readonly ActivityListener _listener;

    public BuildProjectInfoTests()
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

    // ---- Resolver unit tests ----

    private sealed class UndecoratedBuild : TampBuild { public Target Noop => _ => _.Executes(() => { }); }

    [BuildProject("HoldFast", Area = "frontend")]
    private sealed class DecoratedBuild : TampBuild { public Target Noop => _ => _.Executes(() => { }); }

    [BuildProject("HoldFast")]                                         // area absent
    private sealed class DecoratedBuildNoArea : TampBuild { public Target Noop => _ => _.Executes(() => { }); }

    [Fact]
    public void Resolver_Prefers_Attribute_Over_Solution_And_Repo()
    {
        var info = BuildProjectInfo.Resolve(typeof(DecoratedBuild), "/foo/bar/Different.slnx", "/foo/bar");
        Assert.Equal("HoldFast", info.Name);
        Assert.Equal("frontend", info.Area);
        Assert.Equal(ProjectNameSource.Attribute, info.NameSource);
    }

    [Fact]
    public void Resolver_Falls_Back_To_Solution_Filename_When_No_Attribute()
    {
        var info = BuildProjectInfo.Resolve(typeof(UndecoratedBuild), "/foo/bar/Strata.slnx", "/foo/bar");
        Assert.Equal("Strata", info.Name);
        Assert.Null(info.Area);
        Assert.Equal(ProjectNameSource.Solution, info.NameSource);
    }

    [Fact]
    public void Resolver_Strips_Sln_And_Slnx_Extensions_Identically()
    {
        Assert.Equal("Strata", BuildProjectInfo.Resolve(typeof(UndecoratedBuild), "/foo/Strata.slnx", null).Name);
        Assert.Equal("Strata", BuildProjectInfo.Resolve(typeof(UndecoratedBuild), "/foo/Strata.sln",  null).Name);
    }

    [Fact]
    public void Resolver_Falls_Back_To_Repo_Directory_When_No_Solution()
    {
        var info = BuildProjectInfo.Resolve(typeof(UndecoratedBuild), null, "/repos/my-project");
        Assert.Equal("my-project", info.Name);
        Assert.Null(info.Area);
        Assert.Equal(ProjectNameSource.RepoDirectory, info.NameSource);
    }

    [Fact]
    public void Resolver_Returns_Unknown_When_Nothing_Resolves()
    {
        var info = BuildProjectInfo.Resolve(typeof(UndecoratedBuild), null, null);
        Assert.Equal("unknown", info.Name);
        Assert.Equal(ProjectNameSource.Default, info.NameSource);
    }

    [Fact]
    public void Resolver_Accepts_Attribute_Without_Area()
    {
        var info = BuildProjectInfo.Resolve(typeof(DecoratedBuildNoArea), null, null);
        Assert.Equal("HoldFast", info.Name);
        Assert.Null(info.Area);
        Assert.Equal(ProjectNameSource.Attribute, info.NameSource);
    }

    [Fact]
    public void Attribute_Rejects_Empty_Or_Whitespace_Name()
    {
        Assert.Throws<System.ArgumentException>(() => new BuildProjectAttribute(""));
        Assert.Throws<System.ArgumentException>(() => new BuildProjectAttribute("   "));
    }

    // ---- End-to-end emission via TampBuild.Execute<T> ----

    [BuildProject("Strata", Area = "api")]
    private sealed class StrataBuild : TampBuild { public Target Compile => _ => _.Executes(() => { }); }

    [Fact]
    public void Build_Span_Emits_Project_Name_And_Area_From_Attribute()
    {
        TampBuild.Execute<StrataBuild>(["Compile"]);
        var build = _activities.Single(a => a.Source.Name == "Tamp.Build" && a.OperationName == "build");
        var tags = build.TagObjects.ToDictionary(t => t.Key, t => t.Value);

        Assert.Equal("Strata", tags[TampDiagnostics.Tags.BuildProjectName]);
        Assert.Equal("api", tags[TampDiagnostics.Tags.BuildProjectArea]);
        Assert.Equal("attribute", tags[TampDiagnostics.Tags.BuildProjectNameSource]);
    }

    private sealed class FallbackBuild : TampBuild { public Target Compile => _ => _.Executes(() => { }); }

    [Fact]
    public void Build_Span_Emits_Project_Name_From_Fallback_When_No_Attribute()
    {
        TampBuild.Execute<FallbackBuild>(["Compile"]);
        var build = _activities.Single(a => a.Source.Name == "Tamp.Build" && a.OperationName == "build");
        var tags = build.TagObjects.ToDictionary(t => t.Key, t => t.Value);

        // Without an attribute we fall back to solution / repo. Whatever it is,
        // the name must be non-empty and the name_source must be one of the
        // non-Attribute vocabulary values.
        Assert.False(string.IsNullOrEmpty(tags[TampDiagnostics.Tags.BuildProjectName]?.ToString()));
        var src = tags[TampDiagnostics.Tags.BuildProjectNameSource]?.ToString();
        Assert.Contains(src, new[] { "solution", "repodirectory", "default" });
    }

    // ---- Non-.NET / no-solution shape: a build script that builds a pure-JS / Python / Rust / etc.
    //      project doesn't declare a [Solution] field. The resolver must still produce a usable name.

    private sealed class PureFrontendBuild : TampBuild
    {
        // Note: no [Solution] field. Could be a pure-React, Yarn-workspace, Python, Rust, Go build.
        public Target FrontendBuild => _ => _.Executes(() => { });
    }

    [Fact]
    public void Resolver_Works_Without_Any_Solution_Declared()
    {
        // Direct unit-test of the resolver with solutionPath=null — the non-.NET case.
        var info = BuildProjectInfo.Resolve(typeof(PureFrontendBuild), solutionPath: null, repoRoot: "/repos/my-react-app");
        Assert.Equal("my-react-app", info.Name);
        Assert.Equal(ProjectNameSource.RepoDirectory, info.NameSource);
    }

    [Fact]
    public void Build_Span_Resolves_Name_Even_When_Build_Class_Has_No_Solution_Field()
    {
        // End-to-end: a build class with NO [Solution] field. Tamp must still emit a project name.
        TampBuild.Execute<PureFrontendBuild>(["FrontendBuild"]);
        var build = _activities.Single(a => a.Source.Name == "Tamp.Build" && a.OperationName == "build");
        var tags = build.TagObjects.ToDictionary(t => t.Key, t => t.Value);

        Assert.False(string.IsNullOrEmpty(tags[TampDiagnostics.Tags.BuildProjectName]?.ToString()));
        // With no Solution field present and no attribute, the source MUST be repodirectory or default
        // — never `solution`, because there isn't one.
        var src = tags[TampDiagnostics.Tags.BuildProjectNameSource]?.ToString();
        Assert.Contains(src, new[] { "repodirectory", "default" });
        Assert.DoesNotContain("solution", src ?? "");
    }
}
