using Xunit;

namespace Tamp.Core.Tests;

public sealed class TargetGraphTests
{
    private static IReadOnlyDictionary<string, TargetSpec> Specs(params (string Name, string[] Deps)[] entries)
    {
        var d = new Dictionary<string, TargetSpec>(StringComparer.Ordinal);
        foreach (var (name, deps) in entries)
            d[name] = new TargetSpec { Name = name, Dependencies = deps };
        return d;
    }

    [Fact]
    public void Linear_Chain_Yields_Dependencies_First()
    {
        var graph = new TargetGraph(Specs(
            ("A", []),
            ("B", ["A"]),
            ("C", ["B"])));
        var order = graph.TopologicalOrderFor("C").Select(s => s.Name).ToList();
        Assert.Equal(["A", "B", "C"], order);
    }

    [Fact]
    public void Diamond_Yields_Each_Target_Once()
    {
        //     A
        //    / \
        //   B   C
        //    \ /
        //     D
        var graph = new TargetGraph(Specs(
            ("A", []),
            ("B", ["A"]),
            ("C", ["A"]),
            ("D", ["B", "C"])));
        var order = graph.TopologicalOrderFor("D").Select(s => s.Name).ToList();
        Assert.Equal(4, order.Count);
        Assert.Equal("A", order[0]);
        Assert.Equal("D", order[3]);
        Assert.Contains("B", order);
        Assert.Contains("C", order);
        // B and C come before D, after A.
        Assert.True(order.IndexOf("B") < order.IndexOf("D"));
        Assert.True(order.IndexOf("C") < order.IndexOf("D"));
        Assert.True(order.IndexOf("A") < order.IndexOf("B"));
        Assert.True(order.IndexOf("A") < order.IndexOf("C"));
    }

    [Fact]
    public void Standalone_Root_Has_No_Other_Targets()
    {
        var graph = new TargetGraph(Specs(
            ("A", []),
            ("B", []),
            ("C", [])));
        var order = graph.TopologicalOrderFor("A").Select(s => s.Name).ToList();
        Assert.Equal(["A"], order);
    }

    [Fact]
    public void Cycle_Throws()
    {
        var graph = new TargetGraph(Specs(
            ("A", ["B"]),
            ("B", ["A"])));
        var ex = Assert.Throws<InvalidOperationException>(() => graph.TopologicalOrderFor("A"));
        Assert.Contains("Cycle detected", ex.Message);
    }

    [Fact]
    public void Self_Cycle_Throws()
    {
        var graph = new TargetGraph(Specs(
            ("A", ["A"])));
        var ex = Assert.Throws<InvalidOperationException>(() => graph.TopologicalOrderFor("A"));
        Assert.Contains("Cycle detected", ex.Message);
    }

    [Fact]
    public void Constructor_Throws_On_Missing_Dependency()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => new TargetGraph(Specs(
            ("A", ["Missing"]))));
        Assert.Contains("Missing", ex.Message);
    }

    [Fact]
    public void Top_Order_Throws_On_Unknown_Target()
    {
        var graph = new TargetGraph(Specs(("A", [])));
        Assert.Throws<InvalidOperationException>(() => graph.TopologicalOrderFor("B"));
    }

    [Fact]
    public void Constructor_Throws_On_Null_Targets()
    {
        Assert.Throws<ArgumentNullException>(() => new TargetGraph(null!));
    }

    [Fact]
    public void Empty_Graph_Permitted_If_Root_Exists()
    {
        var graph = new TargetGraph(new Dictionary<string, TargetSpec>());
        Assert.Throws<InvalidOperationException>(() => graph.TopologicalOrderFor("anything"));
    }
}
