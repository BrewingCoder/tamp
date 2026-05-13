using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Tamp.Analyzers;

/// <summary>
/// TAMP002 — fires when a class derives from <c>TampBuild</c> but the assembly's <c>Main</c>
/// method doesn't dispatch to it via <c>Execute&lt;T&gt;(args)</c>. Catches the regression
/// pattern that bit <c>tamp-ado-git</c> during the 2026-05-13 wave: <c>build/Build.csproj</c>
/// produced an Exe whose Main was the default empty <c>Program.cs</c>, so <c>dotnet tamp Ci</c>
/// failed with CS5001 — but only the Release pipeline (which actually runs the build script)
/// caught it; the satellite's slnx CI never even compiles <c>build/Build.csproj</c>.
/// </summary>
/// <remarks>
/// <para>
/// Detection rule (per strata-scott vote, "symbol-aware, scoped to Main"):
/// </para>
/// <list type="number">
///   <item>The compilation contains a class that derives (directly or transitively) from <c>Tamp.TampBuild</c>.</item>
///   <item>The compilation has a method named <c>Main</c> with a single <c>string[]</c> parameter.</item>
///   <item>The Main body does NOT contain a call to a method named <c>Execute</c> resolving to a method on a TampBuild-derived type.</item>
///   <item>Fire on the Main method declaration.</item>
/// </list>
/// <para>
/// Severity: Error. Scope is strict-Main for v0.1 of the rule; the call-graph-walk refinement
/// (accepting Execute in a method Main delegates to) lands when an adopter hits it.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MissingTampEntryPointAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "TAMP002";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        title: "TampBuild subclass missing Execute<T>(args) dispatch in Main",
        messageFormat: "A class derives from TampBuild but Main does not call Execute<T>(args). The build script will not dispatch any targets at runtime.",
        category: "Tamp.Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A Tamp build script project (typically build/Build.csproj) must have a Main entry point that dispatches to Execute<T>(args) on the TampBuild-derived class. Without that, the executable starts and exits without running any targets.",
        helpLinkUri: "https://github.com/tamp-build/tamp/blob/main/docs/analyzers/TAMP002.md",
        customTags: new[] { WellKnownDiagnosticTags.CompilationEnd });

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(OnCompilationStart);
    }

    private static void OnCompilationStart(CompilationStartAnalysisContext context)
    {
        // Compilation-level state collected across the analysis:
        // - subclasses: every class we've seen that derives from Tamp.TampBuild.
        // - mainCandidates: Main(string[]) method declarations.
        // - dispatchObserved: shared flag that any Main has been observed to call Execute on a TampBuild type.
        // After all symbol/syntax actions run, the CompilationEnd action reports for any Main that didn't dispatch.
        var subclasses = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        var mainCandidates = new List<(MethodDeclarationSyntax, Location)>();
        var observedDispatchFromMain = new HashSet<SyntaxNode>();
        var lockObj = new object();

        context.RegisterSymbolAction(symCtx =>
        {
            var type = (INamedTypeSymbol)symCtx.Symbol;
            if (DerivesFromTampBuild(type))
                lock (lockObj) subclasses.Add(type);
        }, SymbolKind.NamedType);

        context.RegisterSyntaxNodeAction(synCtx =>
        {
            var method = (MethodDeclarationSyntax)synCtx.Node;
            if (method.Identifier.Text != "Main") return;
            var symbol = synCtx.SemanticModel.GetDeclaredSymbol(method, synCtx.CancellationToken) as IMethodSymbol;
            if (symbol is null || !symbol.IsStatic) return;
            if (symbol.Parameters.Length != 1) return;
            if (symbol.Parameters[0].Type is not IArrayTypeSymbol arr) return;
            if (arr.ElementType.SpecialType != SpecialType.System_String) return;

            lock (lockObj) mainCandidates.Add((method, method.Identifier.GetLocation()));

            // Inspect this Main's body for Execute calls now (we have the semantic model handy).
            var body = (SyntaxNode?)method.Body ?? method.ExpressionBody;
            if (body is null) return;
            foreach (var inv in body.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (NameIsExecute(inv) && InvocationIsExecuteOnTampBuild(inv, synCtx.SemanticModel))
                {
                    lock (lockObj) observedDispatchFromMain.Add(method);
                    break;
                }
            }
        }, SyntaxKind.MethodDeclaration);

        context.RegisterCompilationEndAction(endCtx =>
        {
            // Only fire when both halves of the condition are observed:
            // - this compilation defined a TampBuild subclass
            // - AND the Main entry-point did NOT dispatch to Execute on a TampBuild type
            // Either-or alone is fine: a non-Tamp console app doesn't fire (no subclass);
            // a Tamp library project doesn't fire (no Main).
            if (subclasses.Count == 0) return;
            foreach (var (method, loc) in mainCandidates)
            {
                if (observedDispatchFromMain.Contains(method)) continue;
                endCtx.ReportDiagnostic(Diagnostic.Create(Rule, loc));
            }
        });
    }

    private static bool DerivesFromTampBuild(INamedTypeSymbol type)
    {
        for (var t = type.BaseType; t != null; t = t.BaseType)
        {
            if (t.Name == "TampBuild" && t.ContainingNamespace?.ToDisplayString() == "Tamp")
                return true;
        }
        return false;
    }

    private static bool NameIsExecute(InvocationExpressionSyntax inv) => inv.Expression switch
    {
        GenericNameSyntax gn => gn.Identifier.Text == "Execute",
        IdentifierNameSyntax id => id.Identifier.Text == "Execute",
        MemberAccessExpressionSyntax mae => mae.Name.Identifier.Text == "Execute",
        _ => false,
    };

    private static bool InvocationIsExecuteOnTampBuild(InvocationExpressionSyntax inv, SemanticModel model)
    {
        var symbol = model.GetSymbolInfo(inv).Symbol as IMethodSymbol;
        if (symbol is null)
        {
            // Symbol didn't resolve (e.g. compile-error scenario) — being lenient here would
            // suppress the real diagnostic when the build is mid-edit. Strict: name-match alone
            // is enough.
            return true;
        }
        for (var t = symbol.ContainingType; t != null; t = t.BaseType)
        {
            if (t.Name == "TampBuild" && t.ContainingNamespace?.ToDisplayString() == "Tamp") return true;
        }
        return false;
    }
}
