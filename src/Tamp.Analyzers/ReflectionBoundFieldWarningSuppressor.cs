using System.Collections.Generic;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

[assembly: InternalsVisibleTo("Tamp.Analyzers.Tests")]

namespace Tamp.Analyzers;

/// <summary>
/// TAMP1001 / TAMP1002 — suppress CS0414 ("field is assigned but never used")
/// and IDE0051 ("remove unused private member") on fields decorated with
/// Tamp's reflection-bound attributes (<c>[FromPath]</c>,
/// <c>[FromNodeModules]</c>, <c>[Parameter]</c>, <c>[Secret]</c>).
/// </summary>
/// <remarks>
/// <para>
/// The adopter idiom <c>[FromPath("cargo")] readonly Tool CargoBin = null!;</c>
/// creates a <c>private readonly</c> field initialised to <c>null!</c> and
/// assigned by Tamp's reflection binder at <c>Execute&lt;T&gt;</c> time. The
/// C# compiler can't see the binder's reflection assignment, so it raises
/// CS0414 if the field is never explicitly read by adopter code. This happens
/// in the wild whenever an adopter:
/// </para>
/// <list type="bullet">
///   <item>Declares a tool for completeness but then bypasses the wrapper
///         that would consume it (DasBook bypassed <c>Tauri.Build()</c> and
///         hit CS0414 on the unused <c>[FromNodeModules("tauri")]</c> field).</item>
///   <item>Comments out a target temporarily during debugging.</item>
///   <item>Splits a Build.cs across partial-class files and a field crosses files.</item>
/// </list>
/// <para>
/// In all three cases the warning is noise — Tamp's binder uses the field;
/// the compiler just can't see that. This suppressor lets adopters write
/// the canonical idiom without noise. Filed under TAM-206. DasBook canary
/// friction batch #3 #9 (2026-05-13).
/// </para>
/// <para>
/// Match is by simple attribute name (with or without <c>Attribute</c>
/// suffix), namespace-agnostic. Adopters who derive a custom reflection-bound
/// attribute can name it <c>[Parameter]</c>-style and get the same suppression
/// for free.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ReflectionBoundFieldWarningSuppressor : DiagnosticSuppressor
{
    internal const string Cs0414SuppressionId = "TAMP1001";
    internal const string Ide0051SuppressionId = "TAMP1002";

    private const string Justification =
        "Field is bound at runtime by Tamp's reflection-based binder via " +
        "[FromPath] / [FromNodeModules] / [Parameter] / [Secret]. The compiler " +
        "cannot see the binder's assignment or read sites, but the field IS used.";

    private static readonly SuppressionDescriptor SuppressCs0414 = new(
        id: Cs0414SuppressionId,
        suppressedDiagnosticId: "CS0414",
        justification: Justification);

    private static readonly SuppressionDescriptor SuppressIde0051 = new(
        id: Ide0051SuppressionId,
        suppressedDiagnosticId: "IDE0051",
        justification: Justification);

    public override ImmutableArray<SuppressionDescriptor> SupportedSuppressions { get; } =
        ImmutableArray.Create(SuppressCs0414, SuppressIde0051);

    // Recognised attribute simple-names (case-sensitive — C# is). Both with
    // and without the `Attribute` suffix are accepted since C# allows either
    // form at the call site.
    private static readonly HashSet<string> ReflectionBoundAttributeNames = new()
    {
        "FromPath",            "FromPathAttribute",
        "FromNodeModules",     "FromNodeModulesAttribute",
        "Parameter",           "ParameterAttribute",
        "Secret",              "SecretAttribute",
    };

    public override void ReportSuppressions(SuppressionAnalysisContext context)
    {
        foreach (var diagnostic in context.ReportedDiagnostics)
        {
            var fieldDeclaration = FindEnclosingFieldDeclaration(diagnostic);
            if (fieldDeclaration is null) continue;
            if (!HasReflectionBoundAttribute(fieldDeclaration)) continue;

            SuppressionDescriptor? descriptor = diagnostic.Id switch
            {
                "CS0414" => SuppressCs0414,
                "IDE0051" => SuppressIde0051,
                _ => null,
            };
            if (descriptor is not null)
                context.ReportSuppression(Suppression.Create(descriptor, diagnostic));
        }
    }

    private static FieldDeclarationSyntax? FindEnclosingFieldDeclaration(Diagnostic diagnostic)
    {
        var location = diagnostic.Location;
        var tree = location.SourceTree;
        if (tree is null) return null;
        var root = tree.GetRoot();
        var node = root.FindNode(location.SourceSpan, getInnermostNodeForTie: true);

        // Walk up — diagnostic location may point at the variable declarator
        // (the field's name) rather than at the full field declaration.
        while (node is not null)
        {
            if (node is FieldDeclarationSyntax field) return field;
            node = node.Parent;
        }
        return null;
    }

    /// <summary>
    /// Returns true when <paramref name="field"/> carries one of Tamp's
    /// reflection-bound attributes. Exposed <c>internal</c> for unit testing —
    /// the integration path (running the suppressor under the Roslyn analyzer
    /// driver) is covered by adopter build smoke tests where the suppressor
    /// ships in real CI, but the syntactic-detection logic is where bugs
    /// actually live and is testable in isolation.
    /// </summary>
    internal static bool HasReflectionBoundAttribute(FieldDeclarationSyntax field)
    {
        foreach (var list in field.AttributeLists)
            foreach (var attribute in list.Attributes)
            {
                var nameSyntax = attribute.Name;
                // Strip namespace qualifiers (e.g. Tamp.FromPath → FromPath) since
                // we match by simple name to remain namespace-agnostic.
                var name = nameSyntax is QualifiedNameSyntax qualified
                    ? qualified.Right.Identifier.Text
                    : (nameSyntax as SimpleNameSyntax)?.Identifier.Text
                      ?? nameSyntax.ToString();
                if (ReflectionBoundAttributeNames.Contains(name)) return true;
            }
        return false;
    }
}
