using Xunit;

namespace Tamp.Core.Tests;

/// <summary>
/// Tests for the <c>.Internal()</c> target decorator (Tamp.Core 1.1.0+):
/// targets are listable + callable by default; <c>.Internal()</c> opts out
/// of both — hidden from <c>--list</c> AND non-callable from CLI.
/// </summary>
[Collection(nameof(ConsoleCaptureCollection))]
public sealed class InternalDecoratorTests
{
    // ---- Spec round-trip ----

    [Fact]
    public void IsInternal_Defaults_To_False()
    {
        var def = new TargetDefinition();
        var spec = def.Build("X");
        Assert.False(spec.IsInternal);
    }

    [Fact]
    public void Internal_Method_Sets_IsInternal_True_On_Spec()
    {
        var def = new TargetDefinition();
        def.Internal();
        var spec = def.Build("X");
        Assert.True(spec.IsInternal);
    }

    [Fact]
    public void Internal_Returns_Same_Definition_For_Chaining()
    {
        var def = new TargetDefinition();
        var result = def.Internal();
        Assert.Same(def, result);
    }

    // ---- Direct CLI invocation refused ----

    private sealed class InternalTargetBuild : TampBuild
    {
        public static int RestoreCount;
        public static int CompileCount;

        public Target Restore => _ => _.Internal().Executes(() => RestoreCount++);
        public Target Compile => _ => _.DependsOn(nameof(Restore)).Executes(() => CompileCount++);
    }

    [Fact]
    public void Internal_Target_Is_Not_Directly_Callable()
    {
        var stderr = new StringWriter();
        var origErr = Console.Error;
        Console.SetError(stderr);
        try
        {
            InternalTargetBuild.RestoreCount = 0;
            InternalTargetBuild.CompileCount = 0;
            var exit = TampBuild.Execute<InternalTargetBuild>(["Restore"]);
            Assert.Equal(2, exit);
            Assert.Equal(0, InternalTargetBuild.RestoreCount);  // never ran
            var output = stderr.ToString();
            Assert.Contains("Target 'Restore' is internal", output);
            Assert.Contains("not directly callable", output);
            Assert.Contains("Compile", output);  // names the dependent
        }
        finally { Console.SetError(origErr); }
    }

    [Fact]
    public void Internal_Target_Still_Runs_As_Dependency()
    {
        InternalTargetBuild.RestoreCount = 0;
        InternalTargetBuild.CompileCount = 0;
        var exit = TampBuild.Execute<InternalTargetBuild>(["Compile"]);
        Assert.Equal(0, exit);
        Assert.Equal(1, InternalTargetBuild.RestoreCount);  // pulled in via DependsOn
        Assert.Equal(1, InternalTargetBuild.CompileCount);
    }

    // ---- Mutual exclusion with .Default() ----

    private sealed class InternalAndDefaultBuild : TampBuild
    {
        public Target Restore => _ => _.Internal().Default().Executes(() => { });
        public Target Compile => _ => _.DependsOn(nameof(Restore)).Executes(() => { });
    }

    [Fact]
    public void Internal_And_Default_On_Same_Target_Throws_At_Startup()
    {
        var stderr = new StringWriter();
        var origErr = Console.Error;
        Console.SetError(stderr);
        try
        {
            var exit = TampBuild.Execute<InternalAndDefaultBuild>([]);
            Assert.Equal(2, exit);
            var output = stderr.ToString();
            Assert.Contains("Restore", output);
            Assert.Contains("Internal", output);
            Assert.Contains("Default", output);
        }
        finally { Console.SetError(origErr); }
    }

    // ---- Stranded-internal warning ----

    private sealed class StrandedInternalBuild : TampBuild
    {
        public Target Restore => _ => _.Internal().Executes(() => { });
        public Target Compile => _ => _.Executes(() => { });   // does NOT depend on Restore
    }

    [Fact]
    public void Stranded_Internal_Emits_Warning_But_Build_Continues()
    {
        var stderr = new StringWriter();
        var origErr = Console.Error;
        Console.SetError(stderr);
        try
        {
            var exit = TampBuild.Execute<StrandedInternalBuild>(["Compile"]);
            Assert.Equal(0, exit);  // build still succeeds
            var output = stderr.ToString();
            Assert.Contains("warning", output);
            Assert.Contains("Restore", output);
            Assert.Contains("never run", output);
        }
        finally { Console.SetError(origErr); }
    }

    // ---- --list behavior ----

    private sealed class ListTestBuild : TampBuild
    {
        public Target Compile => _ => _.Executes(() => { });
        public Target Test => _ => _.DependsOn(nameof(Compile)).Executes(() => { });
        public Target _Helper => _ => _.Internal().Executes(() => { });
    }

    [Fact]
    public void List_Hides_Internal_Targets_By_Default()
    {
        var stdout = new StringWriter();
        var origOut = Console.Out;
        Console.SetOut(stdout);
        try
        {
            var exit = TampBuild.Execute<ListTestBuild>(["--list"]);
            Assert.Equal(0, exit);
            var output = stdout.ToString();
            Assert.Contains("Compile", output);
            Assert.Contains("Test", output);
            Assert.DoesNotContain("_Helper", output);
            Assert.Contains("1 internal target", output);  // the trailing hint
        }
        finally { Console.SetOut(origOut); }
    }

    [Fact]
    public void List_All_Reveals_Internal_Targets()
    {
        var stdout = new StringWriter();
        var origOut = Console.Out;
        Console.SetOut(stdout);
        try
        {
            var exit = TampBuild.Execute<ListTestBuild>(["--list", "--all"]);
            Assert.Equal(0, exit);
            var output = stdout.ToString();
            Assert.Contains("_Helper", output);
            Assert.Contains("(internal)", output);  // the marker
        }
        finally { Console.SetOut(origOut); }
    }

    // ---- TopLevel deprecation still compiles + is a no-op ----

    private sealed class LegacyTopLevelBuild : TampBuild
    {
        public static int RanCount;
#pragma warning disable CS0618 // Type or member is obsolete
        public Target Compile => _ => _.TopLevel().Executes(() => RanCount++);
#pragma warning restore CS0618
    }

    [Fact]
    public void Deprecated_TopLevel_Call_Compiles_And_Runs_As_No_Op()
    {
        LegacyTopLevelBuild.RanCount = 0;
        var exit = TampBuild.Execute<LegacyTopLevelBuild>(["Compile"]);
        Assert.Equal(0, exit);
        Assert.Equal(1, LegacyTopLevelBuild.RanCount);
    }
}
