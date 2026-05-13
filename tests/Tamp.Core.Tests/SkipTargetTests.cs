using Xunit;

namespace Tamp.Core.Tests;

/// <summary>
/// Tests for the <c>--skip &lt;target&gt;</c> and <c>--skip-deps</c> CLI flags
/// (TAM-207). Covers parsing, validation, and execution behavior — skipped
/// targets are recorded as Skipped with the correct reason, dependents still
/// run, --skip-deps elides everything but the root.
/// </summary>
public sealed class SkipTargetTests
{
    private sealed class ThreeTargetBuild : TampBuild
    {
        public static int RestoreCount;
        public static int CompileCount;
        public static int TestCount;

        public Target Restore => _ => _.Description("Restore").Executes(() => { RestoreCount++; });
        public Target Compile => _ => _.DependsOn(nameof(Restore)).Executes(() => { CompileCount++; });
        public Target Test => _ => _.DependsOn(nameof(Compile)).Executes(() => { TestCount++; });

        public static void Reset() { RestoreCount = 0; CompileCount = 0; TestCount = 0; }
    }

    // ---- ParseInvocation: --skip / --skip-deps parsing ----

    [Fact]
    public void ParseInvocation_Captures_Single_Skip()
    {
        var targets = TampBuild.CollectTargets(new ThreeTargetBuild());
        var (_, _, _, _, _, skip, skipDeps, _, _) = TampBuild.ParseInvocation(["Test", "--skip", "Restore"], targets);
        Assert.Contains("Restore", skip);
        Assert.False(skipDeps);
    }

    [Fact]
    public void ParseInvocation_Accepts_Inline_Equals_Form()
    {
        var targets = TampBuild.CollectTargets(new ThreeTargetBuild());
        var (_, _, _, _, _, skip, _, _, _) = TampBuild.ParseInvocation(["Test", "--skip=Restore"], targets);
        Assert.Contains("Restore", skip);
    }

    [Fact]
    public void ParseInvocation_Captures_Multiple_Skips()
    {
        var targets = TampBuild.CollectTargets(new ThreeTargetBuild());
        var (_, _, _, _, _, skip, _, _, _) = TampBuild.ParseInvocation(
            ["Test", "--skip", "Restore", "--skip", "Compile"], targets);
        Assert.Equal(2, skip.Count);
        Assert.Contains("Restore", skip);
        Assert.Contains("Compile", skip);
    }

    [Fact]
    public void ParseInvocation_Captures_SkipDeps_Flag()
    {
        var targets = TampBuild.CollectTargets(new ThreeTargetBuild());
        var (_, _, _, _, _, _, skipDeps, _, _) = TampBuild.ParseInvocation(["Test", "--skip-deps"], targets);
        Assert.True(skipDeps);
    }

    [Fact]
    public void ParseInvocation_Throws_On_Skip_Without_Value()
    {
        var targets = TampBuild.CollectTargets(new ThreeTargetBuild());
        // --skip is the last token; no value follows.
        var ex = Assert.Throws<InvalidOperationException>(() =>
            TampBuild.ParseInvocation(["Test", "--skip"], targets));
        Assert.Contains("target name", ex.Message);
    }

    // ---- Executor: skipped targets are recorded as Skipped ----

    [Fact]
    public void Executor_Skips_Target_In_SkippedByUser_Set()
    {
        ThreeTargetBuild.Reset();
        var build = new ThreeTargetBuild();
        var targets = TampBuild.CollectTargets(build);
        var graph = new TargetGraph(targets);

        var executor = new Executor(
            graph,
            skippedByUser: new HashSet<string> { "Restore" });
        var result = executor.Run("Test");

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(0, ThreeTargetBuild.RestoreCount);   // skipped — no Executes
        Assert.Equal(1, ThreeTargetBuild.CompileCount);   // ran — Compile depends on Restore but treats Skipped as satisfied
        Assert.Equal(1, ThreeTargetBuild.TestCount);
    }

    [Fact]
    public void Executor_Skips_Multiple_Targets_In_SkippedByUser_Set()
    {
        ThreeTargetBuild.Reset();
        var build = new ThreeTargetBuild();
        var targets = TampBuild.CollectTargets(build);
        var graph = new TargetGraph(targets);

        var executor = new Executor(
            graph,
            skippedByUser: new HashSet<string> { "Restore", "Compile" });
        var result = executor.Run("Test");

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(0, ThreeTargetBuild.RestoreCount);
        Assert.Equal(0, ThreeTargetBuild.CompileCount);
        Assert.Equal(1, ThreeTargetBuild.TestCount);     // root still runs
    }

    [Fact]
    public void Executor_Skips_Root_Target_When_In_SkippedByUser_Set()
    {
        ThreeTargetBuild.Reset();
        var build = new ThreeTargetBuild();
        var targets = TampBuild.CollectTargets(build);
        var graph = new TargetGraph(targets);

        // Edge case: adopter says --skip Test on `tamp Test`. Test itself is
        // skipped; its deps still run (since Test isn't blocking them).
        var executor = new Executor(
            graph,
            skippedByUser: new HashSet<string> { "Test" });
        var result = executor.Run("Test");

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(1, ThreeTargetBuild.RestoreCount);
        Assert.Equal(1, ThreeTargetBuild.CompileCount);
        Assert.Equal(0, ThreeTargetBuild.TestCount);     // skipped
    }

    // ---- Executor: --skip-deps elides everything except the root ----

    [Fact]
    public void Executor_SkipDeps_Runs_Only_The_Root()
    {
        ThreeTargetBuild.Reset();
        var build = new ThreeTargetBuild();
        var targets = TampBuild.CollectTargets(build);
        var graph = new TargetGraph(targets);

        var executor = new Executor(graph, skipDependencies: true);
        var result = executor.Run("Test");

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(0, ThreeTargetBuild.RestoreCount);
        Assert.Equal(0, ThreeTargetBuild.CompileCount);
        Assert.Equal(1, ThreeTargetBuild.TestCount);
    }

    [Fact]
    public void Executor_SkipDeps_With_Multiple_Roots_Runs_All_Roots()
    {
        ThreeTargetBuild.Reset();
        var build = new ThreeTargetBuild();
        var targets = TampBuild.CollectTargets(build);
        var graph = new TargetGraph(targets);

        var executor = new Executor(graph, skipDependencies: true);
        // Two explicit roots; --skip-deps means run both, skip everything else.
        // (Here Compile transitively requires Restore; both are roots, so both run.)
        var result = executor.Run("Compile", "Test");

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(0, ThreeTargetBuild.RestoreCount);   // skipped — not a root
        Assert.Equal(1, ThreeTargetBuild.CompileCount);   // root
        Assert.Equal(1, ThreeTargetBuild.TestCount);      // root
    }

    [Fact]
    public void Executor_With_No_Skip_Config_Runs_Full_Graph_As_Before()
    {
        ThreeTargetBuild.Reset();
        var build = new ThreeTargetBuild();
        var targets = TampBuild.CollectTargets(build);
        var graph = new TargetGraph(targets);

        // Default constructor — no skip config.
        var executor = new Executor(graph);
        var result = executor.Run("Test");

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(1, ThreeTargetBuild.RestoreCount);
        Assert.Equal(1, ThreeTargetBuild.CompileCount);
        Assert.Equal(1, ThreeTargetBuild.TestCount);
    }

    [Fact]
    public void Executor_Default_SkippedByUser_Is_Empty_Set()
    {
        var build = new ThreeTargetBuild();
        var graph = new TargetGraph(TampBuild.CollectTargets(build));
        var executor = new Executor(graph);
        Assert.Empty(executor.SkippedByUser);
        Assert.False(executor.SkipDependencies);
    }
}
