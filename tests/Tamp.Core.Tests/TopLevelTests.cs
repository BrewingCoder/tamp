using Xunit;

namespace Tamp.Core.Tests;

public sealed class TopLevelTests
{
    private sealed class TopLevelOnlyBuild : TampBuild
    {
        public Target Ci => _ => _.TopLevel().DependsOn(nameof(Compile));
        public Target Pack => _ => _.TopLevel().DependsOn(nameof(Compile));
        public Target Compile => _ => _.DependsOn(nameof(Restore));
        public Target Restore => _ => _;
    }

    private sealed class NoTopLevelBuild : TampBuild
    {
        public Target Build => _ => _;
        public Target Test => _ => _.DependsOn(nameof(Build));
    }

    [Fact]
    public void TopLevel_Marker_Is_Captured_On_Spec()
    {
        var targets = TampBuild.CollectTargets(new TopLevelOnlyBuild());
        Assert.True(targets["Ci"].TopLevel);
        Assert.True(targets["Pack"].TopLevel);
        Assert.False(targets["Compile"].TopLevel);
        Assert.False(targets["Restore"].TopLevel);
    }

    [Fact]
    public void Default_Spec_Has_TopLevel_False()
    {
        var spec = new TargetSpec { Name = "X" };
        Assert.False(spec.TopLevel);
    }

    [Fact]
    public void Unmarked_Targets_Are_Still_Invokable_By_Name()
    {
        // The marker is soft visibility, not hard restriction. Calling
        // an internal target by name still runs it.
        InvokeInternalBuild.RestoreCount = 0;
        var exit = TampBuild.Execute<InvokeInternalBuild>(["Restore"]);
        Assert.Equal(0, exit);
        Assert.Equal(1, InvokeInternalBuild.RestoreCount);
    }

    private sealed class InvokeInternalBuild : TampBuild
    {
        public static int RestoreCount;
        public Target Ci => _ => _.TopLevel().DependsOn(nameof(Restore));
        public Target Restore => _ => _.Executes(() => RestoreCount++);
    }

    [Fact]
    public void TopLevel_Returns_Same_Definition_For_Chaining()
    {
        var def = new TargetDefinition();
        var result = def.TopLevel();
        Assert.Same(def, result);
    }

    [Fact]
    public void Multiple_TopLevel_Calls_Are_Idempotent()
    {
        var def = new TargetDefinition();
        def.TopLevel().TopLevel().TopLevel();
        var spec = def.Build("X");
        Assert.True(spec.TopLevel);
    }
}
