using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Tamp.Analyzers;
using Xunit;

namespace Tamp.Analyzers.Tests;

public sealed class MissingTampEntryPointAnalyzerTests
{
    // TampBuild stub — adopter classes derive from it.
    private const string TampBuildStub = """
        namespace Tamp {
            public abstract class TampBuild
            {
                public static int Execute<T>(string[] args) where T : TampBuild => 0;
            }
        }
        """;

    private static async Task<ImmutableArray<Diagnostic>> RunAsync(string userSource)
    {
        var refs = new[] {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Runtime.CompilerServices.RuntimeHelpers).Assembly.Location),
        };
        var compilation = CSharpCompilation.Create("test-asm",
            new[] { CSharpSyntaxTree.ParseText(TampBuildStub), CSharpSyntaxTree.ParseText(userSource) },
            refs,
            new CSharpCompilationOptions(OutputKind.ConsoleApplication));
        var analyzers = ImmutableArray.Create<DiagnosticAnalyzer>(new MissingTampEntryPointAnalyzer());
        var withAnalyzers = compilation.WithAnalyzers(analyzers);
        return await withAnalyzers.GetAnalyzerDiagnosticsAsync();
    }

    [Fact]
    public async Task Does_Not_Fire_When_Main_Calls_Execute()
    {
        var source = """
            using Tamp;
            class Build : TampBuild
            {
                public static int Main(string[] args) => Execute<Build>(args);
            }
            """;
        var diags = await RunAsync(source);
        Assert.Empty(diags.Where(d => d.Id == "TAMP002"));
    }

    [Fact]
    public async Task Fires_When_Main_Exists_But_Does_Not_Dispatch()
    {
        // This is the tamp-ado-git regression: Main is the default empty Program.cs
        // pattern, TampBuild subclass exists, but Main never dispatches.
        var source = """
            using Tamp;
            class Build : TampBuild
            {
                public static int Main(string[] args)
                {
                    System.Console.WriteLine("hello");
                    return 0;
                }
            }
            """;
        var diags = await RunAsync(source);
        Assert.Single(diags.Where(d => d.Id == "TAMP002"));
    }

    [Fact]
    public async Task Does_Not_Fire_When_No_TampBuild_Subclass()
    {
        // Non-Tamp Exe in the same project — the analyzer must NOT fire.
        var source = """
            class Program
            {
                public static int Main(string[] args)
                {
                    System.Console.WriteLine("hello");
                    return 0;
                }
            }
            """;
        var diags = await RunAsync(source);
        Assert.Empty(diags.Where(d => d.Id == "TAMP002"));
    }

    [Fact]
    public async Task Does_Not_Fire_For_Library_With_TampBuild_Subclass_But_No_Main()
    {
        // Library shape — TampBuild subclass exists but there's no Main (the project might be a
        // shared helper, the Main lives in another project). Don't fire.
        var source = """
            using Tamp;
            class Build : TampBuild
            {
            }
            """;
        // Note: ConsoleApplication output kind would normally require a Main, but if Main is in
        // a different syntax tree the analyzer wouldn't see it. Here there's no Main at all.
        var diags = await RunAsync(source);
        Assert.Empty(diags.Where(d => d.Id == "TAMP002"));
    }

    [Fact]
    public async Task Fires_For_Empty_Main_With_TampBuild_Subclass()
    {
        // Empty body version — the exact tamp-ado-git symptom.
        var source = """
            using Tamp;
            class Build : TampBuild { }
            class Program
            {
                public static int Main(string[] args) => 0;
            }
            """;
        var diags = await RunAsync(source);
        Assert.Single(diags.Where(d => d.Id == "TAMP002"));
    }

    [Fact]
    public async Task Accepts_Generic_Execute_Call_Syntax_Variations()
    {
        var source = """
            using Tamp;
            class Build : TampBuild
            {
                public static int Main(string[] args)
                {
                    return Execute<Build>(args);  // statement form, not expression-body
                }
            }
            """;
        var diags = await RunAsync(source);
        Assert.Empty(diags.Where(d => d.Id == "TAMP002"));
    }
}
