using Xunit;

namespace Tamp.Core.Tests;

/// <summary>
/// Tests for <c>Requires(Func&lt;bool&gt;)</c> hard preconditions and
/// <c>AssuredAfterFailure()</c> cleanup semantics.
/// </summary>
public sealed class RequiresAndAssuredTests
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

    // ---- Requires ----

    [Fact]
    public void Requires_True_Allows_Target_To_Run()
    {
        var ran = false;
        var (exec, _) = Build(Specs(new TargetSpec
        {
            Name = "Compile",
            Requirements = new[] { new TargetCondition(() => true, "Solution != null") },
            Actions = new Action[] { () => ran = true },
        }));
        var result = exec.Run("Compile");
        Assert.Equal(0, result.ExitCode);
        Assert.True(ran);
    }

    [Fact]
    public void Requires_False_Aborts_Build_With_Failed_Status()
    {
        var ran = false;
        var (exec, sw) = Build(Specs(new TargetSpec
        {
            Name = "Compile",
            Requirements = new[] { new TargetCondition(() => false, "Solution != null") },
            Actions = new Action[] { () => ran = true },
        }));
        var result = exec.Run("Compile");
        Assert.NotEqual(0, result.ExitCode);
        Assert.False(ran);
        Assert.Equal("Compile", result.FailedTarget);
        Assert.Contains("REQUIRES failed", sw.ToString());
        Assert.Contains("Solution != null", sw.ToString());
    }

    [Fact]
    public void Requires_Throwing_Is_Treated_As_Failure_With_Exception_Detail()
    {
        var (exec, sw) = Build(Specs(new TargetSpec
        {
            Name = "Compile",
            Requirements = new[] { new TargetCondition(() => throw new InvalidOperationException("oops"), "predicate") },
        }));
        var result = exec.Run("Compile");
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("InvalidOperationException", sw.ToString());
        Assert.Contains("oops", sw.ToString());
    }

    [Fact]
    public void Requires_All_Conditions_Must_Hold()
    {
        var (exec, _) = Build(Specs(new TargetSpec
        {
            Name = "Compile",
            Requirements = new[]
            {
                new TargetCondition(() => true, "first"),
                new TargetCondition(() => false, "second"),
            },
        }));
        var result = exec.Run("Compile");
        Assert.NotEqual(0, result.ExitCode);
        Assert.Equal("Compile", result.FailedTarget);
    }

    [Fact]
    public void Requires_Failure_Triggers_OnFailureOf_Handlers()
    {
        var notified = false;
        var (exec, _) = Build(Specs(
            new TargetSpec
            {
                Name = "Deploy",
                Requirements = new[] { new TargetCondition(() => false, "creds set") },
            },
            new TargetSpec
            {
                Name = "Notify",
                OnFailureOf = new[] { "Deploy" },
                Actions = new Action[] { () => notified = true },
            }));
        var result = exec.Run("Deploy");
        Assert.NotEqual(0, result.ExitCode);
        Assert.True(notified);
        Assert.Contains("Notify", result.FailureHandlersInvoked);
    }

    // ---- AssuredAfterFailure ----

    [Fact]
    public void AssuredAfterFailure_Target_Runs_After_Earlier_Target_Fails()
    {
        var cleanedUp = false;
        var (exec, _) = Build(Specs(
            new TargetSpec
            {
                Name = "Compile",
                Actions = new Action[] { () => throw new InvalidOperationException("boom") },
            },
            new TargetSpec
            {
                Name = "Cleanup",
                Dependencies = new[] { "Compile" },
                AssuredAfterFailure = true,
                Actions = new Action[] { () => cleanedUp = true },
            }));
        var result = exec.Run("Cleanup");
        Assert.NotEqual(0, result.ExitCode);
        Assert.Equal("Compile", result.FailedTarget);
        Assert.True(cleanedUp);
    }

    [Fact]
    public void NonAssured_Target_After_Failed_Target_Is_NotRun()
    {
        var ran = false;
        var (exec, _) = Build(Specs(
            new TargetSpec
            {
                Name = "Compile",
                Actions = new Action[] { () => throw new InvalidOperationException("boom") },
            },
            new TargetSpec
            {
                Name = "Test",
                Dependencies = new[] { "Compile" },
                Actions = new Action[] { () => ran = true },
            }));
        var result = exec.Run("Test");
        Assert.NotEqual(0, result.ExitCode);
        Assert.False(ran);
        var testRecord = result.ExecutionRecords.Single(r => r.Name == "Test");
        Assert.Equal(TargetStatus.NotRun, testRecord.Status);
    }

    [Fact]
    public void AssuredAfterFailure_Target_Failure_Does_Not_Reverse_Original()
    {
        var (exec, _) = Build(Specs(
            new TargetSpec
            {
                Name = "Compile",
                Actions = new Action[] { () => throw new InvalidOperationException("compile failed") },
            },
            new TargetSpec
            {
                Name = "Cleanup",
                Dependencies = new[] { "Compile" },
                AssuredAfterFailure = true,
                Actions = new Action[] { () => throw new InvalidOperationException("cleanup also failed") },
            }));
        var result = exec.Run("Cleanup");
        Assert.NotEqual(0, result.ExitCode);
        Assert.Equal("Compile", result.FailedTarget);  // original failure preserved
    }

    [Fact]
    public void Multiple_Assured_Targets_All_Run_After_Failure()
    {
        var c1 = false;
        var c2 = false;
        var (exec, _) = Build(Specs(
            new TargetSpec
            {
                Name = "Compile",
                Actions = new Action[] { () => throw new InvalidOperationException("boom") },
            },
            new TargetSpec
            {
                Name = "Cleanup1",
                Dependencies = new[] { "Compile" },
                AssuredAfterFailure = true,
                Actions = new Action[] { () => c1 = true },
            },
            new TargetSpec
            {
                Name = "Cleanup2",
                Dependencies = new[] { "Cleanup1" },
                AssuredAfterFailure = true,
                Actions = new Action[] { () => c2 = true },
            }));
        var result = exec.Run("Cleanup2");
        Assert.NotEqual(0, result.ExitCode);
        Assert.True(c1);
        Assert.True(c2);
    }

    // ---- Build summary ----

    [Fact]
    public void Build_Summary_Prints_Status_Per_Target()
    {
        var (exec, sw) = Build(Specs(
            new TargetSpec { Name = "A", Actions = new Action[] { () => { } } },
            new TargetSpec
            {
                Name = "B",
                Dependencies = new[] { "A" },
                OnlyWhenConditions = new[] { new TargetCondition(() => false, "off") },
            },
            new TargetSpec { Name = "C", Dependencies = new[] { "B" }, Actions = new Action[] { () => { } } }));
        exec.Run("C");
        var output = sw.ToString();
        Assert.Contains("Build Summary", output);
        Assert.Contains("A", output);
        Assert.Contains("B", output);
        Assert.Contains("C", output);
        Assert.Contains("Done", output);
        Assert.Contains("Skipped", output);
        Assert.Contains("Total", output);
    }

    [Fact]
    public void Execution_Records_Track_Status_Per_Target()
    {
        var (exec, _) = Build(Specs(
            new TargetSpec { Name = "A", Actions = new Action[] { () => { } } },
            new TargetSpec
            {
                Name = "B",
                Dependencies = new[] { "A" },
                OnlyWhenConditions = new[] { new TargetCondition(() => false, "off") },
            }));
        var result = exec.Run("B");
        Assert.Equal(TargetStatus.Done, result.ExecutionRecords.Single(r => r.Name == "A").Status);
        Assert.Equal(TargetStatus.Skipped, result.ExecutionRecords.Single(r => r.Name == "B").Status);
    }

    [Fact]
    public void Execution_Records_Show_Failed_And_NotRun_After_Failure()
    {
        var (exec, _) = Build(Specs(
            new TargetSpec { Name = "A", Actions = new Action[] { () => throw new InvalidOperationException("x") } },
            new TargetSpec { Name = "B", Dependencies = new[] { "A" }, Actions = new Action[] { () => { } } }));
        var result = exec.Run("B");
        Assert.Equal(TargetStatus.Failed, result.ExecutionRecords.Single(r => r.Name == "A").Status);
        Assert.Equal(TargetStatus.NotRun, result.ExecutionRecords.Single(r => r.Name == "B").Status);
    }

    [Fact]
    public void Total_Duration_Is_Reported()
    {
        var (exec, _) = Build(Specs(
            new TargetSpec { Name = "A", Actions = new Action[] { () => { } } }));
        var result = exec.Run("A");
        Assert.True(result.Duration > TimeSpan.Zero);
    }

    [Fact]
    public void Duration_Per_Target_Is_Captured()
    {
        var (exec, _) = Build(Specs(
            new TargetSpec
            {
                Name = "A",
                Actions = new Action[] { () => System.Threading.Thread.Sleep(10) },
            }));
        var result = exec.Run("A");
        var record = Assert.Single(result.ExecutionRecords);
        Assert.True(record.Duration >= TimeSpan.FromMilliseconds(5));
    }
}
