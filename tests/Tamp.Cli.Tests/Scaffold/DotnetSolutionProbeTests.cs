using System;
using System.IO;
using Tamp.Cli.Scaffold.Probes;
using Tamp.Scaffold;
using Xunit;

namespace Tamp.Cli.Tests.Scaffold;

/// <summary>
/// Exercises DotnetSolutionProbe against fabricated directory layouts.
/// </summary>
public sealed class DotnetSolutionProbeTests : IDisposable
{
    private readonly string _root;

    public DotnetSolutionProbeTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "tamp-probe-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { }
    }

    private ScaffoldContextBuilder Ctx() => new()
    {
        RepoRoot = AbsolutePath.Create(_root),
        TampCoreVersion = "1.4.0",
    };

    [Fact]
    public void Single_Slnx_Wins_Over_Sln_When_Both_Present_Singletons()
    {
        File.WriteAllText(Path.Combine(_root, "Foo.slnx"), "");
        File.WriteAllText(Path.Combine(_root, "Foo.sln"), "");

        var ctx = Ctx();
        new DotnetSolutionProbe().Probe(AbsolutePath.Create(_root), ctx);
        var built = ctx.Build();

        Assert.NotNull(built.Solution);
        Assert.EndsWith("Foo.slnx", built.Solution!.Value);
    }

    [Fact]
    public void Single_Sln_Used_When_No_Slnx()
    {
        File.WriteAllText(Path.Combine(_root, "OnlyOld.sln"), "");

        var ctx = Ctx();
        new DotnetSolutionProbe().Probe(AbsolutePath.Create(_root), ctx);
        var built = ctx.Build();

        Assert.NotNull(built.Solution);
        Assert.EndsWith("OnlyOld.sln", built.Solution!.Value);
    }

    [Fact]
    public void Zero_Solutions_Leaves_Slot_Empty_And_Records_Diagnostic()
    {
        var ctx = Ctx();
        new DotnetSolutionProbe().Probe(AbsolutePath.Create(_root), ctx);
        var built = ctx.Build();

        Assert.Null(built.Solution);
        Assert.True(built.ProbeData.TryGetValue("dotnet.solution.detection", out var msg));
        Assert.Equal("no-solution-found", msg);
    }

    [Fact]
    public void Multi_Slnx_Leaves_Slot_Empty_And_Records_Count()
    {
        File.WriteAllText(Path.Combine(_root, "A.slnx"), "");
        File.WriteAllText(Path.Combine(_root, "B.slnx"), "");

        var ctx = Ctx();
        new DotnetSolutionProbe().Probe(AbsolutePath.Create(_root), ctx);
        var built = ctx.Build();

        Assert.Null(built.Solution);
        Assert.Contains("multiple-solutions", built.ProbeData["dotnet.solution.detection"]);
        Assert.Contains("2 .slnx", built.ProbeData["dotnet.solution.detection"]);
    }

    [Fact]
    public void Multi_Sln_Leaves_Slot_Empty_And_Records_Count()
    {
        File.WriteAllText(Path.Combine(_root, "A.sln"), "");
        File.WriteAllText(Path.Combine(_root, "B.sln"), "");

        var ctx = Ctx();
        new DotnetSolutionProbe().Probe(AbsolutePath.Create(_root), ctx);
        var built = ctx.Build();

        Assert.Null(built.Solution);
        Assert.Contains("multiple-solutions", built.ProbeData["dotnet.solution.detection"]);
        Assert.Contains("2 .sln", built.ProbeData["dotnet.solution.detection"]);
    }
}
