using Xunit;

namespace Tamp.Trivy.Tests;

public class TrivyConfigTests
{
    [Fact]
    public void Minimal_Settings_Produce_Config_Scan_Plan()
    {
        var plan = Trivy.ScanConfig(s => s.SetTarget("./infra"));

        Assert.Equal("trivy", plan.Executable);
        Assert.Equal(new[] { "config", "--format", "sarif", "./infra" }, plan.Arguments);
    }

    [Fact]
    public void Multiple_Config_Check_Paths_Emit_Repeated_Flags()
    {
        var plan = Trivy.ScanConfig(s => s
            .SetTarget("./terraform")
            .AddConfigCheckPath("./checks/policy.rego")
            .AddConfigCheckPath("./checks/extra"));

        Assert.Equal(2, plan.Arguments.Count(a => a == "--config-check"));
        Assert.Contains("./checks/policy.rego", plan.Arguments);
        Assert.Contains("./checks/extra", plan.Arguments);
    }

    [Fact]
    public void Skip_Dirs_And_Files_Emit_Repeated_Flags()
    {
        var plan = Trivy.ScanConfig(s => s
            .SetTarget(".")
            .AddSkipDir("**/.terraform/**")
            .AddSkipDir("vendor/**")
            .AddSkipFile("**/*.generated.yaml"));

        Assert.Equal(2, plan.Arguments.Count(a => a == "--skip-dirs"));
        Assert.Equal(1, plan.Arguments.Count(a => a == "--skip-files"));
        Assert.Contains("**/.terraform/**", plan.Arguments);
        Assert.Contains("vendor/**", plan.Arguments);
        Assert.Contains("**/*.generated.yaml", plan.Arguments);
    }

    [Fact]
    public void Include_Non_Failures_Flag_Emitted_When_Set()
    {
        var plan = Trivy.ScanConfig(s => s.SetTarget(".").SetIncludeNonFailures(true));
        Assert.Contains("--include-non-failures", plan.Arguments);
    }

    [Fact]
    public void Output_File_And_Severity_Filter()
    {
        var plan = Trivy.ScanConfig(s => s
            .SetTarget("./k8s")
            .SetOutputFile("./artifacts/iac.sarif")
            .AddSeverity(TrivySeverity.Medium)
            .AddSeverity(TrivySeverity.High)
            .AddSeverity(TrivySeverity.Critical));

        Assert.Contains("--output", plan.Arguments);
        Assert.Contains("./artifacts/iac.sarif", plan.Arguments);
        var args = plan.Arguments.ToList();
        var idx = args.IndexOf("--severity");
        Assert.Equal("MEDIUM,HIGH,CRITICAL", args[idx + 1]);
    }

    [Fact]
    public void Missing_Target_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => Trivy.ScanConfig(_ => { }));
    }

    [Fact]
    public void Positional_Target_Comes_Last()
    {
        var plan = Trivy.ScanConfig(s => s
            .SetTarget("./infra")
            .AddConfigCheckPath("./checks")
            .AddSkipDir("vendor"));

        Assert.Equal("./infra", plan.Arguments[^1]);
    }
}
