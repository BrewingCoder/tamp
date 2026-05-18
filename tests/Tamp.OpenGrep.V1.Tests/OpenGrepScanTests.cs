using Xunit;

namespace Tamp.OpenGrep.V1.Tests;

public class OpenGrepScanTests
{
    [Fact]
    public void Minimal_Settings_Produce_Opengrep_Scan_Plan()
    {
        var plan = OpenGrep.Scan(s => s.AddTarget("src/"));

        Assert.Equal("opengrep", plan.Executable);
        Assert.Equal(new[] { "scan", "--sarif", "--disable-version-check", "src/" }, plan.Arguments);
    }

    [Fact]
    public void Sarif_Flag_Is_On_By_Default_For_Wave_1_Chain()
    {
        var plan = OpenGrep.Scan(s => s.AddTarget("."));
        Assert.Contains("--sarif", plan.Arguments);
    }

    [Fact]
    public void Sarif_Disable_Omits_Flag()
    {
        var plan = OpenGrep.Scan(s => s.AddTarget(".").SetSarif(false));
        Assert.DoesNotContain("--sarif", plan.Arguments);
    }

    [Fact]
    public void Version_Check_Disabled_By_Default_For_Air_Gap_Friendliness()
    {
        var plan = OpenGrep.Scan(s => s.AddTarget("."));
        Assert.Contains("--disable-version-check", plan.Arguments);
    }

    [Fact]
    public void Version_Check_Can_Be_Re_Enabled()
    {
        var plan = OpenGrep.Scan(s => s.AddTarget(".").SetDisableVersionCheck(false));
        Assert.DoesNotContain("--disable-version-check", plan.Arguments);
    }

    [Fact]
    public void Configs_Translate_To_Repeated_Config_Flags()
    {
        var plan = OpenGrep.Scan(s => s
            .AddTarget(".")
            .AddConfig("p/owasp-top-ten")
            .AddConfig("./custom-rules"));

        Assert.Equal(2, plan.Arguments.Count(a => a == "--config"));
        Assert.Contains("p/owasp-top-ten", plan.Arguments);
        Assert.Contains("./custom-rules", plan.Arguments);
    }

    [Fact]
    public void Output_File_Translates_To_Output_Flag()
    {
        var plan = OpenGrep.Scan(s => s.AddTarget(".").SetOutputFile("./artifacts/scan.sarif"));

        var args = plan.Arguments.ToList();
        var outputIdx = args.IndexOf("--output");
        Assert.True(outputIdx >= 0);
        Assert.Equal("./artifacts/scan.sarif", args[outputIdx + 1]);
    }

    [Fact]
    public void Baseline_Commit_Translates_To_Equals_Form_Flag()
    {
        var plan = OpenGrep.Scan(s => s.AddTarget(".").SetBaselineCommit("abc1234"));
        Assert.Contains("--baseline-commit=abc1234", plan.Arguments);
    }

    [Theory]
    [InlineData(OpenGrepSeverity.Info, "--severity=INFO")]
    [InlineData(OpenGrepSeverity.Warning, "--severity=WARNING")]
    [InlineData(OpenGrepSeverity.Error, "--severity=ERROR")]
    public void Severity_Threshold_Maps_To_Uppercase_Equals_Form(OpenGrepSeverity severity, string expectedArg)
    {
        var plan = OpenGrep.Scan(s => s.AddTarget(".").SetSeverityThreshold(severity));
        Assert.Contains(expectedArg, plan.Arguments);
    }

    [Fact]
    public void Excludes_Translate_To_Equals_Form_Flags()
    {
        var plan = OpenGrep.Scan(s => s.AddTarget(".").AddExclude("**/bin/**").AddExclude("**/obj/**"));

        Assert.Contains("--exclude=**/bin/**", plan.Arguments);
        Assert.Contains("--exclude=**/obj/**", plan.Arguments);
    }

    [Fact]
    public void Max_Target_Bytes_Emitted_As_Invariant_Decimal_In_Equals_Form()
    {
        var plan = OpenGrep.Scan(s => s.AddTarget(".").SetMaxTargetBytes(5_000_000L));
        Assert.Contains("--max-target-bytes=5000000", plan.Arguments);
    }

    [Fact]
    public void Quiet_Flag_Emitted_When_Set()
    {
        var plan = OpenGrep.Scan(s => s.AddTarget(".").SetQuiet(true));
        Assert.Contains("--quiet", plan.Arguments);
    }

    [Fact]
    public void Multiple_Targets_All_Appended_After_Options()
    {
        var plan = OpenGrep.Scan(s => s.AddTargets(["src/", "tests/", "build/"]));

        // Targets are positional and come last; their order is preserved.
        var lastThree = plan.Arguments.TakeLast(3).ToList();
        Assert.Equal(new[] { "src/", "tests/", "build/" }, lastThree);
    }

    [Fact]
    public void Working_Directory_Propagates_To_Plan()
    {
        var plan = OpenGrep.Scan(s => s.AddTarget(".").SetWorkingDirectory("/repos/some-app"));
        Assert.Equal("/repos/some-app", plan.WorkingDirectory);
    }

    [Fact]
    public void Custom_Environment_Variables_Are_Preserved()
    {
        var plan = OpenGrep.Scan(s =>
        {
            s.AddTarget(".");
            s.EnvironmentVariables["TAMP_OPENGREP_RULES"] = "/etc/opengrep/rules";
        });

        Assert.Equal("/etc/opengrep/rules", plan.Environment["TAMP_OPENGREP_RULES"]);
    }

    [Fact]
    public void Empty_Targets_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => OpenGrep.Scan(_ => { }));
    }

    [Fact]
    public void Null_Configurer_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => OpenGrep.Scan(null!));
    }

    [Fact]
    public void Full_Configuration_Mirrors_Wave_1_CI_Recipe()
    {
        // This is the regression gate: the typical CI invocation should
        // serialise to the args we expect. If a default flips, this test
        // tells us before adopters do.
        var plan = OpenGrep.Scan(s => s
            .AddTarget("src/")
            .AddTarget("tests/")
            .AddConfig("auto")
            .SetOutputFile("./artifacts/opengrep.sarif")
            .SetSeverityThreshold(OpenGrepSeverity.Warning)
            .AddExclude("**/generated/**")
            .SetQuiet(true));

        Assert.Equal("opengrep", plan.Executable);
        Assert.Equal(new[]
        {
            "scan",
            "--config", "auto",
            "--sarif",
            "--output", "./artifacts/opengrep.sarif",
            "--severity=WARNING",
            "--exclude=**/generated/**",
            "--disable-version-check",
            "--quiet",
            "src/", "tests/",
        }, plan.Arguments);
    }
}
