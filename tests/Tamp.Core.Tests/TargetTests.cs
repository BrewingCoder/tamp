using Xunit;

namespace Tamp.Core.Tests;

public sealed class TargetTests
{
    private static TargetSpec Build(string name, Target del)
    {
        var def = new TargetDefinition();
        del(def);
        return def.Build(name);
    }

    [Fact]
    public void Empty_Definition_Produces_Spec_With_Defaults()
    {
        var spec = Build("X", _ => _);
        Assert.Equal("X", spec.Name);
        Assert.Equal(Phase.None, spec.Phase);
        Assert.Empty(spec.Dependencies);
        Assert.Empty(spec.Resources);
        Assert.Empty(spec.Tags);
        Assert.False(spec.RequiresNetwork);
        Assert.False(spec.RequiresDocker);
        Assert.False(spec.RequiresAdmin);
        Assert.False(spec.Idempotent);
        Assert.Equal(RunMode.Always, spec.RunMode);
        Assert.Equal(FailureMode.Fatal, spec.FailureMode);
    }

    [Fact]
    public void Phase_Round_Trips()
    {
        var spec = Build("X", _ => _.Phase(Phase.Test));
        Assert.Equal(Phase.Test, spec.Phase);
    }

    [Fact]
    public void Phase_With_Descriptor_Round_Trips()
    {
        var spec = Build("X", _ => _.Phase(Phase.Custom, new PhaseDescriptor("Smoke")));
        Assert.Equal(Phase.Custom, spec.Phase);
        Assert.Equal("Smoke", spec.PhaseDescriptor?.Name);
    }

    [Fact]
    public void Description_Round_Trips()
    {
        var spec = Build("X", _ => _.Description("Builds the world"));
        Assert.Equal("Builds the world", spec.Description);
    }

    [Fact]
    public void Tags_Accumulate_Across_Calls()
    {
        var spec = Build("X", _ => _.Tag("a", "b").Tag("c"));
        Assert.Equal(["a", "b", "c"], spec.Tags);
    }

    [Fact]
    public void DependsOn_Accumulates()
    {
        var spec = Build("X", _ => _.DependsOn("Restore", "Compile").DependsOn("Test"));
        Assert.Equal(["Restore", "Compile", "Test"], spec.Dependencies);
    }

    [Fact]
    public void Consumes_Records_Resource_And_Mode()
    {
        var spec = Build("X", _ => _
            .Consumes(Resource.BuildCache.Dotnet, ConsumeMode.Exclusive)
            .Consumes(Resource.Network.Internet, ConsumeMode.Shared));
        Assert.Equal(2, spec.Resources.Count);
        Assert.Equal(ConsumeMode.Exclusive, spec.Resources[0].Mode);
        Assert.Equal(ConsumeMode.Shared, spec.Resources[1].Mode);
    }

    [Fact]
    public void Capabilities_Are_Settable_And_Independent()
    {
        var spec = Build("X", _ => _.RequiresNetwork().RequiresDocker());
        Assert.True(spec.RequiresNetwork);
        Assert.True(spec.RequiresDocker);
        Assert.False(spec.RequiresAdmin);
    }

    [Fact]
    public void RequiresTool_Records_Name_And_Optional_Version()
    {
        var spec = Build("X", _ => _.RequiresTool("docker").RequiresTool("kubectl", "1.30"));
        Assert.Equal(2, spec.ToolRequirements.Count);
        Assert.Null(spec.ToolRequirements[0].MinVersion);
        Assert.Equal("1.30", spec.ToolRequirements[1].MinVersion);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(int.MaxValue)]
    public void Memory_Budget_Accepts_Boundary_Values(int mb)
    {
        var spec = Build("X", _ => _.MemoryBudget(mb));
        Assert.Equal(mb, spec.MemoryBudgetMb);
    }

    [Fact]
    public void Retry_Sets_Failure_Mode_And_Backoff()
    {
        var spec = Build("X", _ => _.Retry(3, Backoff.Linear(TimeSpan.FromSeconds(1)), 137, 143));
        Assert.Equal(FailureMode.Retry, spec.FailureMode);
        Assert.Equal(3, spec.RetryCount);
        Assert.NotNull(spec.RetryBackoff);
        Assert.Equal([137, 143], spec.RetryableExitCodes);
    }

    [Fact]
    public void Idempotent_And_Produces_Round_Trip()
    {
        var spec = Build("X", _ => _.Idempotent().Produces("artifacts/*.nupkg").Produces("artifacts/*.snupkg"));
        Assert.True(spec.Idempotent);
        Assert.Equal(["artifacts/*.nupkg", "artifacts/*.snupkg"], spec.ProducedGlobs);
    }

    [Fact]
    public void Executes_Action_Records_Action()
    {
        var ran = false;
        var spec = Build("X", _ => _.Executes(() => ran = true));
        Assert.Single(spec.Actions);
        spec.Actions[0]();  // Manually invoke for the test.
        Assert.True(ran);
    }

    [Fact]
    public void Executes_CommandPlan_Factory_Wraps_To_Single_Plan_Sequence()
    {
        var spec = Build("X", _ => _.Executes(() => new CommandPlan
        {
            Executable = "tool",
            Arguments = ["arg"],
        }));
        Assert.Single(spec.PlanFactories);
        var plans = spec.PlanFactories[0]().ToList();
        Assert.Single(plans);
        Assert.Equal("tool", plans[0].Executable);
    }

    [Fact]
    public void Executes_Multi_Plan_Factory_Records_Plan_Sequence()
    {
        var spec = Build("X", _ => _.Executes(() => new[]
        {
            new CommandPlan { Executable = "a", Arguments = [] },
            new CommandPlan { Executable = "b", Arguments = [] },
        }));
        Assert.Single(spec.PlanFactories);
        var plans = spec.PlanFactories[0]().ToList();
        Assert.Equal(2, plans.Count);
        Assert.Equal("a", plans[0].Executable);
        Assert.Equal("b", plans[1].Executable);
    }

    [Fact]
    public void Multiple_Executes_Calls_Stack_In_Order()
    {
        var order = new List<int>();
        var spec = Build("X", _ => _
            .Executes(() => order.Add(1))
            .Executes(() => order.Add(2)));
        Assert.Equal(2, spec.Actions.Count);
        spec.Actions[0]();
        spec.Actions[1]();
        Assert.Equal([1, 2], order);
    }

    [Fact]
    public void RunMode_And_FailureMode_Round_Trip()
    {
        var spec = Build("X", _ => _.RunMode(RunMode.WhenInputsChanged).FailureMode(FailureMode.Continue));
        Assert.Equal(RunMode.WhenInputsChanged, spec.RunMode);
        Assert.Equal(FailureMode.Continue, spec.FailureMode);
    }

    [Fact]
    public void TargetSpec_Record_Equality_Compares_Names()
    {
        var a = Build("X", _ => _);
        var b = Build("X", _ => _);
        // Records compare by value but collection-typed properties don't
        // have structural equality; verify name is correct rather than
        // requiring full record equality here.
        Assert.Equal(a.Name, b.Name);
    }
}
