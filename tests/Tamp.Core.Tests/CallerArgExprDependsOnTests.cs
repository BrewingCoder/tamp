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
        Assert.Throws<ArgumentException>(() => def.DependsOn(_ => _, null));
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
}
