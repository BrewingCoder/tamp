using Xunit;

namespace Tamp.Core.Tests;

/// <summary>
/// Executor-level tests for OnlyWhen conditional skipping and OnFailureOf
/// catch handlers. These exercise the full Executor → TargetGraph pipeline.
/// </summary>
public sealed class OnlyWhenAndFailureHandlerTests
{
    private static (Executor Exec, StringWriter Out) Build(IReadOnlyDictionary<string, TargetSpec> specs, ExecutionMode mode = ExecutionMode.Run)
    {
        var sw = new StringWriter();
        return (new Executor(new TargetGraph(specs), mode, sw), sw);
    }

    private static IReadOnlyDictionary<string, TargetSpec> Specs(params TargetSpec[] entries)
    {
        var d = new Dictionary<string, TargetSpec>(StringComparer.Ordinal);
        foreach (var s in entries) d[s.Name] = s;
        return d;
    }

    // ---- OnlyWhen ----

    [Fact]
    public void OnlyWhen_True_Allows_Target_To_Run()
    {
        var ran = false;
        var (exec, _) = Build(Specs(new TargetSpec
        {
            Name = "Compile",
            OnlyWhenConditions = new[] { new TargetCondition(() => true, "config != null") },
            Actions = new Action[] { () => ran = true },
        }));
        var result = exec.Run("Compile");
        Assert.Equal(0, result.ExitCode);
        Assert.True(ran);
        Assert.Empty(result.SkippedTargets);
    }

    [Fact]
    public void OnlyWhen_False_Skips_Target_And_Records_Skip()
    {
        var ran = false;
        var (exec, _) = Build(Specs(new TargetSpec
        {
            Name = "Compile",
            OnlyWhenConditions = new[] { new TargetCondition(() => false, "config != null") },
            Actions = new Action[] { () => ran = true },
        }));
        var result = exec.Run("Compile");
        Assert.Equal(0, result.ExitCode);
        Assert.False(ran);
        Assert.Contains("Compile", result.SkippedTargets);
    }

    [Fact]
    public void OnlyWhen_All_Conditions_Must_Hold_To_Run()
    {
        var ran = false;
        var (exec, _) = Build(Specs(new TargetSpec
        {
            Name = "Compile",
            OnlyWhenConditions = new[]
            {
                new TargetCondition(() => true, "first"),
                new TargetCondition(() => false, "second"),
            },
            Actions = new Action[] { () => ran = true },
        }));
        var result = exec.Run("Compile");
        Assert.False(ran);
        Assert.Contains("Compile", result.SkippedTargets);
    }

    [Fact]
    public void OnlyWhen_Skip_Reason_Is_The_Expression_Text()
    {
        var (exec, sw) = Build(Specs(new TargetSpec
        {
            Name = "Compile",
            OnlyWhenConditions = new[] { new TargetCondition(() => false, "BuildFlag.Enabled") },
        }));
        exec.Run("Compile");
        Assert.Contains("BuildFlag.Enabled", sw.ToString());
    }

    [Fact]
    public void OnlyWhen_Throwing_Predicate_Is_Treated_As_Skip()
    {
        var (exec, sw) = Build(Specs(new TargetSpec
        {
            Name = "Compile",
            OnlyWhenConditions = new[] { new TargetCondition(() => throw new InvalidOperationException("boom"), "predicate") },
        }));
        var result = exec.Run("Compile");
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Compile", result.SkippedTargets);
        Assert.Contains("InvalidOperationException", sw.ToString());
    }

    [Fact]
    public void OnlyWhen_DryRun_Reports_Skip()
    {
        var (exec, sw) = Build(Specs(new TargetSpec
        {
            Name = "Compile",
            OnlyWhenConditions = new[] { new TargetCondition(() => false, "off") },
            PlanFactories = new Func<IEnumerable<CommandPlan>>[]
            {
                () => new[] { new CommandPlan { Executable = "tool", Arguments = new[] { "x" } } },
            },
        }), ExecutionMode.DryRun);
        exec.Run("Compile");
        Assert.Contains("[skipped]", sw.ToString());
    }

    // ---- OnFailureOf ----

    [Fact]
    public void Failure_Handler_Runs_When_Target_Fails()
    {
        var notified = false;
        var (exec, _) = Build(Specs(
            new TargetSpec
            {
                Name = "Deploy",
                Actions = new Action[] { () => throw new InvalidOperationException("deploy failed") },
            },
            new TargetSpec
            {
                Name = "Notify",
                OnFailureOf = new[] { "Deploy" },
                Actions = new Action[] { () => notified = true },
            }));

        var result = exec.Run("Deploy");
        Assert.NotEqual(0, result.ExitCode);
        Assert.Equal("Deploy", result.FailedTarget);
        Assert.True(notified);
        Assert.Contains("Notify", result.FailureHandlersInvoked);
    }

    [Fact]
    public void Failure_Handler_Does_Not_Run_When_Target_Succeeds()
    {
        var notified = false;
        var (exec, _) = Build(Specs(
            new TargetSpec { Name = "Deploy", Actions = new Action[] { () => { } } },
            new TargetSpec
            {
                Name = "Notify",
                OnFailureOf = new[] { "Deploy" },
                Actions = new Action[] { () => notified = true },
            }));

        var result = exec.Run("Deploy");
        Assert.Equal(0, result.ExitCode);
        Assert.False(notified);
        Assert.Empty(result.FailureHandlersInvoked);
    }

    [Fact]
    public void Failure_Handler_Does_Not_Reverse_Build_Failure()
    {
        var (exec, _) = Build(Specs(
            new TargetSpec
            {
                Name = "Deploy",
                Actions = new Action[] { () => throw new InvalidOperationException("boom") },
            },
            new TargetSpec
            {
                Name = "Notify",
                OnFailureOf = new[] { "Deploy" },
                Actions = new Action[] { () => { /* succeeds */ } },
            }));

        var result = exec.Run("Deploy");
        Assert.NotEqual(0, result.ExitCode);
        Assert.Equal("Deploy", result.FailedTarget);
    }

    [Fact]
    public void Failure_Handler_Failing_Does_Not_Reverse_Build_Failure_Either()
    {
        var (exec, sw) = Build(Specs(
            new TargetSpec
            {
                Name = "Deploy",
                Actions = new Action[] { () => throw new InvalidOperationException("deploy failed") },
            },
            new TargetSpec
            {
                Name = "Notify",
                OnFailureOf = new[] { "Deploy" },
                Actions = new Action[] { () => throw new InvalidOperationException("notify also failed") },
            }));

        var result = exec.Run("Deploy");
        Assert.NotEqual(0, result.ExitCode);
        Assert.Equal("Deploy", result.FailedTarget);
        Assert.Contains("Notify", result.FailureHandlersInvoked);
        Assert.Contains("original failure stands", sw.ToString());
    }

    [Fact]
    public void Multiple_Failure_Handlers_For_Same_Target_All_Run()
    {
        var notified = 0;
        var cleaned = 0;
        var (exec, _) = Build(Specs(
            new TargetSpec
            {
                Name = "Deploy",
                Actions = new Action[] { () => throw new InvalidOperationException("boom") },
            },
            new TargetSpec
            {
                Name = "Notify",
                OnFailureOf = new[] { "Deploy" },
                Actions = new Action[] { () => notified++ },
            },
            new TargetSpec
            {
                Name = "Cleanup",
                OnFailureOf = new[] { "Deploy" },
                Actions = new Action[] { () => cleaned++ },
            }));

        var result = exec.Run("Deploy");
        Assert.NotEqual(0, result.ExitCode);
        Assert.Equal(1, notified);
        Assert.Equal(1, cleaned);
    }

    [Fact]
    public void Failure_Handler_Pulls_Its_Own_Dependencies()
    {
        var prepRan = false;
        var notifyRan = false;
        var (exec, _) = Build(Specs(
            new TargetSpec
            {
                Name = "Deploy",
                Actions = new Action[] { () => throw new InvalidOperationException("boom") },
            },
            new TargetSpec
            {
                Name = "PrepareNotify",
                Actions = new Action[] { () => prepRan = true },
            },
            new TargetSpec
            {
                Name = "Notify",
                OnFailureOf = new[] { "Deploy" },
                Dependencies = new[] { "PrepareNotify" },
                Actions = new Action[] { () => notifyRan = true },
            }));

        var result = exec.Run("Deploy");
        Assert.NotEqual(0, result.ExitCode);
        Assert.True(prepRan, "Handler dep PrepareNotify should run before handler");
        Assert.True(notifyRan);
    }

    [Fact]
    public void Failure_Handler_For_Different_Target_Does_Not_Run()
    {
        var notified = false;
        var (exec, _) = Build(Specs(
            new TargetSpec
            {
                Name = "Deploy",
                Actions = new Action[] { () => throw new InvalidOperationException("boom") },
            },
            new TargetSpec
            {
                Name = "Migrate",
                Actions = new Action[] { () => { } },
            },
            new TargetSpec
            {
                Name = "Notify",
                OnFailureOf = new[] { "Migrate" },  // only catches Migrate, not Deploy
                Actions = new Action[] { () => notified = true },
            }));

        var result = exec.Run("Deploy");
        Assert.NotEqual(0, result.ExitCode);
        Assert.False(notified);
    }
}
