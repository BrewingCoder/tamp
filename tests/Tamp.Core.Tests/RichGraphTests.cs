using Xunit;

namespace Tamp.Core.Tests;

/// <summary>
/// Tests for the richer dependency surface added on top of v0's strict DAG:
/// multi-target invocation, Before/After order constraints, Triggers and
/// TriggeredBy fan-out, OnFailureOf catch handlers, and OnlyWhen conditional
/// skipping.
/// </summary>
public sealed class RichGraphTests
{
    private static IReadOnlyDictionary<string, TargetSpec> Specs(params TargetSpec[] entries)
    {
        var d = new Dictionary<string, TargetSpec>(StringComparer.Ordinal);
        foreach (var s in entries) d[s.Name] = s;
        return d;
    }

    private static TargetSpec Spec(
        string name,
        string[]? dependsOn = null,
        string[]? after = null,
        string[]? before = null,
        string[]? triggers = null,
        string[]? triggeredBy = null,
        string[]? onFailureOf = null)
        => new()
        {
            Name = name,
            Dependencies = dependsOn ?? Array.Empty<string>(),
            OrderAfter = after ?? Array.Empty<string>(),
            OrderBefore = before ?? Array.Empty<string>(),
            Triggers = triggers ?? Array.Empty<string>(),
            TriggeredBy = triggeredBy ?? Array.Empty<string>(),
            OnFailureOf = onFailureOf ?? Array.Empty<string>(),
        };

    // ---- Multi-target invocation ----

    [Fact]
    public void Multi_Root_Invocation_Dedupes_Shared_Dependencies()
    {
        var graph = new TargetGraph(Specs(
            Spec("A"),
            Spec("B", dependsOn: ["A"]),
            Spec("C", dependsOn: ["A"])));

        var order = graph.ComputeExecutionOrder("B", "C");
        Assert.Equal(3, order.Count);
        Assert.Equal("A", order[0].Name);
        Assert.Contains(order, s => s.Name == "B");
        Assert.Contains(order, s => s.Name == "C");
    }

    [Fact]
    public void Empty_Roots_Throws()
    {
        var graph = new TargetGraph(Specs(Spec("A")));
        Assert.Throws<InvalidOperationException>(() => graph.ComputeExecutionOrder());
    }

    [Fact]
    public void Unknown_Root_Throws()
    {
        var graph = new TargetGraph(Specs(Spec("A")));
        Assert.Throws<InvalidOperationException>(() => graph.ComputeExecutionOrder("Missing"));
    }

    // ---- After / Before ----

    [Fact]
    public void After_Constrains_Order_When_Both_In_Plan()
    {
        // B is invoked. A is invoked. B.After(A) → A before B.
        // Critically: B does NOT pull A in via a hard dependency.
        var graph = new TargetGraph(Specs(
            Spec("A"),
            Spec("B", after: ["A"])));
        var order = graph.ComputeExecutionOrder("A", "B");
        var indexA = order.ToList().FindIndex(s => s.Name == "A");
        var indexB = order.ToList().FindIndex(s => s.Name == "B");
        Assert.True(indexA < indexB, $"A should precede B; got A@{indexA}, B@{indexB}");
    }

    [Fact]
    public void After_Does_Not_Pull_Dependency_Into_Plan()
    {
        // B is invoked alone. B.After(A) does NOT make A run.
        var graph = new TargetGraph(Specs(
            Spec("A"),
            Spec("B", after: ["A"])));
        var order = graph.ComputeExecutionOrder("B");
        Assert.Single(order);
        Assert.Equal("B", order[0].Name);
    }

    [Fact]
    public void Before_Constrains_Order_When_Both_In_Plan()
    {
        // A.Before(B) → A precedes B if both run.
        var graph = new TargetGraph(Specs(
            Spec("A", before: ["B"]),
            Spec("B")));
        var order = graph.ComputeExecutionOrder("A", "B");
        var indexA = order.ToList().FindIndex(s => s.Name == "A");
        var indexB = order.ToList().FindIndex(s => s.Name == "B");
        Assert.True(indexA < indexB);
    }

    [Fact]
    public void Before_Does_Not_Pull_Dependency_Into_Plan()
    {
        var graph = new TargetGraph(Specs(
            Spec("A", before: ["B"]),
            Spec("B")));
        var order = graph.ComputeExecutionOrder("A");
        Assert.Single(order);
        Assert.Equal("A", order[0].Name);
    }

    [Fact]
    public void Cycle_Across_DependsOn_And_After_Detected()
    {
        // A.DependsOn(B); B.After(A) → cycle.
        Assert.Throws<InvalidOperationException>(() => new TargetGraph(Specs(
            Spec("A", dependsOn: ["B"]),
            Spec("B", after: ["A"]))));
    }

    [Fact]
    public void Cycle_Across_Before_Detected()
    {
        // A.Before(B); B.Before(A) → cycle.
        Assert.Throws<InvalidOperationException>(() => new TargetGraph(Specs(
            Spec("A", before: ["B"]),
            Spec("B", before: ["A"]))));
    }

    // ---- Triggers / TriggeredBy ----

    [Fact]
    public void Triggers_Outgoing_Pulls_Target_Into_Plan()
    {
        // A invoked. A.Triggers(B) → B also runs.
        var graph = new TargetGraph(Specs(
            Spec("A", triggers: ["B"]),
            Spec("B")));
        var order = graph.ComputeExecutionOrder("A");
        Assert.Equal(2, order.Count);
        Assert.Contains(order, s => s.Name == "B");
    }

    [Fact]
    public void TriggeredBy_Incoming_Pulls_Target_Into_Plan()
    {
        // A invoked. B.TriggeredBy(A) → B runs because A ran.
        var graph = new TargetGraph(Specs(
            Spec("A"),
            Spec("B", triggeredBy: ["A"])));
        var order = graph.ComputeExecutionOrder("A");
        Assert.Equal(2, order.Count);
        Assert.Contains(order, s => s.Name == "B");
    }

    [Fact]
    public void Trigger_Chain_Expands_Until_Stable()
    {
        // A → triggers B → triggers C
        var graph = new TargetGraph(Specs(
            Spec("A", triggers: ["B"]),
            Spec("B", triggers: ["C"]),
            Spec("C")));
        var order = graph.ComputeExecutionOrder("A");
        Assert.Equal(3, order.Count);
    }

    [Fact]
    public void TriggeredBy_Constrains_Order()
    {
        // B.TriggeredBy(A) → A precedes B.
        var graph = new TargetGraph(Specs(
            Spec("A"),
            Spec("B", triggeredBy: ["A"])));
        var order = graph.ComputeExecutionOrder("A");
        Assert.Equal("A", order[0].Name);
        Assert.Equal("B", order[1].Name);
    }

    [Fact]
    public void Triggers_Without_Invocation_Means_Triggered_Target_Stays_Out()
    {
        // C is invoked alone; A.Triggers(B) is irrelevant since A isn't in the plan.
        var graph = new TargetGraph(Specs(
            Spec("A", triggers: ["B"]),
            Spec("B"),
            Spec("C")));
        var order = graph.ComputeExecutionOrder("C");
        Assert.Single(order);
        Assert.Equal("C", order[0].Name);
    }

    // ---- OnFailureOf handlers ----

    [Fact]
    public void HandlersFor_Returns_Empty_For_Targets_With_No_Handler()
    {
        var graph = new TargetGraph(Specs(Spec("A"), Spec("B")));
        Assert.Empty(graph.HandlersFor("A"));
    }

    [Fact]
    public void HandlersFor_Returns_Targets_That_Declared_OnFailureOf()
    {
        var graph = new TargetGraph(Specs(
            Spec("Deploy"),
            Spec("Notify", onFailureOf: ["Deploy"]),
            Spec("Cleanup", onFailureOf: ["Deploy", "Migrate"]),
            Spec("Migrate")));
        var handlers = graph.HandlersFor("Deploy");
        Assert.Equal(2, handlers.Count);
        Assert.Contains(handlers, h => h.Name == "Notify");
        Assert.Contains(handlers, h => h.Name == "Cleanup");
    }

    [Fact]
    public void Failure_Handlers_Do_Not_Appear_In_Normal_Plan()
    {
        var graph = new TargetGraph(Specs(
            Spec("Deploy"),
            Spec("Notify", onFailureOf: ["Deploy"])));
        var order = graph.ComputeExecutionOrder("Deploy");
        Assert.Single(order);
        Assert.Equal("Deploy", order[0].Name);
    }

    [Fact]
    public void OnFailureOf_References_Are_Validated_At_Construction()
    {
        Assert.Throws<InvalidOperationException>(() => new TargetGraph(Specs(
            Spec("Notify", onFailureOf: ["NonexistentTarget"]))));
    }

    [Fact]
    public void OnFailureOf_Cycles_Are_NOT_Detected_Because_They_Are_Runtime_Only()
    {
        // A.OnFailureOf(B), B.OnFailureOf(A) is silly but legal — failure
        // handlers are runtime conditional, not plan-time edges, so they
        // can't form a cycle in the ordering graph.
        var graph = new TargetGraph(Specs(
            Spec("A", onFailureOf: ["B"]),
            Spec("B", onFailureOf: ["A"])));
        Assert.NotNull(graph);
    }

    // ---- Edge-reference validation ----

    [Theory]
    [InlineData("after")]
    [InlineData("before")]
    [InlineData("triggers")]
    [InlineData("triggeredBy")]
    public void All_Edge_Kinds_Validate_References(string edgeKind)
    {
        var spec = edgeKind switch
        {
            "after" => Spec("A", after: ["Missing"]),
            "before" => Spec("A", before: ["Missing"]),
            "triggers" => Spec("A", triggers: ["Missing"]),
            "triggeredBy" => Spec("A", triggeredBy: ["Missing"]),
            _ => throw new ArgumentOutOfRangeException(nameof(edgeKind)),
        };
        Assert.Throws<InvalidOperationException>(() => new TargetGraph(Specs(spec)));
    }
}
