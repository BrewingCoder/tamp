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
/// Tests for <see cref="FromPathFieldShadowingAnalyzer"/> (TAMP005, TAM-200).
/// Verifies the analyzer fires when a [FromPath]/[FromNodeModules] field
/// name shadows a public static class in an imported namespace, and stays
/// silent for non-conflicting names.
/// </summary>
public sealed class FromPathFieldShadowingAnalyzerTests
{
    // Minimal stubs for the Tamp.Cargo / Tamp.Tauri.V2 facade pattern.
    private const string StubSource = """
        namespace Tamp
        {
            public sealed class FromPathAttribute : System.Attribute {
                public FromPathAttribute(string name) { Name = name; }
                public string Name { get; }
            }
            public sealed class FromNodeModulesAttribute : System.Attribute {
                public FromNodeModulesAttribute(string name) { Name = name; }
                public string Name { get; }
            }
            public sealed class Tool { }
        }

        namespace Tamp.Cargo
        {
            // The facade class whose name a poorly-named [FromPath] field shadows.
            public static class Cargo { public static int Build() => 0; }
        }

        namespace Tamp.Tauri.V2
        {
            public static class Tauri { public static int Build() => 0; }
        }

        namespace SomeUnrelated
        {
            public class NotStatic { }
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

        var analyzers = ImmutableArray.Create<DiagnosticAnalyzer>(new FromPathFieldShadowingAnalyzer());
        var withAnalyzers = compilation.WithAnalyzers(analyzers);
        return await withAnalyzers.GetAnalyzerDiagnosticsAsync();
    }

    // ─── Shadowing detected — TAMP005 fires ──────────────────────────────

    [Fact]
    public async Task Fires_When_FromPath_Field_Shadows_Cargo_Facade()
    {
        var src = """
            using Tamp;
            using Tamp.Cargo;
            class Build {
                [FromPath("cargo")] readonly Tool Cargo = null!;
            }
            """;
        var diags = (await RunAsync(src)).Where(d => d.Id == "TAMP005").ToList();
        Assert.Single(diags);
        Assert.Contains("Cargo", diags[0].GetMessage());
        Assert.Contains("Tamp.Cargo.Cargo", diags[0].GetMessage());
    }

    [Fact]
    public async Task Fires_When_FromNodeModules_Field_Shadows_Tauri_Facade()
    {
        var src = """
            using Tamp;
            using Tamp.Tauri.V2;
            class Build {
                [FromNodeModules("tauri")] readonly Tool Tauri = null!;
            }
            """;
        var diags = (await RunAsync(src)).Where(d => d.Id == "TAMP005").ToList();
        Assert.Single(diags);
        Assert.Contains("Tauri", diags[0].GetMessage());
    }

    [Fact]
    public async Task Suggests_Bin_Convention_In_Message()
    {
        var src = """
            using Tamp;
            using Tamp.Cargo;
            class Build {
                [FromPath("cargo")] readonly Tool Cargo = null!;
            }
            """;
        var diag = (await RunAsync(src)).First(d => d.Id == "TAMP005");
        Assert.Contains("CargoBin", diag.GetMessage());
    }

    // ─── No false positives — analyzer stays silent in the right cases ──

    [Fact]
    public async Task Stays_Silent_When_Field_Uses_Bin_Convention()
    {
        var src = """
            using Tamp;
            using Tamp.Cargo;
            class Build {
                [FromPath("cargo")] readonly Tool CargoBin = null!;
            }
            """;
        Assert.Empty((await RunAsync(src)).Where(d => d.Id == "TAMP005"));
    }

    [Theory]
    [InlineData("CargoTool")]
    [InlineData("CargoCli")]
    [InlineData("CargoExe")]
    public async Task Stays_Silent_For_Other_Deshadowed_Suffixes(string fieldName)
    {
        var src = $$"""
            using Tamp;
            using Tamp.Cargo;
            class Build {
                [FromPath("cargo")] readonly Tool {{fieldName}} = null!;
            }
            """;
        Assert.Empty((await RunAsync(src)).Where(d => d.Id == "TAMP005"));
    }

    [Fact]
    public async Task Stays_Silent_When_Satellite_Namespace_Not_Imported()
    {
        // No `using Tamp.Cargo;` — the static facade isn't in scope, so the
        // adopter's field doesn't shadow anything reachable.
        var src = """
            using Tamp;
            class Build {
                [FromPath("cargo")] readonly Tool Cargo = null!;
            }
            """;
        Assert.Empty((await RunAsync(src)).Where(d => d.Id == "TAMP005"));
    }

    [Fact]
    public async Task Stays_Silent_When_Field_Has_No_Tamp_Attribute()
    {
        var src = """
            using Tamp.Cargo;
            class Build {
                readonly object Cargo = null!;
            }
            """;
        Assert.Empty((await RunAsync(src)).Where(d => d.Id == "TAMP005"));
    }

    [Fact]
    public async Task Stays_Silent_For_Non_Static_Class_With_Matching_Name()
    {
        // SomeUnrelated.NotStatic is a regular (non-static) class — calling
        // NotStatic.Verb(...) wouldn't compile anyway (instance member),
        // so the shadowing concern doesn't apply.
        var src = """
            using Tamp;
            using SomeUnrelated;
            class Build {
                [FromPath("notstatic")] readonly Tool NotStatic = null!;
            }
            """;
        Assert.Empty((await RunAsync(src)).Where(d => d.Id == "TAMP005"));
    }

    [Fact]
    public async Task Stays_Silent_For_Internal_Static_Class_With_Matching_Name()
    {
        // Internal accessibility — adopter can't `using` it cross-assembly so
        // the shadowing risk is minimal. Don't false-positive.
        var src = """
            using Tamp;
            using SomeUnrelated;
            class Build {
                [FromPath("privatefacade")] readonly Tool PrivateFacade = null!;
            }
            """;
        Assert.Empty((await RunAsync(src)).Where(d => d.Id == "TAMP005"));
    }

    // ─── Special using forms — analyzer correctly ignores ──────────────

    [Fact]
    public async Task Stays_Silent_When_Using_Is_Static()
    {
        // `using static X` brings members in, not the type itself — adopter
        // can call Build() directly. Field shadowing of the static class
        // name isn't applicable.
        var src = """
            using Tamp;
            using static Tamp.Cargo.Cargo;
            class Build {
                [FromPath("cargo")] readonly Tool Cargo = null!;
            }
            """;
        Assert.Empty((await RunAsync(src)).Where(d => d.Id == "TAMP005"));
    }

    [Fact]
    public async Task Stays_Silent_When_Using_Is_Aliased()
    {
        // `using Cargo = Tamp.Cargo.Cargo;` aliases — but adopters use the
        // ALIAS name to reach the facade. The field's bare "Cargo" doesn't
        // shadow the alias resolution in the way the analyzer detects.
        // Skipping aliased usings is the safe default; if it produces a
        // false negative in practice we can revisit.
        var src = """
            using Tamp;
            using CargoFacade = Tamp.Cargo.Cargo;
            class Build {
                [FromPath("cargo")] readonly Tool Cargo = null!;
            }
            """;
        Assert.Empty((await RunAsync(src)).Where(d => d.Id == "TAMP005"));
    }

    // ─── Multi-declarator fields ─────────────────────────────────────

    [Fact]
    public async Task Fires_Once_Per_Declarator_In_Multi_Field_Declaration()
    {
        // Edge case — both fields in a multi-declarator share the same
        // attribute list. If either has a shadowing name, fire.
        var src = """
            using Tamp;
            using Tamp.Cargo;
            class Build {
                [FromPath("cargo")] readonly Tool Cargo = null!, Other = null!;
            }
            """;
        var diags = (await RunAsync(src)).Where(d => d.Id == "TAMP005").ToList();
        // Only the `Cargo` variant shadows; `Other` doesn't.
        Assert.Single(diags);
        Assert.Contains("Cargo", diags[0].GetMessage());
    }
}
