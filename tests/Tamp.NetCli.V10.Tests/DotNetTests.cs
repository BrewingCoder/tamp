using Xunit;

namespace Tamp.NetCli.V10.Tests;

public sealed class DotNetTests
{
    private static int IndexOf(IReadOnlyList<string> args, string value, int start = 0)
    {
        for (var i = start; i < args.Count; i++)
            if (args[i] == value) return i;
        return -1;
    }

    // ---- Common shape ----

    [Fact]
    public void Every_Verb_Targets_The_Dotnet_Executable()
    {
        Assert.Equal("dotnet", DotNet.Restore().Executable);
        Assert.Equal("dotnet", DotNet.Build().Executable);
        Assert.Equal("dotnet", DotNet.Test().Executable);
        Assert.Equal("dotnet", DotNet.Pack().Executable);
        Assert.Equal("dotnet", DotNet.Publish().Executable);
    }

    [Fact]
    public void Every_Verb_Sets_NoLogo_And_TelemetryOptOut()
    {
        foreach (var plan in new[] { DotNet.Restore(), DotNet.Build(), DotNet.Test(), DotNet.Pack(), DotNet.Publish() })
        {
            Assert.Equal("1", plan.Environment["DOTNET_NOLOGO"]);
            Assert.Equal("1", plan.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"]);
        }
    }

    [Fact]
    public void Every_Verb_Begins_With_Its_Verb_Token()
    {
        Assert.Equal("restore", DotNet.Restore().Arguments[0]);
        Assert.Equal("build", DotNet.Build().Arguments[0]);
        Assert.Equal("test", DotNet.Test().Arguments[0]);
        Assert.Equal("pack", DotNet.Pack().Arguments[0]);
        Assert.Equal("publish", DotNet.Publish().Arguments[0]);
    }

    [Fact]
    public void Default_Plan_Has_No_Working_Directory()
    {
        Assert.Null(DotNet.Build().WorkingDirectory);
    }

    // ---- Restore ----

    [Fact]
    public void Restore_Project_Becomes_Positional_Argument()
    {
        var plan = DotNet.Restore(s => s.SetProject("./Foo.csproj"));
        Assert.Equal("./Foo.csproj", plan.Arguments[1]);
    }

    [Fact]
    public void Restore_NoCache_And_Force_Are_Distinct_Flags()
    {
        var plan = DotNet.Restore(s => s.SetNoCache(true).SetForce(true));
        Assert.Contains("--no-cache", plan.Arguments);
        Assert.Contains("--force", plan.Arguments);
    }

    [Fact]
    public void Restore_Sources_Emit_As_Repeated_Source_Flags()
    {
        var plan = DotNet.Restore(s => s.AddSource("https://a/v3/index.json").AddSource("https://b/v3/index.json"));
        var args = plan.Arguments;
        var firstSource = IndexOf(args, "--source");
        var secondSource = IndexOf(args, "--source", firstSource + 1);
        Assert.True(firstSource >= 0);
        Assert.True(secondSource > firstSource);
        Assert.Equal("https://a/v3/index.json", args[firstSource + 1]);
        Assert.Equal("https://b/v3/index.json", args[secondSource + 1]);
    }

    [Fact]
    public void Restore_LockedMode_Implies_Use_Lock_File_Mention_Independently()
    {
        var plan = DotNet.Restore(s => s.SetLockedMode(true).SetUseLockFile(true));
        Assert.Contains("--use-lock-file", plan.Arguments);
        Assert.Contains("--locked-mode", plan.Arguments);
    }

    // ---- Build ----

    [Fact]
    public void Build_Configuration_Maps_To_Title_Case_Argument()
    {
        var plan = DotNet.Build(s => s.SetConfiguration(Configuration.Release));
        var args = plan.Arguments;
        var idx = IndexOf(args, "--configuration");
        Assert.True(idx >= 0);
        Assert.Equal("Release", args[idx + 1]);
    }

    [Fact]
    public void Build_NoRestore_Becomes_Flag()
    {
        var plan = DotNet.Build(s => s.SetNoRestore(true));
        Assert.Contains("--no-restore", plan.Arguments);
    }

    [Fact]
    public void Build_Properties_Emit_As_DashP_Pairs()
    {
        var plan = DotNet.Build(s => s.SetProperty("Version", "1.2.3").SetProperty("Foo", "bar"));
        Assert.Contains("-p:Version=1.2.3", plan.Arguments);
        Assert.Contains("-p:Foo=bar", plan.Arguments);
    }

    [Fact]
    public void Build_Output_And_Framework_Round_Trip()
    {
        var plan = DotNet.Build(s => s.SetOutput("bin/Release").SetFramework("net10.0"));
        var args = plan.Arguments;
        Assert.Equal("bin/Release", args[IndexOf(args, "--output") + 1]);
        Assert.Equal("net10.0", args[IndexOf(args, "--framework") + 1]);
    }

    // ---- Test ----

    [Fact]
    public void Test_NoBuild_Without_NoRestore_Is_Permitted()
    {
        var plan = DotNet.Test(s => s.SetNoBuild(true));
        Assert.Contains("--no-build", plan.Arguments);
        Assert.DoesNotContain("--no-restore", plan.Arguments);
    }

    [Fact]
    public void Test_Filter_And_Loggers_Round_Trip()
    {
        var plan = DotNet.Test(s => s
            .SetFilter("Category=Smoke")
            .AddLogger("trx;LogFileName=t.trx")
            .AddLogger("console;verbosity=normal"));
        var args = plan.Arguments;
        Assert.Equal("Category=Smoke", args[IndexOf(args, "--filter") + 1]);
        var firstLogger = IndexOf(args, "--logger");
        var secondLogger = IndexOf(args, "--logger", firstLogger + 1);
        Assert.Equal("trx;LogFileName=t.trx", args[firstLogger + 1]);
        Assert.Equal("console;verbosity=normal", args[secondLogger + 1]);
    }

    [Fact]
    public void Test_BlameHang_With_Timeout_Emits_Milliseconds()
    {
        var plan = DotNet.Test(s => s.SetBlameHang(true).SetBlameHangTimeout(TimeSpan.FromSeconds(45)));
        var args = plan.Arguments;
        Assert.Contains("--blame-hang", args);
        Assert.Equal("45000ms", args[IndexOf(args, "--blame-hang-timeout") + 1]);
    }

    // ---- Pack ----

    [Fact]
    public void Pack_Output_Round_Trips()
    {
        var plan = DotNet.Pack(s => s.SetConfiguration(Configuration.Release).SetOutput("artifacts"));
        var args = plan.Arguments;
        Assert.Equal("Release", args[IndexOf(args, "--configuration") + 1]);
        Assert.Equal("artifacts", args[IndexOf(args, "--output") + 1]);
    }

    [Fact]
    public void Pack_IncludeSymbols_And_IncludeSource_Are_Independent()
    {
        var plan = DotNet.Pack(s => s.SetIncludeSymbols(true).SetIncludeSource(true));
        Assert.Contains("--include-symbols", plan.Arguments);
        Assert.Contains("--include-source", plan.Arguments);
    }

    // ---- Publish ----

    [Fact]
    public void Publish_SelfContained_Maps_To_Affirmative_Or_Negative_Flag()
    {
        var on = DotNet.Publish(s => s.SetSelfContained(true));
        var off = DotNet.Publish(s => s.SetSelfContained(false));
        Assert.Contains("--self-contained", on.Arguments);
        Assert.Contains("--no-self-contained", off.Arguments);
    }

    [Fact]
    public void Publish_Boolean_MSBuild_Properties_Emit_As_DashP_True_Pairs()
    {
        var plan = DotNet.Publish(s => s
            .SetPublishSingleFile(true)
            .SetPublishTrimmed(true)
            .SetPublishReadyToRun(true));
        Assert.Contains("-p:PublishSingleFile=true", plan.Arguments);
        Assert.Contains("-p:PublishTrimmed=true", plan.Arguments);
        Assert.Contains("-p:PublishReadyToRun=true", plan.Arguments);
    }

    // ---- Verbosity (shared via base) ----

    [Theory]
    [InlineData(DotNetVerbosity.Quiet, "quiet")]
    [InlineData(DotNetVerbosity.Minimal, "minimal")]
    [InlineData(DotNetVerbosity.Normal, "normal")]
    [InlineData(DotNetVerbosity.Detailed, "detailed")]
    [InlineData(DotNetVerbosity.Diagnostic, "diagnostic")]
    public void Verbosity_Round_Trips_For_Build(DotNetVerbosity v, string expected)
    {
        var plan = DotNet.Build(s => s.SetVerbosity(v));
        var args = plan.Arguments;
        Assert.Equal(expected, args[IndexOf(args, "--verbosity") + 1]);
    }

    [Fact]
    public void Verbosity_Round_Trips_For_All_Verbs()
    {
        // Sanity that the shared base is wired into every settings class.
        var verbs = new CommandPlan[]
        {
            DotNet.Restore(s => s.SetVerbosity(DotNetVerbosity.Detailed)),
            DotNet.Build(s => s.SetVerbosity(DotNetVerbosity.Detailed)),
            DotNet.Test(s => s.SetVerbosity(DotNetVerbosity.Detailed)),
            DotNet.Pack(s => s.SetVerbosity(DotNetVerbosity.Detailed)),
            DotNet.Publish(s => s.SetVerbosity(DotNetVerbosity.Detailed)),
        };
        foreach (var plan in verbs)
        {
            Assert.Contains("--verbosity", plan.Arguments);
            Assert.Contains("detailed", plan.Arguments);
        }
    }

    [Fact]
    public void Working_Directory_Round_Trips()
    {
        var plan = DotNet.Build(s => s.SetWorkingDirectory("/tmp/work"));
        Assert.Equal("/tmp/work", plan.WorkingDirectory);
    }

    [Fact]
    public void Null_Configurer_Produces_Default_Plan()
    {
        var plan = DotNet.Build();
        Assert.Equal(["build"], plan.Arguments);
    }

    [Fact]
    public void Custom_Environment_Variable_Survives_Plus_Defaults_Stay_Set()
    {
        var plan = DotNet.Build(s => s.EnvironmentVariables["MY_VAR"] = "hello");
        Assert.Equal("hello", plan.Environment["MY_VAR"]);
        Assert.Equal("1", plan.Environment["DOTNET_NOLOGO"]);
    }
}
