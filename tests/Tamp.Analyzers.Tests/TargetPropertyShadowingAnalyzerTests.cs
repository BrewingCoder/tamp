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
/// Tests for <see cref="TargetPropertyShadowingAnalyzer"/> (TAMP006).
/// Verifies the analyzer fires when a Target-typed property name shadows
/// a public static class in an imported namespace, and stays silent in
/// the canonical safe shapes.
/// </summary>
public sealed class TargetPropertyShadowingAnalyzerTests
{
    private const string StubSource = """
        namespace Tamp
        {
            public sealed class Target { /* not a real delegate — we just need the type name */ }
        }

        namespace Tamp.Cargo
        {
            public static class Cargo { public static int Build() => 0; }
        }

        namespace Tamp.GraphQLCodegen.V5
        {
            public static class GraphQLCodegen { public static int Generate() => 0; }
        }

        namespace SomeUnrelated
        {
            public class InstanceClass { }
            internal static class PrivateFacade { }
        }
        """;

    private static async Task<ImmutableArray<Diagnostic>> RunAsync(string userSource)
    {
        var refs = new[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Attribute).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Runtime.CompilerServices.RuntimeHelpers).Assembly.Location),
        };
        var compilation = CSharpCompilation.Create(
            "test-asm",
            new[] { CSharpSyntaxTree.ParseText(StubSource), CSharpSyntaxTree.ParseText(userSource) },
            refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var analyzers = ImmutableArray.Create<DiagnosticAnalyzer>(new TargetPropertyShadowingAnalyzer());
        var withAnalyzers = compilation.WithAnalyzers(analyzers);
        return await withAnalyzers.GetAnalyzerDiagnosticsAsync();
    }

    // ─── Shadowing detected — TAMP006 fires ──────────────────────────────

    [Fact]
    public async Task Fires_When_Target_Property_Shadows_GraphQLCodegen_Facade()
    {
        var src = """
            using Tamp;
            using Tamp.GraphQLCodegen.V5;
            class Build {
                Target GraphQLCodegen => null!;
            }
            """;
        var diags = (await RunAsync(src)).Where(d => d.Id == "TAMP006").ToList();
        Assert.Single(diags);
        Assert.Contains("GraphQLCodegen", diags[0].GetMessage());
        Assert.Contains("Tamp.GraphQLCodegen.V5.GraphQLCodegen", diags[0].GetMessage());
    }

    [Fact]
    public async Task Fires_When_Target_Property_Shadows_Cargo_Facade()
    {
        var src = """
            using Tamp;
            using Tamp.Cargo;
            class Build {
                Target Cargo => null!;
            }
            """;
        var diags = (await RunAsync(src)).Where(d => d.Id == "TAMP006").ToList();
        Assert.Single(diags);
    }

    [Fact]
    public async Task Suggests_Verb_Form_Rename_In_Message()
    {
        var src = """
            using Tamp;
            using Tamp.GraphQLCodegen.V5;
            class Build {
                Target GraphQLCodegen => null!;
            }
            """;
        var diag = (await RunAsync(src)).First(d => d.Id == "TAMP006");
        Assert.Contains("RunGraphQLCodegen", diag.GetMessage());
    }

    // ─── No false positives ──────────────────────────────────────────────

    [Fact]
    public async Task Stays_Silent_When_Property_Type_Is_Not_Tamp_Target()
    {
        // Even when the property name shadows a static class, if the type isn't
        // Tamp.Target the analyzer must not fire — that's TAMP005's territory
        // for [FromPath] fields, and other types are out-of-scope here.
        var src = """
            using Tamp.Cargo;
            class Build {
                int Cargo => 1;
            }
            """;
        Assert.Empty((await RunAsync(src)).Where(d => d.Id == "TAMP006"));
    }

    [Fact]
    public async Task Stays_Silent_When_Property_Name_Does_Not_Shadow_Anything()
    {
        var src = """
            using Tamp;
            using Tamp.GraphQLCodegen.V5;
            class Build {
                Target FrontendCodegen => null!;     // canonical fix — no shadowing
            }
            """;
        Assert.Empty((await RunAsync(src)).Where(d => d.Id == "TAMP006"));
    }

    [Fact]
    public async Task Stays_Silent_When_Satellite_Namespace_Not_Imported()
    {
        var src = """
            using Tamp;
            class Build {
                Target GraphQLCodegen => null!;    // No `using Tamp.GraphQLCodegen.V5;`
            }
            """;
        Assert.Empty((await RunAsync(src)).Where(d => d.Id == "TAMP006"));
    }

    [Fact]
    public async Task Stays_Silent_For_NonStatic_Or_Internal_Classes()
    {
        var src1 = """
            using Tamp;
            using SomeUnrelated;
            class Build {
                Target InstanceClass => null!;     // non-static — not a facade
            }
            """;
        Assert.Empty((await RunAsync(src1)).Where(d => d.Id == "TAMP006"));

        var src2 = """
            using Tamp;
            using SomeUnrelated;
            class Build {
                Target PrivateFacade => null!;     // internal — can't be `using`d cross-asm
            }
            """;
        Assert.Empty((await RunAsync(src2)).Where(d => d.Id == "TAMP006"));
    }

    [Fact]
    public async Task Stays_Silent_For_UsingStatic()
    {
        var src = """
            using Tamp;
            using static Tamp.Cargo.Cargo;
            class Build {
                Target Cargo => null!;
            }
            """;
        Assert.Empty((await RunAsync(src)).Where(d => d.Id == "TAMP006"));
    }

    [Fact]
    public async Task Stays_Silent_For_AliasUsing()
    {
        var src = """
            using Tamp;
            using CargoFacade = Tamp.Cargo.Cargo;
            class Build {
                Target Cargo => null!;
            }
            """;
        Assert.Empty((await RunAsync(src)).Where(d => d.Id == "TAMP006"));
    }

    // ─── Diagnostic location is on the property identifier (not the type) ─

    [Fact]
    public async Task Diagnostic_Location_Is_On_Property_Identifier()
    {
        var src = """
            using Tamp;
            using Tamp.GraphQLCodegen.V5;
            class Build {
                Target GraphQLCodegen => null!;
            }
            """;
        var diag = (await RunAsync(src)).First(d => d.Id == "TAMP006");
        var lineSpan = diag.Location.GetLineSpan();
        var lineText = src.Split('\n')[lineSpan.StartLinePosition.Line];
        Assert.Contains("GraphQLCodegen", lineText);
        // The reported character span starts where the identifier starts, not where the line starts.
        Assert.True(lineSpan.StartLinePosition.Character > 0);
    }
}
