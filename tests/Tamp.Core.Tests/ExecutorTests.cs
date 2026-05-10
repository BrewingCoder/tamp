using Xunit;

namespace Tamp.Core.Tests;

public sealed class ExecutorTests
{
    private static TargetGraph GraphWith(params (string Name, string[] Deps, Action[] Actions, Func<IEnumerable<CommandPlan>>[] Plans)[] entries)
    {
        var d = new Dictionary<string, TargetSpec>(StringComparer.Ordinal);
        foreach (var (name, deps, actions, plans) in entries)
        {
            d[name] = new TargetSpec
            {
                Name = name,
                Dependencies = deps,
                Actions = actions,
                PlanFactories = plans,
            };
        }
        return new TargetGraph(d);
    }

    [Fact]
    public void Run_Mode_Executes_Actions_In_Topological_Order()
    {
        var order = new List<string>();
        var graph = GraphWith(
            ("A", [], [() => order.Add("A")], []),
            ("B", ["A"], [() => order.Add("B")], []),
            ("C", ["B"], [() => order.Add("C")], []));
        var exec = new Executor(graph, ExecutionMode.Run, TextWriter.Null);
        var result = exec.Run("C");
        Assert.Equal(0, result.ExitCode);
        Assert.Equal(["A", "B", "C"], order);
    }

    [Fact]
    public void Plan_Mode_Lists_Targets_Without_Executing_Actions()
    {
        var ran = false;
        var graph = GraphWith(
            ("A", [], [() => ran = true], []));
        var sw = new StringWriter();
        var exec = new Executor(graph, ExecutionMode.Plan, sw);
        var result = exec.Run("A");
        Assert.Equal(0, result.ExitCode);
        Assert.False(ran);
        Assert.Contains("A", sw.ToString());
    }

    [Fact]
    public void DryRun_Mode_Prints_Plans_Without_Executing()
    {
        var ran = false;
        var graph = GraphWith(
            ("A", [],
                [() => ran = true],
                [() => new[] { new CommandPlan { Executable = "echo", Arguments = ["hi"] } }]));
        var sw = new StringWriter();
        var exec = new Executor(graph, ExecutionMode.DryRun, sw);
        var result = exec.Run("A");
        Assert.Equal(0, result.ExitCode);
        Assert.False(ran);
        var output = sw.ToString();
        Assert.Contains("DRY RUN", output);
        Assert.Contains("echo", output);
        Assert.Contains("hi", output);
    }

    [Fact]
    public void DryRun_Mode_Reports_Plan_Count()
    {
        var graph = GraphWith(
            ("A", [],
                [],
                [() => new[]
                {
                    new CommandPlan { Executable = "a", Arguments = [] },
                    new CommandPlan { Executable = "b", Arguments = [] },
                }]));
        var exec = new Executor(graph, ExecutionMode.DryRun, TextWriter.Null);
        var result = exec.Run("A");
        Assert.Equal(2, result.CommandPlansPrinted);
    }

    [Fact]
    public void Action_Throwing_Aborts_Build_With_Failed_Target()
    {
        var graph = GraphWith(
            ("Bad", [], [() => throw new InvalidOperationException("boom")], []));
        var exec = new Executor(graph, ExecutionMode.Run, TextWriter.Null);
        var result = exec.Run("Bad");
        Assert.NotEqual(0, result.ExitCode);
        Assert.Equal("Bad", result.FailedTarget);
    }

    [Fact]
    public void Action_Throwing_Inside_Continue_Mode_Skips_Failure()
    {
        var d = new Dictionary<string, TargetSpec>(StringComparer.Ordinal)
        {
            ["Sloppy"] = new TargetSpec
            {
                Name = "Sloppy",
                FailureMode = FailureMode.Continue,
                Actions = [() => throw new InvalidOperationException("ignored")],
            },
        };
        var exec = new Executor(new TargetGraph(d), ExecutionMode.Run, TextWriter.Null);
        var result = exec.Run("Sloppy");
        Assert.Equal(0, result.ExitCode);
    }

    [Fact]
    public void Result_Includes_Targets_Traversed_Count()
    {
        var graph = GraphWith(
            ("A", [], [], []),
            ("B", ["A"], [], []),
            ("C", ["B"], [], []));
        var exec = new Executor(graph, ExecutionMode.Run, TextWriter.Null);
        var result = exec.Run("C");
        Assert.Equal(3, result.TargetsTraversed);
    }

    [Fact]
    public void Constructor_Throws_On_Null_Graph()
    {
        Assert.Throws<ArgumentNullException>(() => new Executor(null!));
    }
}
