using Xunit;

namespace Tamp.Core.Tests;

public sealed class TampBuildTests
{
    private sealed class EmptyBuild : TampBuild { }

    private sealed class ThreeTargetBuild : TampBuild
    {
        public Target Restore => _ => _.Phase(Phase.Restore).Description("Restore packages");
        public Target Compile => _ => _.Phase(Phase.Build).DependsOn(nameof(Restore));
        public Target Test => _ => _.Phase(Phase.Test).DependsOn(nameof(Compile));
    }

    private sealed class PrivateTargetsBuild : TampBuild
    {
        // Private targets are still discovered. Useful for build-internal
        // helpers that consumers shouldn't invoke directly via `tamp <name>`
        // — visibility plus naming convention is enough at the CLI layer.
        private Target Setup => _ => _.Description("internal setup");
        public Target Build => _ => _.DependsOn(nameof(Setup));
    }

    private sealed class NullTargetBuild : TampBuild
    {
        public Target Broken => null!;
    }

    [Fact]
    public void Build_With_No_Targets_Yields_Empty_Map()
    {
        var targets = TampBuild.CollectTargets(new EmptyBuild());
        Assert.Empty(targets);
    }

    [Fact]
    public void All_Target_Properties_Are_Discovered()
    {
        var targets = TampBuild.CollectTargets(new ThreeTargetBuild());
        Assert.Equal(3, targets.Count);
        Assert.Contains("Restore", targets.Keys);
        Assert.Contains("Compile", targets.Keys);
        Assert.Contains("Test", targets.Keys);
    }

    [Fact]
    public void Targets_Are_Keyed_By_Property_Name()
    {
        var targets = TampBuild.CollectTargets(new ThreeTargetBuild());
        Assert.Equal("Restore", targets["Restore"].Name);
    }

    [Fact]
    public void Target_Phase_And_Dependencies_Materialize_Correctly()
    {
        var targets = TampBuild.CollectTargets(new ThreeTargetBuild());
        Assert.Equal(Phase.Build, targets["Compile"].Phase);
        Assert.Equal(["Restore"], targets["Compile"].Dependencies);
        Assert.Equal(["Compile"], targets["Test"].Dependencies);
    }

    [Fact]
    public void Description_Round_Trips_Through_Discovery()
    {
        var targets = TampBuild.CollectTargets(new ThreeTargetBuild());
        Assert.Equal("Restore packages", targets["Restore"].Description);
    }

    [Fact]
    public void Private_Targets_Are_Discovered_Like_Public_Ones()
    {
        var targets = TampBuild.CollectTargets(new PrivateTargetsBuild());
        Assert.Equal(2, targets.Count);
        Assert.Contains("Setup", targets.Keys);
        Assert.Contains("Build", targets.Keys);
    }

    [Fact]
    public void Null_Target_Property_Throws()
    {
        Assert.Throws<InvalidOperationException>(
            () => TampBuild.CollectTargets(new NullTargetBuild()));
    }

    [Fact]
    public void CollectTargets_Throws_On_Null_Build()
    {
        Assert.Throws<ArgumentNullException>(() => TampBuild.CollectTargets(null!));
    }
}
