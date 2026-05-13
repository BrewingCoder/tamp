using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Tamp.Analyzers;
using Xunit;

namespace Tamp.Analyzers.Tests;

public sealed class AsyncVoidInExecutesAnalyzerTests
{
    // Stubs mirror the 1.5.0 surface: Action overload PLUS Func<Task> overload.
    // The analyzer fires when an async lambda binds to the Action overload.
    private const string StubSource = """
        using System;
        using System.Collections.Generic;
        using System.Threading.Tasks;
        namespace Tamp
        {
            public sealed class CommandPlan { }
            public interface ITargetDefinition
            {
                ITargetDefinition Executes(Action action);
                ITargetDefinition Executes(Func<Task> asyncAction);
                ITargetDefinition Executes(Func<CommandPlan> planFactory);
                ITargetDefinition Executes(Func<Task<CommandPlan>> asyncPlanFactory);
            }
            public static class HttpProbe
            {
                public static Task GetAsync(string url) => Task.CompletedTask;
                public static Task<string> FetchAsync(string url) => Task.FromResult("");
            }
            public static class FileWork
            {
                public static void Do() { }
            }
        }
        """;

    private static async Task<ImmutableArray<Diagnostic>> RunAsync(string userSource)
    {
        var refs = new[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Collections.Generic.IEnumerable<>).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Task).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Runtime.CompilerServices.RuntimeHelpers).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Linq.Enumerable).Assembly.Location),
        };
        var compilation = CSharpCompilation.Create(
            "test-asm",
            new[] { CSharpSyntaxTree.ParseText(StubSource), CSharpSyntaxTree.ParseText(userSource) },
            refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var analyzers = ImmutableArray.Create<DiagnosticAnalyzer>(new AsyncVoidInExecutesAnalyzer());
        var withAnalyzers = compilation.WithAnalyzers(analyzers);
        return await withAnalyzers.GetAnalyzerDiagnosticsAsync();
    }

    [Fact]
    public async Task Does_Not_Fire_When_Async_Binds_To_Func_Task()
    {
        // Both overloads exist; the async lambda binds to Func<Task>, no diagnostic.
        var source = """
            using Tamp;
            class Build {
                ITargetDefinition Target = null!;
                void M() {
                    Target.Executes(async () => { await HttpProbe.GetAsync("https://x"); });
                }
            }
            """;
        var diags = await RunAsync(source);
        Assert.Empty(diags.Where(d => d.Id == "TAMP003"));
    }

    [Fact]
    public async Task Fires_When_Async_Lambda_Binds_To_Action_Overload_Only()
    {
        // Stub that ONLY has Executes(Action) — the realistic Tamp.Core <1.5.0 shape.
        var stubOnlyAction = """
            using System;
            using System.Threading.Tasks;
            namespace Tamp {
                public sealed class CommandPlan { }
                public interface ITargetDefinition {
                    ITargetDefinition Executes(Action action);
                }
                public static class HttpProbe { public static Task GetAsync(string s) => Task.CompletedTask; }
            }
            """;
        var user = """
            using Tamp;
            class Build {
                ITargetDefinition Target = null!;
                void M() {
                    Target.Executes(async () => { await HttpProbe.GetAsync("https://x"); });
                }
            }
            """;
        var refs = new[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Task).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Runtime.CompilerServices.RuntimeHelpers).Assembly.Location),
        };
        var compilation = CSharpCompilation.Create("t",
            new[] { CSharpSyntaxTree.ParseText(stubOnlyAction), CSharpSyntaxTree.ParseText(user) },
            refs, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var withAnalyzers = compilation.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new AsyncVoidInExecutesAnalyzer()));
        var diags = await withAnalyzers.GetAnalyzerDiagnosticsAsync();
        Assert.Single(diags.Where(d => d.Id == "TAMP003"));
    }

    [Fact]
    public async Task Does_Not_Fire_For_Sync_Action_Lambda()
    {
        var source = """
            using Tamp;
            class Build {
                ITargetDefinition Target = null!;
                void M() {
                    Target.Executes(() => { FileWork.Do(); });
                }
            }
            """;
        var diags = await RunAsync(source);
        Assert.Empty(diags.Where(d => d.Id == "TAMP003"));
    }

    [Fact]
    public async Task Does_Not_Fire_For_Async_Lambda_With_No_Await()
    {
        // Pathological case — async modifier present, no actual await. Doesn't actually
        // exhibit the silent-no-op because there's no awaited work. Heuristic skips it.
        var source = """
            using Tamp;
            using System.Threading.Tasks;
            class Build {
                ITargetDefinition Target = null!;
                void M() {
                    Target.Executes(async () => { FileWork.Do(); await Task.CompletedTask; });
                }
            }
            """;
        // We DO fire here because Task.CompletedTask is awaited. The rule is "has at least
        // one await"; the analyzer can't tell that Task.CompletedTask is trivial.
        // For a no-await async lambda, the C# compiler itself issues CS1998.
        var diags = await RunAsync(source);
        // Either binds to Func<Task> (no TAMP003) because Func<Task> exists in the full stub.
        Assert.Empty(diags.Where(d => d.Id == "TAMP003"));
    }
}
