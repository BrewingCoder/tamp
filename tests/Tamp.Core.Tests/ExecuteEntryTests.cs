using Xunit;

namespace Tamp.Core.Tests;

/// <summary>
/// End-to-end tests for the <see cref="TampBuild.Execute{T}"/> entry point.
/// Verifies argument-to-mode parsing and that the executor runs against the
/// real build class via reflection.
/// </summary>
public sealed class ExecuteEntryTests
{
    private sealed class SimpleBuild : TampBuild
    {
        public static int RestoreCount;
        public static int CompileCount;

        public Target Restore => _ => _.Description("restore").Executes(() => RestoreCount++);
        public Target Compile => _ => _.DependsOn(nameof(Restore)).Executes(() => CompileCount++);
        public Target Default => _ => _.DependsOn(nameof(Compile));
    }

    [Fact]
    public void Default_Target_Runs_When_No_Argument_Given()
    {
        SimpleBuild.RestoreCount = 0;
        SimpleBuild.CompileCount = 0;
        var exit = TampBuild.Execute<SimpleBuild>([]);
        Assert.Equal(0, exit);
        Assert.Equal(1, SimpleBuild.RestoreCount);
        Assert.Equal(1, SimpleBuild.CompileCount);
    }

    [Fact]
    public void Specific_Target_Runs_When_Named()
    {
        SimpleBuild.RestoreCount = 0;
        SimpleBuild.CompileCount = 0;
        var exit = TampBuild.Execute<SimpleBuild>(["Restore"]);
        Assert.Equal(0, exit);
        Assert.Equal(1, SimpleBuild.RestoreCount);
        Assert.Equal(0, SimpleBuild.CompileCount);
    }

    [Fact]
    public void DryRun_Flag_Skips_Action_Execution()
    {
        SimpleBuild.RestoreCount = 0;
        SimpleBuild.CompileCount = 0;
        var exit = TampBuild.Execute<SimpleBuild>(["Compile", "--dry-run"]);
        Assert.Equal(0, exit);
        // DryRun skips both actions and plans.
        Assert.Equal(0, SimpleBuild.RestoreCount);
        Assert.Equal(0, SimpleBuild.CompileCount);
    }

    [Fact]
    public void Plan_Flag_Skips_Action_Execution()
    {
        SimpleBuild.RestoreCount = 0;
        var exit = TampBuild.Execute<SimpleBuild>(["Compile", "--plan"]);
        Assert.Equal(0, exit);
        Assert.Equal(0, SimpleBuild.RestoreCount);
    }

    [Fact]
    public void List_Flag_Returns_Zero_Without_Running_Targets()
    {
        SimpleBuild.RestoreCount = 0;
        SimpleBuild.CompileCount = 0;
        var exit = TampBuild.Execute<SimpleBuild>(["--list"]);
        Assert.Equal(0, exit);
        Assert.Equal(0, SimpleBuild.RestoreCount);
        Assert.Equal(0, SimpleBuild.CompileCount);
    }

    [Theory]
    [InlineData("Compile")]
    [InlineData("Restore")]
    [InlineData("Default")]
    public void All_Top_Level_Targets_Resolve(string targetName)
    {
        SimpleBuild.RestoreCount = 0;
        SimpleBuild.CompileCount = 0;
        var exit = TampBuild.Execute<SimpleBuild>([targetName, "--plan"]);
        Assert.Equal(0, exit);
    }
}
