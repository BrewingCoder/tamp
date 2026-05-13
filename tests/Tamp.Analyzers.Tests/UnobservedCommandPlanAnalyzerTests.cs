using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Tamp.Analyzers;
using Xunit;

namespace Tamp.Analyzers.Tests;

/// <summary>
/// Smoke tests for the TAMP001 analyzer. We compile a small synthetic program that
/// references stubs for the Tamp types and assert which lines fire the diagnostic.
/// </summary>
public sealed class UnobservedCommandPlanAnalyzerTests
{
    // Stub source that mirrors Tamp.Core's surface enough to compile the test scripts below.
    private const string StubSource = """
        using System;
        using System.Collections.Generic;
        namespace Tamp
        {
            public sealed class CommandPlan { }
            public sealed class Tool { }
            public interface ITargetDefinition
            {
                ITargetDefinition Executes(Action action);
                ITargetDefinition Executes(Func<CommandPlan> planFactory);
                ITargetDefinition Executes(Func<IEnumerable<CommandPlan>> planFactory);
            }
            public static class DotNet
            {
                public static CommandPlan Restore() => new();
                public static CommandPlan Build() => new();
                public static CommandPlan Test() => new();
            }
            public static class Probe
            {
                public static void DoWork() { }   // void-returning sibling — must NOT fire TAMP001
            }
        }
        """;

    private static async Task<ImmutableArray<Diagnostic>> RunAsync(string userSource)
    {
        var refs = new[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Collections.Generic.IEnumerable<>).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Runtime.CompilerServices.RuntimeHelpers).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Linq.Enumerable).Assembly.Location),
        };
        var compilation = CSharpCompilation.Create(
            "test-asm",
            new[] { CSharpSyntaxTree.ParseText(StubSource), CSharpSyntaxTree.ParseText(userSource) },
            refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var analyzers = ImmutableArray.Create<DiagnosticAnalyzer>(new UnobservedCommandPlanAnalyzer());
        var withAnalyzers = compilation.WithAnalyzers(analyzers);
        var diagnostics = await withAnalyzers.GetAnalyzerDiagnosticsAsync();
        return diagnostics;
    }

    [Fact]
    public async Task Flags_Unobserved_CommandPlan_In_Executes_Action_Lambda()
    {
        var source = """
            using Tamp;
            class Build
            {
                ITargetDefinition Target = null!;
                void M()
                {
                    Target.Executes(() => { DotNet.Restore(); DotNet.Build(); });
                }
            }
            """;
        var diags = await RunAsync(source);
        var tampDiags = diags.Where(d => d.Id == "TAMP001").ToArray();
        Assert.Equal(2, tampDiags.Length);
        Assert.Contains(tampDiags, d => d.GetMessage().Contains("Restore"));
        Assert.Contains(tampDiags, d => d.GetMessage().Contains("Build"));
    }

    [Fact]
    public async Task Does_Not_Flag_When_Returning_Array_Of_Plans()
    {
        var source = """
            using Tamp;
            using System.Collections.Generic;
            class Build
            {
                ITargetDefinition Target = null!;
                void M()
                {
                    Target.Executes(() => new[] { DotNet.Restore(), DotNet.Build() });
                }
            }
            """;
        var diags = await RunAsync(source);
        Assert.Empty(diags.Where(d => d.Id == "TAMP001"));
    }

    [Fact]
    public async Task Does_Not_Flag_Single_Plan_Lambda()
    {
        var source = """
            using Tamp;
            class Build
            {
                ITargetDefinition Target = null!;
                void M()
                {
                    Target.Executes(() => DotNet.Build());
                }
            }
            """;
        var diags = await RunAsync(source);
        Assert.Empty(diags.Where(d => d.Id == "TAMP001"));
    }

    [Fact]
    public async Task Does_Not_Flag_Void_Returning_Calls_In_Executes_Action()
    {
        // void-returning calls inside Executes(Action) are intentional — file I/O, logging, etc.
        var source = """
            using Tamp;
            class Build
            {
                ITargetDefinition Target = null!;
                void M()
                {
                    Target.Executes(() => { Probe.DoWork(); });
                }
            }
            """;
        var diags = await RunAsync(source);
        Assert.Empty(diags.Where(d => d.Id == "TAMP001"));
    }

    [Fact]
    public async Task Does_Not_Flag_When_Plan_Is_Assigned_To_Variable()
    {
        // Assignment is not a statement-expression invocation — caller may use the variable.
        var source = """
            using Tamp;
            class Build
            {
                ITargetDefinition Target = null!;
                void M()
                {
                    Target.Executes(() => { var p = DotNet.Build(); });
                }
            }
            """;
        var diags = await RunAsync(source);
        // Note: an unused variable is a different problem (CS0219 from the C# compiler).
        // TAMP001 is specifically about unobserved CommandPlan return values, not unused locals.
        Assert.Empty(diags.Where(d => d.Id == "TAMP001"));
    }

    [Fact]
    public async Task Does_Not_Flag_Outside_Of_Executes_Call()
    {
        // CommandPlan returned + discarded outside an Executes context — not our problem.
        var source = """
            using Tamp;
            class Build
            {
                void M()
                {
                    DotNet.Restore();
                }
            }
            """;
        var diags = await RunAsync(source);
        Assert.Empty(diags.Where(d => d.Id == "TAMP001"));
    }

    [Fact]
    public async Task Flags_Mixed_Block_With_Some_Void_Some_Plan()
    {
        // The void call (DoWork) is fine; the plan call (Build) is not.
        var source = """
            using Tamp;
            class Build
            {
                ITargetDefinition Target = null!;
                void M()
                {
                    Target.Executes(() => { Probe.DoWork(); DotNet.Build(); });
                }
            }
            """;
        var diags = await RunAsync(source);
        var tamp = diags.Where(d => d.Id == "TAMP001").ToArray();
        var diag = Assert.Single(tamp);
        Assert.Contains("Build", diag.GetMessage());
    }
}
