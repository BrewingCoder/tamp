using Xunit;

namespace Tamp.DotNetCoverage.V17.Tests;

public sealed class DotNetCoverageTests
{
    private static Tool FakeTool() => new(AbsolutePath.Create("/fake/dotnet-coverage"));

    private static CommandPlan FakeInner(string exe = "dotnet", params string[] args) => new()
    {
        Executable = exe,
        Arguments = args,
    };

    private static int IndexOf(IReadOnlyList<string> args, string value, int start = 0)
    {
        for (var i = start; i < args.Count; i++)
            if (args[i] == value) return i;
        return -1;
    }

    // ---- Collect: argument validation ----

    [Fact]
    public void Collect_Throws_On_Null_Tool()
    {
        Assert.Throws<ArgumentNullException>(() =>
            DotNetCoverage.Collect(null!, FakeInner()));
    }

    [Fact]
    public void Collect_Throws_On_Null_Inner()
    {
        Assert.Throws<ArgumentNullException>(() =>
            DotNetCoverage.Collect(FakeTool(), null!));
    }

    // ---- Collect: shape ----

    [Fact]
    public void Collect_Executable_Is_The_Tool_Path()
    {
        var plan = DotNetCoverage.Collect(FakeTool(), FakeInner("dotnet", "test"));
        Assert.Equal("/fake/dotnet-coverage", plan.Executable);
    }

    [Fact]
    public void Collect_First_Arg_Is_Verb_Then_Inner_Command()
    {
        var plan = DotNetCoverage.Collect(FakeTool(), FakeInner("dotnet", "test", "--no-build"));
        Assert.Equal("collect", plan.Arguments[0]);
        // Default-on --nologo precedes the inner command.
        Assert.Contains("--nologo", plan.Arguments);
        // The wrapped command's executable + args appear after the flags.
        var dotnetIdx = IndexOf(plan.Arguments, "dotnet");
        Assert.True(dotnetIdx >= 0);
        Assert.Equal("test", plan.Arguments[dotnetIdx + 1]);
        Assert.Equal("--no-build", plan.Arguments[dotnetIdx + 2]);
    }

    [Fact]
    public void Collect_Output_Round_Trips()
    {
        var plan = DotNetCoverage.Collect(FakeTool(), FakeInner(),
            c => c.SetOutput("artifacts/test.coverage"));
        var args = plan.Arguments;
        Assert.Equal("artifacts/test.coverage", args[IndexOf(args, "--output") + 1]);
    }

    [Theory]
    [InlineData(CoverageFormat.Coverage, "coverage")]
    [InlineData(CoverageFormat.Xml, "xml")]
    [InlineData(CoverageFormat.Cobertura, "cobertura")]
    [InlineData(CoverageFormat.Lcov, "lcov")]
    public void Collect_OutputFormat_Maps_To_Flag_Value(CoverageFormat format, string expected)
    {
        var plan = DotNetCoverage.Collect(FakeTool(), FakeInner(),
            c => c.SetOutputFormat(format));
        var args = plan.Arguments;
        Assert.Equal(expected, args[IndexOf(args, "--output-format") + 1]);
    }

    [Fact]
    public void Collect_SessionId_And_Settings_Round_Trip()
    {
        var plan = DotNetCoverage.Collect(FakeTool(), FakeInner(),
            c => c.SetSessionId("my-session").SetSettings("/abs/coverage.runsettings"));
        var args = plan.Arguments;
        Assert.Equal("my-session", args[IndexOf(args, "--session-id") + 1]);
        Assert.Equal("/abs/coverage.runsettings", args[IndexOf(args, "--settings") + 1]);
    }

    [Fact]
    public void Collect_IncludeFiles_Emit_As_Repeated_Flag()
    {
        var plan = DotNetCoverage.Collect(FakeTool(), FakeInner(),
            c => c.AddIncludeFile("**/*.dll").AddIncludeFile("src/MyApp/bin/**/*.dll"));
        var args = plan.Arguments;
        var first = IndexOf(args, "--include-files");
        var second = IndexOf(args, "--include-files", first + 1);
        Assert.True(first >= 0 && second > first);
        Assert.Equal("**/*.dll", args[first + 1]);
        Assert.Equal("src/MyApp/bin/**/*.dll", args[second + 1]);
    }

    [Fact]
    public void Collect_NoLogo_On_By_Default()
    {
        var plan = DotNetCoverage.Collect(FakeTool(), FakeInner());
        Assert.Contains("--nologo", plan.Arguments);
    }

    [Fact]
    public void Collect_NoLogo_Can_Be_Disabled()
    {
        var plan = DotNetCoverage.Collect(FakeTool(), FakeInner(),
            c => c.SetNoLogo(false));
        Assert.DoesNotContain("--nologo", plan.Arguments);
    }

    [Fact]
    public void Collect_LogLevel_Maps_To_Title_Case()
    {
        var plan = DotNetCoverage.Collect(FakeTool(), FakeInner(),
            c => c.SetLogLevel(CoverageLogLevel.Verbose));
        var args = plan.Arguments;
        Assert.Equal("Verbose", args[IndexOf(args, "--log-level") + 1]);
    }

    [Fact]
    public void Collect_Preserves_Inner_Environment()
    {
        var inner = new CommandPlan
        {
            Executable = "dotnet",
            Arguments = ["test"],
            Environment = new Dictionary<string, string> { ["MY_INNER"] = "from-inner" },
        };
        var plan = DotNetCoverage.Collect(FakeTool(), inner);
        Assert.Equal("from-inner", plan.Environment["MY_INNER"]);
    }

    [Fact]
    public void Collect_Inner_Secrets_Propagate_To_Wrapper_Plan()
    {
        var token = new Secret("ApiKey", "abc123");
        var inner = new CommandPlan
        {
            Executable = "dotnet",
            Arguments = ["test"],
            Secrets = [token],
        };
        var plan = DotNetCoverage.Collect(FakeTool(), inner);
        Assert.Single(plan.Secrets);
        Assert.Same(token, plan.Secrets[0]);
    }

    [Fact]
    public void Collect_WorkingDirectory_Settings_Wins_Over_Inner_Wins_Over_Tool()
    {
        var tool = new Tool(AbsolutePath.Create("/fake/dc"), workingDirectory: "/from-tool");
        var inner = new CommandPlan
        {
            Executable = "dotnet",
            Arguments = ["test"],
            WorkingDirectory = "/from-inner",
        };
        // No setting on settings → falls through to inner.
        var plan = DotNetCoverage.Collect(tool, inner);
        Assert.Equal("/from-inner", plan.WorkingDirectory);
        // Settings override.
        var plan2 = DotNetCoverage.Collect(tool, inner, c => c.SetWorkingDirectory("/from-settings"));
        Assert.Equal("/from-settings", plan2.WorkingDirectory);
    }

    // ---- Merge: argument validation ----

    [Fact]
    public void Merge_Throws_On_Null_Tool()
    {
        Assert.Throws<ArgumentNullException>(() =>
            DotNetCoverage.Merge(null!, _ => { }));
    }

    [Fact]
    public void Merge_Throws_On_Null_Configurer()
    {
        Assert.Throws<ArgumentNullException>(() =>
            DotNetCoverage.Merge(FakeTool(), null!));
    }

    [Fact]
    public void Merge_Requires_At_Least_One_Input()
    {
        Assert.Throws<InvalidOperationException>(() =>
            DotNetCoverage.Merge(FakeTool(), m => m.SetOutput("merged.coverage")));
    }

    // ---- Merge: shape ----

    [Fact]
    public void Merge_First_Arg_Is_Verb()
    {
        var plan = DotNetCoverage.Merge(FakeTool(), m => m.AddInput("a.coverage"));
        Assert.Equal("merge", plan.Arguments[0]);
    }

    [Fact]
    public void Merge_Inputs_Are_Trailing_Positionals()
    {
        var plan = DotNetCoverage.Merge(FakeTool(), m => m
            .SetOutput("merged.cobertura.xml")
            .SetOutputFormat(CoverageFormat.Cobertura)
            .AddInput("a.coverage")
            .AddInput("b.coverage"));
        // Positional inputs come after the flags.
        var lastFlag = IndexOf(plan.Arguments, "--output-format");
        Assert.True(lastFlag > 0);
        var aIdx = IndexOf(plan.Arguments, "a.coverage");
        var bIdx = IndexOf(plan.Arguments, "b.coverage");
        Assert.True(aIdx > lastFlag);
        Assert.True(bIdx > aIdx);
    }

    [Fact]
    public void Merge_AddInputs_Accepts_AbsolutePath_Sequence()
    {
        var paths = new[]
        {
            AbsolutePath.Create("/abs/a.coverage"),
            AbsolutePath.Create("/abs/b.coverage"),
        };
        var plan = DotNetCoverage.Merge(FakeTool(), m => m
            .AddInputs(paths)
            .SetOutput("/abs/merged.cobertura.xml")
            .SetOutputFormat(CoverageFormat.Cobertura));
        Assert.Contains("/abs/a.coverage", plan.Arguments);
        Assert.Contains("/abs/b.coverage", plan.Arguments);
    }

    [Fact]
    public void Merge_RemoveInputFiles_Becomes_Flag()
    {
        var plan = DotNetCoverage.Merge(FakeTool(), m => m
            .AddInput("a.coverage")
            .SetRemoveInputFiles(true));
        Assert.Contains("--remove-input-files", plan.Arguments);
    }

    [Fact]
    public void Merge_NoLogo_On_By_Default()
    {
        var plan = DotNetCoverage.Merge(FakeTool(), m => m.AddInput("a.coverage"));
        Assert.Contains("--nologo", plan.Arguments);
    }

    [Fact]
    public void Merge_Format_Conversion_Single_Input()
    {
        // The "convert" path: one input + a different output format.
        // dotnet-coverage doesn't have a separate `convert` verb, so
        // single-input merge is how you convert.
        var plan = DotNetCoverage.Merge(FakeTool(), m => m
            .AddInput("test.coverage")
            .SetOutput("test.cobertura.xml")
            .SetOutputFormat(CoverageFormat.Cobertura));
        var args = plan.Arguments;
        Assert.Equal("test.cobertura.xml", args[IndexOf(args, "--output") + 1]);
        Assert.Equal("cobertura", args[IndexOf(args, "--output-format") + 1]);
        Assert.Contains("test.coverage", args);
    }
}
