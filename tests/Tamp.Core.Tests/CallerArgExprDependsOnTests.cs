using Xunit;

namespace Tamp.Core.Tests;

/// <summary>
/// Tests the `[CallerArgumentExpression]` overloads on target-name parameters
/// (DependsOn / After / Before / Triggers / TriggeredBy / OnFailureOf).
/// Goal: kill `nameof()` from build scripts. User writes `.DependsOn(Restore)`;
/// the C# compiler injects "Restore" as the captured-expression string.
/// </summary>
public sealed class CallerArgExprDependsOnTests
{
    // ---- The reference end-to-end test: target reference works at runtime ----

    private sealed class DependsOnByReferenceBuild : TampBuild
    {
        public static int RestoreCount;
        public static int CompileCount;

        public Target Restore => _ => _.Executes(() => RestoreCount++);
        public Target Compile => _ => _.DependsOn(Restore).Executes(() => CompileCount++);
    }

    [Fact]
    public void DependsOn_Target_Reference_Runs_Dependency_First()
    {
        DependsOnByReferenceBuild.RestoreCount = 0;
        DependsOnByReferenceBuild.CompileCount = 0;
        var exit = TampBuild.Execute<DependsOnByReferenceBuild>(["Compile"]);
        Assert.Equal(0, exit);
        Assert.Equal(1, DependsOnByReferenceBuild.RestoreCount);
        Assert.Equal(1, DependsOnByReferenceBuild.CompileCount);
    }

    [Fact]
    public void DependsOn_Target_Reference_Captures_Name_On_Spec()
    {
        var targets = TampBuild.CollectTargets(new DependsOnByReferenceBuild());
        Assert.Contains("Restore", targets["Compile"].Dependencies);
    }

    // ---- Each overload threads through correctly ----

    [Fact]
    public void After_Target_Reference_Captures_Name()
    {
        var def = new TargetDefinition();
        // We test the implementation directly here — the C# compiler captures
        // "Restore" as the expression string at the call site.
        IDummy d = new Dummy();
        d.After(_ => _);                            // dummy invocation
        var marker = "Restore";
        def.After(_ => _, marker);                  // simulating the captured-string injection
        var spec = def.Build("X");
        Assert.Contains("Restore", spec.OrderAfter);
    }

    [Fact]
    public void Before_Target_Reference_Captures_Name()
    {
        var def = new TargetDefinition();
        def.Before(_ => _, "Compile");
        var spec = def.Build("X");
        Assert.Contains("Compile", spec.OrderBefore);
    }

    [Fact]
    public void Triggers_Target_Reference_Captures_Name()
    {
        var def = new TargetDefinition();
        def.Triggers(_ => _, "Notify");
        var spec = def.Build("X");
        Assert.Contains("Notify", spec.Triggers);
    }

    [Fact]
    public void TriggeredBy_Target_Reference_Captures_Name()
    {
        var def = new TargetDefinition();
        def.TriggeredBy(_ => _, "Compile");
        var spec = def.Build("X");
        Assert.Contains("Compile", spec.TriggeredBy);
    }

    [Fact]
    public void OnFailureOf_Target_Reference_Captures_Name()
    {
        var def = new TargetDefinition();
        def.OnFailureOf(_ => _, "Deploy");
        var spec = def.Build("X");
        Assert.Contains("Deploy", spec.OnFailureOf);
    }

    // ---- Validator ----

    [Fact]
    public void Captured_Expression_Strips_this_Prefix()
    {
        var def = new TargetDefinition();
        def.DependsOn(_ => _, "this.Restore");
        var spec = def.Build("X");
        Assert.Contains("Restore", spec.Dependencies);
        Assert.DoesNotContain("this.Restore", spec.Dependencies);
    }

    [Fact]
    public void Captured_Expression_Trimmed()
    {
        var def = new TargetDefinition();
        def.DependsOn(_ => _, "  Restore  ");
        var spec = def.Build("X");
        Assert.Contains("Restore", spec.Dependencies);
    }

    [Fact]
    public void Complex_Expression_Throws_With_Helpful_Message()
    {
        var def = new TargetDefinition();
        var ex = Assert.Throws<ArgumentException>(
            () => def.DependsOn(_ => _, "Restore ?? Compile"));
        Assert.Contains("DependsOn", ex.Message);
        Assert.Contains("simple target reference", ex.Message);
        Assert.Contains("'Restore ?? Compile'", ex.Message);  // shows the actual expression in the error
    }

    [Fact]
    public void Method_Call_Expression_Throws()
    {
        var def = new TargetDefinition();
        Assert.Throws<ArgumentException>(
            () => def.DependsOn(_ => _, "GetTarget(\"Pack\")"));
    }

    [Fact]
    public void Empty_Captured_Expression_Throws()
    {
        var def = new TargetDefinition();
        Assert.Throws<ArgumentException>(() => def.DependsOn(_ => _, ""));
        Assert.Throws<ArgumentException>(() => def.DependsOn(_ => _, "   "));
        Assert.Throws<ArgumentException>(() => def.DependsOn(_ => _, (string?)null));
    }

    // ---- Normalize helper directly ----

    [Fact]
    public void Normalize_Helper_Strips_this_Prefix()
    {
        var result = TargetDefinition.NormalizeCapturedTargetName("this.Restore", "DependsOn");
        Assert.Equal("Restore", result);
    }

    [Fact]
    public void Normalize_Helper_Passes_Simple_Identifiers()
    {
        Assert.Equal("Restore", TargetDefinition.NormalizeCapturedTargetName("Restore", "DependsOn"));
        Assert.Equal("_Foo", TargetDefinition.NormalizeCapturedTargetName("_Foo", "DependsOn"));
        Assert.Equal("Target123", TargetDefinition.NormalizeCapturedTargetName("Target123", "DependsOn"));
    }

    [Fact]
    public void Normalize_Helper_Rejects_Whitespace_In_Name()
    {
        Assert.Throws<ArgumentException>(() => TargetDefinition.NormalizeCapturedTargetName("a b", "DependsOn"));
    }

    [Fact]
    public void Normalize_Helper_Rejects_Special_Chars()
    {
        Assert.Throws<ArgumentException>(() => TargetDefinition.NormalizeCapturedTargetName("Restore!", "DependsOn"));
        Assert.Throws<ArgumentException>(() => TargetDefinition.NormalizeCapturedTargetName("Restore-Foo", "DependsOn"));
    }

    // ---- Back-compat: string overload still works ----

    [Fact]
    public void String_Overload_Still_Works_For_Back_Compat()
    {
        var def = new TargetDefinition();
        def.DependsOn("Restore", "Compile");
        var spec = def.Build("X");
        Assert.Contains("Restore", spec.Dependencies);
        Assert.Contains("Compile", spec.Dependencies);
    }

    [Fact]
    public void String_Overload_Accepts_nameof_Output()
    {
        // nameof(Restore) is still the documented back-compat path.
        var def = new TargetDefinition();
        def.DependsOn(nameof(DependsOnByReferenceBuild.Restore));
        var spec = def.Build("X");
        Assert.Contains("Restore", spec.Dependencies);
    }

    // ---- Chaining works for multi-target dependencies ----

    private sealed class MultiDependsOnBuild : TampBuild
    {
        public Target Restore => _ => _.Executes(() => { });
        public Target Lint => _ => _.Executes(() => { });
        public Target Compile => _ => _.DependsOn(Restore).DependsOn(Lint).Executes(() => { });
    }

    [Fact]
    public void Chained_DependsOn_Accumulates_All_References()
    {
        var targets = TampBuild.CollectTargets(new MultiDependsOnBuild());
        var compile = targets["Compile"];
        Assert.Contains("Restore", compile.Dependencies);
        Assert.Contains("Lint", compile.Dependencies);
        Assert.Equal(2, compile.Dependencies.Count);
    }

    // ---- Mixed: string-overload + Target-overload in the same chain ----

    private sealed class MixedDependsOnBuild : TampBuild
    {
        public Target Restore => _ => _.Executes(() => { });
        public Target Lint => _ => _.Executes(() => { });
        public Target Compile => _ => _
            .DependsOn(Restore)                          // captured-expression overload
            .DependsOn(nameof(Lint))                     // string overload
            .Executes(() => { });
    }

    [Fact]
    public void Mixed_Overloads_Cooperate_In_Single_Chain()
    {
        var targets = TampBuild.CollectTargets(new MixedDependsOnBuild());
        Assert.Contains("Restore", targets["Compile"].Dependencies);
        Assert.Contains("Lint", targets["Compile"].Dependencies);
    }

    // Helper interface to make the test compile (we just need a dummy invocation site).
    private interface IDummy { void After(Target t); }
    private sealed class Dummy : IDummy { public void After(Target t) { } }

    // ---- params Target[] overloads (TAM-162, friction #14, 1.3.0+) ----

    private sealed class VarargsDependsOnBuild : TampBuild
    {
        public Target Restore => _ => _.Executes(() => { });
        public Target Lint => _ => _.Executes(() => { });
        public Target Format => _ => _.Executes(() => { });
        public Target Compile => _ => _
            .DependsOn(Restore, Lint, Format)
            .Executes(() => { });
    }

    [Fact]
    public void DependsOn_Varargs_Captures_All_Names()
    {
        var targets = TampBuild.CollectTargets(new VarargsDependsOnBuild());
        var compile = targets["Compile"];
        Assert.Equal(new[] { "Restore", "Lint", "Format" }, compile.Dependencies);
    }

    [Fact]
    public void DependsOn_Varargs_Runs_All_Deps_First()
    {
        RecordingVarargsBuild.Hits.Clear();
        var exit = TampBuild.Execute<RecordingVarargsBuild>(["Compile"]);
        Assert.Equal(0, exit);
        Assert.Equal(new[] { "Restore", "Lint", "Format", "Compile" }, RecordingVarargsBuild.Hits);
    }

    private sealed class RecordingVarargsBuild : TampBuild
    {
        public static readonly List<string> Hits = [];
        public Target Restore => _ => _.Executes(() => Hits.Add("Restore"));
        public Target Lint => _ => _.Executes(() => Hits.Add("Lint"));
        public Target Format => _ => _.Executes(() => Hits.Add("Format"));
        public Target Compile => _ => _
            .DependsOn(Restore, Lint, Format)
            .Executes(() => Hits.Add("Compile"));
    }

    private sealed class VarargsAfterBuild : TampBuild
    {
        public Target A => _ => _.Executes(() => { });
        public Target B => _ => _.Executes(() => { });
        public Target Late => _ => _.After(A, B).Executes(() => { });
    }

    [Fact]
    public void After_Varargs_Captures_All_Names()
    {
        var targets = TampBuild.CollectTargets(new VarargsAfterBuild());
        Assert.Equal(new[] { "A", "B" }, targets["Late"].OrderAfter);
    }

    private sealed class VarargsBeforeBuild : TampBuild
    {
        public Target A => _ => _.Executes(() => { });
        public Target B => _ => _.Executes(() => { });
        public Target Early => _ => _.Before(A, B).Executes(() => { });
    }

    [Fact]
    public void Before_Varargs_Captures_All_Names()
    {
        var targets = TampBuild.CollectTargets(new VarargsBeforeBuild());
        Assert.Equal(new[] { "A", "B" }, targets["Early"].OrderBefore);
    }

    private sealed class VarargsTriggersBuild : TampBuild
    {
        public Target A => _ => _.Executes(() => { });
        public Target B => _ => _.Executes(() => { });
        public Target Pivot => _ => _.Triggers(A, B).Executes(() => { });
    }

    [Fact]
    public void Triggers_Varargs_Captures_All_Names()
    {
        var targets = TampBuild.CollectTargets(new VarargsTriggersBuild());
        Assert.Equal(new[] { "A", "B" }, targets["Pivot"].Triggers);
    }

    private sealed class VarargsTriggeredByBuild : TampBuild
    {
        public Target A => _ => _.Executes(() => { });
        public Target B => _ => _.Executes(() => { });
        public Target Catchup => _ => _.TriggeredBy(A, B).Executes(() => { });
    }

    [Fact]
    public void TriggeredBy_Varargs_Captures_All_Names()
    {
        var targets = TampBuild.CollectTargets(new VarargsTriggeredByBuild());
        Assert.Equal(new[] { "A", "B" }, targets["Catchup"].TriggeredBy);
    }

    private sealed class VarargsOnFailureOfBuild : TampBuild
    {
        public Target A => _ => _.Executes(() => { });
        public Target B => _ => _.Executes(() => { });
        public Target Rescue => _ => _.OnFailureOf(A, B).Executes(() => { });
    }

    [Fact]
    public void OnFailureOf_Varargs_Captures_All_Names()
    {
        var targets = TampBuild.CollectTargets(new VarargsOnFailureOfBuild());
        Assert.Equal(new[] { "A", "B" }, targets["Rescue"].OnFailureOf);
    }

    [Fact]
    public void Varargs_DependsOn_4Plus_Args_All_Captured()
    {
        var build = new HoldFastShapeBuild();
        var targets = TampBuild.CollectTargets(build);
        var ci = targets["Ci"];
        // Mirrors HoldFast's friction #14 ask: Ci.DependsOn(Test, Publish, FrontendBuild, DockerBuildBackend).
        Assert.Equal(new[] { "Test", "Publish", "FrontendBuild", "DockerBuildBackend" }, ci.Dependencies);
    }

    private sealed class HoldFastShapeBuild : TampBuild
    {
        public Target Test => _ => _.Executes(() => { });
        public Target Publish => _ => _.Executes(() => { });
        public Target FrontendBuild => _ => _.Executes(() => { });
        public Target DockerBuildBackend => _ => _.Executes(() => { });
        public Target Ci => _ => _
            .Default()
            .DependsOn(Test, Publish, FrontendBuild, DockerBuildBackend);
    }

    [Fact]
    public void Varargs_DependsOn_Unmapped_Delegate_Throws_Helpful_Message()
    {
        // Direct construction of TargetDefinition without a method map — the
        // params Target[] path requires one, so an unmapped call must throw
        // with guidance.
        var def = new TargetDefinition();
        var ex = Assert.Throws<InvalidOperationException>(() =>
            def.DependsOn(_ => _, _ => _));
        Assert.Contains("no target-method map", ex.Message);
        Assert.Contains("string overload", ex.Message);
    }

    [Fact]
    public void Varargs_DependsOn_Null_Target_Throws_ArgumentNullException()
    {
        var build = new HoldFastShapeBuild();
        // Build the method map so the resolve path is engaged.
        var methodMap = new Dictionary<System.Reflection.MethodInfo, string>
        {
            [((Target)(_ => _)).Method] = "Anonymous"
        };
        var def = new TargetDefinition(methodMap);
        Assert.Throws<ArgumentNullException>(() =>
            def.DependsOn(null!, _ => _));
    }
}
