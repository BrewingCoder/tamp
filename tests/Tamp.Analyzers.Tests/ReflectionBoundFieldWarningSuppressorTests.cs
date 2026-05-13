using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Tamp.Analyzers;
using Xunit;

namespace Tamp.Analyzers.Tests;

/// <summary>
/// Tests for <see cref="ReflectionBoundFieldWarningSuppressor"/> (TAM-206 /
/// TAMP1001 / TAMP1002).
///
/// The suppressor's correctness reduces to one question: does
/// <see cref="ReflectionBoundFieldWarningSuppressor.HasReflectionBoundAttribute"/>
/// recognise the right attributes on a field declaration? The Roslyn driver
/// integration (running under CompilationWithAnalyzers + reading
/// IsSuppressed on compiler diagnostics) is covered by the in-CI adopter
/// path — Roslyn ships and operates the suppressor pipeline reliably across
/// minor versions, but exposes that path inconsistently to unit tests across
/// Microsoft.CodeAnalysis 4.x point releases. So we test the detection logic
/// directly here, and rely on adopter CI for the end-to-end smoke.
/// </summary>
public sealed class ReflectionBoundFieldWarningSuppressorTests
{
    private static FieldDeclarationSyntax ParseFirstField(string source)
    {
        var tree = CSharpSyntaxTree.ParseText(source);
        return tree.GetRoot()
            .DescendantNodes()
            .OfType<FieldDeclarationSyntax>()
            .First();
    }

    // ─── Recognised: short attribute names ───

    [Theory]
    [InlineData("FromPath")]
    [InlineData("FromNodeModules")]
    [InlineData("Parameter")]
    [InlineData("Secret")]
    public void Recognises_Short_Attribute_Names(string attrName)
    {
        var source = $$"""
            class Build {
                [{{attrName}}] private readonly int X = 1;
            }
            """;
        var field = ParseFirstField(source);
        Assert.True(ReflectionBoundFieldWarningSuppressor.HasReflectionBoundAttribute(field),
            $"Expected [{attrName}] to be recognized as reflection-bound.");
    }

    // ─── Recognised: full attribute names with `Attribute` suffix ───

    [Theory]
    [InlineData("FromPathAttribute")]
    [InlineData("FromNodeModulesAttribute")]
    [InlineData("ParameterAttribute")]
    [InlineData("SecretAttribute")]
    public void Recognises_Full_Attribute_Names(string attrName)
    {
        var source = $$"""
            class Build {
                [{{attrName}}] private readonly int X = 1;
            }
            """;
        var field = ParseFirstField(source);
        Assert.True(ReflectionBoundFieldWarningSuppressor.HasReflectionBoundAttribute(field),
            $"Expected [{attrName}] (full name) to be recognized as reflection-bound.");
    }

    // ─── Recognised: namespace-qualified attribute names ───

    [Theory]
    [InlineData("Tamp.FromPath")]
    [InlineData("Tamp.FromPathAttribute")]
    [InlineData("global::Tamp.FromNodeModules")]
    [InlineData("Tamp.Annotations.Parameter")]      // hypothetical nested namespace
    public void Recognises_Namespace_Qualified_Attributes(string attrName)
    {
        var source = $$"""
            class Build {
                [{{attrName}}] private readonly int X = 1;
            }
            """;
        var field = ParseFirstField(source);
        Assert.True(ReflectionBoundFieldWarningSuppressor.HasReflectionBoundAttribute(field),
            $"Expected [{attrName}] (qualified) to be recognized as reflection-bound.");
    }

    // ─── Recognised: with attribute arguments (the realistic shape) ───

    [Fact]
    public void Recognises_FromPath_With_Constructor_Argument()
    {
        var field = ParseFirstField("""
            class Build {
                [FromPath("cargo")] private readonly object CargoBin = null!;
            }
            """);
        Assert.True(ReflectionBoundFieldWarningSuppressor.HasReflectionBoundAttribute(field));
    }

    [Fact]
    public void Recognises_FromNodeModules_With_Constructor_Argument()
    {
        var field = ParseFirstField("""
            class Build {
                [FromNodeModules("tauri")] private readonly object TauriCli = null!;
            }
            """);
        Assert.True(ReflectionBoundFieldWarningSuppressor.HasReflectionBoundAttribute(field));
    }

    // ─── Recognised: mixed attribute lists ───

    [Fact]
    public void Recognises_When_One_Of_Multiple_Attributes_Matches()
    {
        var field = ParseFirstField("""
            using System;
            class Build {
                [Obsolete] [FromPath("cargo")] private readonly object X = null!;
            }
            """);
        Assert.True(ReflectionBoundFieldWarningSuppressor.HasReflectionBoundAttribute(field));
    }

    [Fact]
    public void Recognises_When_Multiple_Attribute_Lists_Present()
    {
        // C# allows multiple [...] groups on a single declaration.
        var field = ParseFirstField("""
            using System;
            class Build {
                [Obsolete]
                [FromPath("cargo")]
                private readonly object X = null!;
            }
            """);
        Assert.True(ReflectionBoundFieldWarningSuppressor.HasReflectionBoundAttribute(field));
    }

    // ─── Not recognised: control cases ───

    [Fact]
    public void Does_Not_Recognise_Field_Without_Any_Attribute()
    {
        var field = ParseFirstField("""
            class Build {
                private readonly int Counter = 42;
            }
            """);
        Assert.False(ReflectionBoundFieldWarningSuppressor.HasReflectionBoundAttribute(field));
    }

    [Theory]
    [InlineData("Obsolete")]
    [InlineData("Required")]
    [InlineData("Description")]
    [InlineData("UnrelatedSecretSomething")]   // not an exact match
    [InlineData("MySecret")]                    // not an exact match either — case-sensitive
    public void Does_Not_Recognise_Unrelated_Attributes(string attrName)
    {
        var field = ParseFirstField($$"""
            using System;
            class Build {
                [{{attrName}}] private readonly int X = 1;
            }
            """);
        Assert.False(ReflectionBoundFieldWarningSuppressor.HasReflectionBoundAttribute(field),
            $"[{attrName}] should NOT match Tamp's reflection-bound attribute set.");
    }

    [Fact]
    public void Match_Is_Case_Sensitive()
    {
        // C# attribute names are case-sensitive; we should not match `fromPath`.
        var field = ParseFirstField("""
            class Build {
                [fromPath("cargo")] private readonly object X = null!;
            }
            """);
        Assert.False(ReflectionBoundFieldWarningSuppressor.HasReflectionBoundAttribute(field));
    }

    // ─── SuppressionDescriptors are correctly configured ───

    [Fact]
    public void Supports_Both_Cs0414_And_Ide0051_Suppressions()
    {
        var suppressor = new ReflectionBoundFieldWarningSuppressor();
        var supported = suppressor.SupportedSuppressions;
        Assert.Equal(2, supported.Length);
        Assert.Contains(supported, s => s.SuppressedDiagnosticId == "CS0414" && s.Id == "TAMP1001");
        Assert.Contains(supported, s => s.SuppressedDiagnosticId == "IDE0051" && s.Id == "TAMP1002");
    }

    [Fact]
    public void Suppression_Justifications_Mention_Reflection_Binder()
    {
        var suppressor = new ReflectionBoundFieldWarningSuppressor();
        foreach (var s in suppressor.SupportedSuppressions)
        {
            var justification = s.Justification.ToString();
            Assert.Contains("reflection", justification,
                System.StringComparison.OrdinalIgnoreCase);
            // Adopters reading the justification (e.g. via IDE quick-info on a
            // suppressed diagnostic) should see WHY the suppression is safe.
            Assert.Contains("Tamp", justification);
        }
    }
}
