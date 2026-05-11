using Xunit;

namespace Tamp.Core.Tests;

/// <summary>
/// Tests for the <c>.Default()</c> target decorator: any target can opt in as the
/// default invocation target regardless of its name. Single-default invariant
/// enforced at startup, file-layout-independent (works across partial classes).
/// </summary>
[Collection(nameof(ConsoleCaptureCollection))]
public sealed class DefaultDecoratorTests
{
    // ---- Marked .Default() target wins ----

    private sealed class MarkedDefaultBuild : TampBuild
    {
        public static int RestoreCount;
        public static int CompileCount;

        public Target Restore => _ => _.Executes(() => RestoreCount++);
        public Target Compile => _ => _.Default().DependsOn(nameof(Restore)).Executes(() => CompileCount++);
    }

    [Fact]
    public void Marked_Default_Runs_When_No_Argument_Given()
    {
        MarkedDefaultBuild.RestoreCount = 0;
        MarkedDefaultBuild.CompileCount = 0;
        var exit = TampBuild.Execute<MarkedDefaultBuild>([]);
        Assert.Equal(0, exit);
        Assert.Equal(1, MarkedDefaultBuild.RestoreCount);
        Assert.Equal(1, MarkedDefaultBuild.CompileCount);
    }

    [Fact]
    public void Explicit_Target_Argument_Still_Wins_Over_Marked_Default()
    {
        MarkedDefaultBuild.RestoreCount = 0;
        MarkedDefaultBuild.CompileCount = 0;
        var exit = TampBuild.Execute<MarkedDefaultBuild>(["Restore"]);
        Assert.Equal(0, exit);
        Assert.Equal(1, MarkedDefaultBuild.RestoreCount);
        Assert.Equal(0, MarkedDefaultBuild.CompileCount);
    }

    // ---- Marked .Default() supersedes name-based fallback ----

    private sealed class MarkedDefaultBeatsNamed : TampBuild
    {
        public static int ChosenCount;
        public static int LegacyCount;

        public Target Compile => _ => _.Default().Executes(() => ChosenCount++);
        public Target Default => _ => _.Executes(() => LegacyCount++);
    }

    [Fact]
    public void Marked_Default_Supersedes_Target_Named_Default()
    {
        MarkedDefaultBeatsNamed.ChosenCount = 0;
        MarkedDefaultBeatsNamed.LegacyCount = 0;
        var exit = TampBuild.Execute<MarkedDefaultBeatsNamed>([]);
        Assert.Equal(0, exit);
        Assert.Equal(1, MarkedDefaultBeatsNamed.ChosenCount);
        Assert.Equal(0, MarkedDefaultBeatsNamed.LegacyCount);
    }

    // ---- Name-based fallback still works when nothing is marked ----

    private sealed class LegacyDefaultNameBuild : TampBuild
    {
        public static int RanCount;
        public Target Default => _ => _.Executes(() => RanCount++);
    }

    [Fact]
    public void Target_Named_Default_Still_Works_When_Nothing_Is_Marked()
    {
        LegacyDefaultNameBuild.RanCount = 0;
        var exit = TampBuild.Execute<LegacyDefaultNameBuild>([]);
        Assert.Equal(0, exit);
        Assert.Equal(1, LegacyDefaultNameBuild.RanCount);
    }

    private sealed class LegacyCiNameBuild : TampBuild
    {
        public static int RanCount;
        public Target Ci => _ => _.Executes(() => RanCount++);
    }

    [Fact]
    public void Target_Named_Ci_Still_Works_When_Nothing_Is_Marked_And_No_Default()
    {
        LegacyCiNameBuild.RanCount = 0;
        var exit = TampBuild.Execute<LegacyCiNameBuild>([]);
        Assert.Equal(0, exit);
        Assert.Equal(1, LegacyCiNameBuild.RanCount);
    }

    // ---- No default at all → friendly error ----

    private sealed class NoDefaultBuild : TampBuild
    {
        public Target Restore => _ => _.Executes(() => { });
        public Target Compile => _ => _.Executes(() => { });
    }

    [Fact]
    public void No_Default_And_No_Target_Argument_Returns_Exit_2_With_Friendly_Error()
    {
        var stderr = new StringWriter();
        var origErr = Console.Error;
        Console.SetError(stderr);
        try
        {
            var exit = TampBuild.Execute<NoDefaultBuild>([]);
            Assert.Equal(2, exit);
            var output = stderr.ToString();
            Assert.Contains(".Default()", output);
            Assert.Contains("--list", output);
        }
        finally { Console.SetError(origErr); }
    }

    // ---- Single-default invariant ----

    private sealed class TwoMarkedDefaultsBuild : TampBuild
    {
        public Target Compile => _ => _.Default().Executes(() => { });
        public Target Pack => _ => _.Default().Executes(() => { });
    }

    [Fact]
    public void Two_Marked_Defaults_Returns_Exit_2_With_Both_Names_In_Error()
    {
        var stderr = new StringWriter();
        var origErr = Console.Error;
        Console.SetError(stderr);
        try
        {
            var exit = TampBuild.Execute<TwoMarkedDefaultsBuild>([]);
            Assert.Equal(2, exit);
            var output = stderr.ToString();
            Assert.Contains("Multiple targets are marked `.Default()`", output);
            Assert.Contains("Compile", output);
            Assert.Contains("Pack", output);
        }
        finally { Console.SetError(origErr); }
    }

    // ---- Partial-class invariant: defaults across files still get caught ----

    private sealed partial class PartialBuild : TampBuild
    {
        public Target Compile => _ => _.Default().Executes(() => { });
    }

    private sealed partial class PartialBuild
    {
        public Target Pack => _ => _.Default().Executes(() => { });
    }

    [Fact]
    public void Two_Defaults_Spread_Across_Partial_Class_Files_Still_Detected()
    {
        // Reflection sees all Target properties on the type regardless of which source file
        // declared them. The uniqueness check is naturally file-layout-independent.
        var stderr = new StringWriter();
        var origErr = Console.Error;
        Console.SetError(stderr);
        try
        {
            var exit = TampBuild.Execute<PartialBuild>([]);
            Assert.Equal(2, exit);
            var output = stderr.ToString();
            Assert.Contains("Compile", output);
            Assert.Contains("Pack", output);
        }
        finally { Console.SetError(origErr); }
    }

    // ---- IsDefault flag is round-tripped to the spec ----

    [Fact]
    public void IsDefault_Flag_Is_Round_Tripped_To_Spec()
    {
        var def = new TargetDefinition();
        def.Default();
        var spec = def.Build("X");
        Assert.True(spec.IsDefault);
    }

    [Fact]
    public void IsDefault_Defaults_To_False_When_Not_Marked()
    {
        var def = new TargetDefinition();
        var spec = def.Build("X");
        Assert.False(spec.IsDefault);
    }

    [Fact]
    public void Default_Method_Is_Chainable_With_Other_Decorators()
    {
        // Smoke test on the fluent surface — Default() composes naturally with
        // DependsOn, Description, Tag, etc.
        var def = new TargetDefinition();
        var result = def.Default().DependsOn("Foo").Description("desc").Tag("fast");
        Assert.NotNull(result);
        var spec = ((TargetDefinition)result).Build("X");
        Assert.True(spec.IsDefault);
        Assert.Contains("Foo", spec.Dependencies);
        Assert.Equal("desc", spec.Description);
        Assert.Contains("fast", spec.Tags);
    }
}
