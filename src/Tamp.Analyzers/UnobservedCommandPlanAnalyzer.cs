using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Tamp.Analyzers;

/// <summary>
/// TAMP001 — flags a CommandPlan-returning invocation whose value is discarded
/// because it appears as a statement expression inside an <c>Executes(Action)</c>
/// lambda body. The lambda body has nowhere to put the plan, so the plan is
/// constructed and dropped. The target reports success in zero milliseconds
/// with no output — a silent no-op that strata-scott + holdfast both hit.
/// </summary>
/// <remarks>
/// Heuristic (no semantic-model-of-tamp-itself, to keep the analyzer portable):
/// <list type="number">
///   <item>For each invocation expression in a method/lambda body...</item>
///   <item>...whose return type is named <c>CommandPlan</c> or <c>IEnumerable&lt;CommandPlan&gt;</c>...</item>
///   <item>...AND whose enclosing block is the body of a lambda passed to a method named <c>Executes</c>...</item>
///   <item>...AND whose return value is discarded (the invocation is the entire expression in an ExpressionStatement)...</item>
///   <item>...report TAMP001 at the invocation's location.</item>
/// </list>
/// We deliberately do NOT match the full namespace (<c>Tamp.</c>) so the rule fires
/// on wrappers + custom user code uniformly. False-positive rate is low because the
/// <c>Executes</c> + <c>CommandPlan</c> combo is Tamp-specific.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class UnobservedCommandPlanAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "TAMP001";

    private static readonly LocalizableString Title =
        "CommandPlan value is unobserved";

    private static readonly LocalizableString MessageFormat =
        "Method '{0}' returns a CommandPlan but its return value is discarded inside an Executes(Action) lambda — the plan will not be executed";

    private static readonly LocalizableString Description =
        "Inside Executes(() => { ... }) the lambda's return value is void. CommandPlan-returning calls " +
        "whose value isn't returned or collected are constructed but never dispatched. Either return them " +
        "(Executes(() => new[] { DotNet.Build(...), DotNet.Test(...) })) or use Executes(Func<CommandPlan>) " +
        "for a single plan.";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        Title,
        MessageFormat,
        category: "Tamp.Usage",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: Description,
        helpLinkUri: "https://github.com/tamp-build/tamp/blob/main/docs/analyzers/TAMP001.md");

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

        // The invocation must be a statement expression (i.e. its value isn't used)
        // to be a candidate. Walk up to confirm.
        if (invocation.Parent is not ExpressionStatementSyntax) return;

        // The invocation must return CommandPlan or IEnumerable<CommandPlan> (or Task<CommandPlan>,
        // for the async patterns we may grow into). Otherwise it's a legit void or other return type.
        var symbol = context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol as IMethodSymbol;
        if (symbol is null) return;
        if (!ReturnsCommandPlan(symbol.ReturnType)) return;

        // The enclosing lambda must be the argument to a method invocation whose target
        // method is named "Executes". Walk up to find it.
        if (!IsInsideExecutesActionLambda(invocation)) return;

        var loc = invocation.GetLocation();
        var calledName = symbol.Name;
        context.ReportDiagnostic(Diagnostic.Create(Rule, loc, calledName));
    }

    private static bool ReturnsCommandPlan(ITypeSymbol returnType)
    {
        if (returnType is null) return false;
        if (returnType.Name == "CommandPlan") return true;
        // IEnumerable<CommandPlan>, IReadOnlyList<CommandPlan>, etc.
        if (returnType is INamedTypeSymbol named && named.TypeArguments.Length == 1)
        {
            var inner = named.TypeArguments[0];
            if (inner.Name == "CommandPlan") return true;
        }
        return false;
    }

    private static bool IsInsideExecutesActionLambda(SyntaxNode node)
    {
        // Walk up until we find a lambda whose parent is an argument to a method named Executes,
        // and confirm the lambda's body is a block (so callers using `=> CommandPlan` expressions
        // are excluded — those flow through Executes(Func<CommandPlan>) and the value is returned).
        for (var n = node.Parent; n != null; n = n.Parent)
        {
            if (n is ParenthesizedLambdaExpressionSyntax pl && pl.Body is BlockSyntax)
            {
                return IsArgumentToExecutes(pl);
            }
            if (n is SimpleLambdaExpressionSyntax sl && sl.Body is BlockSyntax)
            {
                return IsArgumentToExecutes(sl);
            }
            // Bail if we hit a method declaration — we've exited any enclosing lambda.
            if (n is MethodDeclarationSyntax || n is LocalFunctionStatementSyntax) return false;
        }
        return false;
    }

    private static bool IsArgumentToExecutes(SyntaxNode lambda)
    {
        // lambda -> Argument -> ArgumentList -> InvocationExpressionSyntax (the Executes call)
        if (lambda.Parent is not ArgumentSyntax arg) return false;
        if (arg.Parent is not ArgumentListSyntax argList) return false;
        if (argList.Parent is not InvocationExpressionSyntax invocation) return false;

        return invocation.Expression switch
        {
            // _.Executes(...) — fluent chain on target builder
            MemberAccessExpressionSyntax mae => mae.Name.Identifier.Text == "Executes",
            // Executes(...) — direct call (rare in build scripts but supported)
            IdentifierNameSyntax id => id.Identifier.Text == "Executes",
            _ => false,
        };
    }
}
