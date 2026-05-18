using Xunit;

namespace Tamp.Trivy.Tests;

public class TrivyFilesystemTests
{
    [Fact]
    public void Minimal_Settings_Produce_Fs_Scan_Plan()
    {
        var plan = Trivy.ScanFilesystem(s => s.SetPath("./src"));

        Assert.Equal("trivy", plan.Executable);
        Assert.Equal(new[] { "fs", "--format", "sarif", "./src" }, plan.Arguments);
    }

    [Fact]
    public void Wave_1_Secrets_Plus_Misconfig_Recipe()
    {
        // The shape Tamp's own SecurityScanTrivy target uses — pinned here
        // as a regression gate. A flipped default surfaces here first.
        var plan = Trivy.ScanFilesystem(s => s
            .SetPath(".")
            .AddScanner(TrivyScanner.Secret)
            .AddScanner(TrivyScanner.Misconfig)
            .SetOutputFile("./artifacts/security/tamp-trivy.sarif")
            .AddSkipDir("artifacts")
            .AddSkipDir("**/bin/**")
            .AddSkipDir("**/obj/**")
            .SetQuiet(true)
            .SetNoProgress(true));

        Assert.Equal("trivy", plan.Executable);
        Assert.Equal(new[]
        {
            "fs",
            "--format", "sarif",
            "--output", "./artifacts/security/tamp-trivy.sarif",
            "--scanners", "secret,misconfig",
            "--quiet",
            "--no-progress",
            "--skip-dirs", "artifacts",
            "--skip-dirs", "**/bin/**",
            "--skip-dirs", "**/obj/**",
            ".",
        }, plan.Arguments);
    }

    [Fact]
    public void Skip_Dirs_And_Skip_Files_Repeat()
    {
        var plan = Trivy.ScanFilesystem(s => s
            .SetPath(".")
            .AddSkipDir("vendor")
            .AddSkipDir("node_modules")
            .AddSkipFile(".secrets.baseline"));

        Assert.Equal(2, plan.Arguments.Count(a => a == "--skip-dirs"));
        Assert.Equal(1, plan.Arguments.Count(a => a == "--skip-files"));
    }

    [Fact]
    public void Scanners_Default_Empty_Lets_Trivy_Pick_Defaults()
    {
        // Per Trivy docs the fs subcommand defaults to vuln,secret when --scanners
        // is omitted. We don't impose; adopter passes the list they want.
        var plan = Trivy.ScanFilesystem(s => s.SetPath("."));
        Assert.DoesNotContain("--scanners", plan.Arguments);
    }

    [Fact]
    public void Ignore_Unfixed_Combined_With_Severity_For_CI_Gating()
    {
        var plan = Trivy.ScanFilesystem(s => s
            .SetPath(".")
            .AddSeverity(TrivySeverity.High)
            .AddSeverity(TrivySeverity.Critical)
            .SetIgnoreUnfixed(true)
            .SetExitCode(1));

        Assert.Contains("--ignore-unfixed", plan.Arguments);
        Assert.Contains("--severity", plan.Arguments);
        Assert.Contains("HIGH,CRITICAL", plan.Arguments);
        Assert.Contains("--exit-code", plan.Arguments);
        Assert.Contains("1", plan.Arguments);
    }

    [Fact]
    public void Working_Directory_And_Env_Propagate()
    {
        var plan = Trivy.ScanFilesystem(s =>
        {
            s.SetPath(".").SetWorkingDirectory("/repos/some-app");
            s.EnvironmentVariables["TRIVY_NO_PROGRESS"] = "true";
        });

        Assert.Equal("/repos/some-app", plan.WorkingDirectory);
        Assert.Equal("true", plan.Environment["TRIVY_NO_PROGRESS"]);
    }

    [Fact]
    public void Missing_Path_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => Trivy.ScanFilesystem(_ => { }));
    }

    [Fact]
    public void Null_Configurer_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => Trivy.ScanFilesystem(null!));
    }
}
