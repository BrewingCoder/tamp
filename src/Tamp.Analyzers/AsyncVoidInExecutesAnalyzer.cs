using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Tamp.Analyzers;

/// <summary>
/// TAMP003 — fires when an <c>async</c> lambda passed to <c>Executes(...)</c> matches the
/// <c>Executes(Action)</c> overload rather than <c>Executes(Func&lt;Task&gt;)</c>. The lambda's
/// state machine becomes <c>async void</c>, control returns to Tamp before the await
/// completes, and the target appears to finish without doing the awaited work.
/// </summary>
/// <remarks>
/// Heuristic:
/// <list type="number">
///   <item>An <c>InvocationExpression</c> whose target is a method named <c>Executes</c>...</item>
///   <item>...with at least one argument that's a lambda with the <c>async</c> modifier...</item>
///   <item>...AND the matched parameter is <c>System.Action</c> (not <c>Func&lt;Task&gt;</c>)...</item>
///   <item>...AND the lambda body contains at least one <c>await</c> expression.</item>
/// </list>
/// The "matched parameter is Action" step is the load-bearing check — it's what distinguishes
/// the silent-no-op shape from the correct shape that exists on Tamp.Core 1.5.0+. We use the
/// SymbolInfo from the invocation to resolve the actual overload that bound.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class AsyncVoidInExecutesAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "TAMP003";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        title: "async lambda passed to Executes(Action) becomes async void",
        messageFormat: "async lambda matched Executes(Action) — the Task is discarded and the target completes before the awaited work finishes. Upgrade to Tamp.Core 1.5.0+ and the call binds to Executes(Func<Task>) automatically.",
        category: "Tamp.Usage",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Inside Executes(async () => { await ... }) the async lambda becomes async void when the matched overload is Action. Tamp returns immediately and the awaited work runs unobserved. Bump Tamp.Core to 1.5.0+ to gain the Executes(Func<Task>) overload, after which the same lambda binds correctly.",
        helpLinkUri: "https://github.com/tamp-build/tamp/blob/main/docs/analyzers/TAMP003.md");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.InvocationExpression);
    }

    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        // Only inspect Executes(...) calls.
        var calledName = invocation.Expression switch
        {
            MemberAccessExpressionSyntax mae => mae.Name.Identifier.Text,
            IdentifierNameSyntax id => id.Identifier.Text,
            _ => "",
        };
        if (calledName != "Executes") return;

        var symbol = context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol as IMethodSymbol;
        if (symbol is null) return;
        if (symbol.Parameters.Length == 0) return;

        // Walk each argument; flag when a lambda argument is async + has await AND the
        // matched parameter type is System.Action.
        var argList = invocation.ArgumentList?.Arguments;
        if (argList is null) return;

        for (var i = 0; i < argList.Value.Count && i < symbol.Parameters.Length; i++)
        {
            var arg = argList.Value[i];
            var param = symbol.Parameters[i];
            if (!IsActionType(param.Type)) continue;

            var lambda = arg.Expression switch
            {
                ParenthesizedLambdaExpressionSyntax pl => (AnonymousFunctionExpressionSyntax)pl,
                SimpleLambdaExpressionSyntax sl => sl,
                AnonymousMethodExpressionSyntax am => am,
                _ => null,
            };
            if (lambda is null) continue;

            // Must be `async`, must contain at least one `await`.
            if (!lambda.Modifiers.Any(m => m.IsKind(SyntaxKind.AsyncKeyword))) continue;
            var body = (SyntaxNode?)lambda.Block ?? lambda.ExpressionBody;
            if (body is null) continue;
            if (!body.DescendantNodes().OfType<AwaitExpressionSyntax>().Any()) continue;

            context.ReportDiagnostic(Diagnostic.Create(Rule, lambda.GetLocation()));
        }
    }

    private static bool IsActionType(ITypeSymbol type)
    {
        // System.Action — non-generic, no parameters. Distinguishes it from Func<T> / Action<T> etc.
        if (type is not INamedTypeSymbol named) return false;
        return named.Name == "Action"
            && named.ContainingNamespace?.ToDisplayString() == "System"
            && named.TypeParameters.Length == 0
            && !named.IsGenericType;
    }
}
