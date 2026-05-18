using Xunit;

namespace Tamp.OsvScanner.V2.Tests;

public class OsvScannerScanTests
{
    [Fact]
    public void Sbom_Only_Settings_Produce_Scan_Source_Sbom_Plan()
    {
        var plan = OsvScanner.ScanSource(s => s.SetSbomFile("artifacts/security/tamp-bom.json"));

        Assert.Equal("osv-scanner", plan.Executable);
        Assert.Equal(new[]
        {
            "scan", "source",
            "--sbom", "artifacts/security/tamp-bom.json",
            "--format", "sarif",
            "--allow-no-lockfiles",
        }, plan.Arguments);
    }

    [Fact]
    public void Sarif_Format_Is_Default_For_Wave_1_Chain()
    {
        var plan = OsvScanner.ScanSource(s => s.SetSbomFile("bom.json"));
        var args = plan.Arguments.ToList();
        var idx = args.IndexOf("--format");
        Assert.True(idx >= 0);
        Assert.Equal("sarif", args[idx + 1]);
    }

    [Theory]
    [InlineData(OsvScannerFormat.Sarif, "sarif")]
    [InlineData(OsvScannerFormat.Json, "json")]
    [InlineData(OsvScannerFormat.Markdown, "markdown")]
    [InlineData(OsvScannerFormat.Html, "html")]
    [InlineData(OsvScannerFormat.CycloneDx14, "cyclonedx-1-4")]
    [InlineData(OsvScannerFormat.CycloneDx15, "cyclonedx-1-5")]
    [InlineData(OsvScannerFormat.Spdx23, "spdx-2-3")]
    [InlineData(OsvScannerFormat.GhAnnotations, "gh-annotations")]
    public void Format_Maps_To_Documented_Wire_Value(OsvScannerFormat format, string expectedWire)
    {
        var plan = OsvScanner.ScanSource(s => s.SetSbomFile("bom.json").SetFormat(format));
        var args = plan.Arguments.ToList();
        var idx = args.IndexOf("--format");
        Assert.True(idx >= 0);
        Assert.Equal(expectedWire, args[idx + 1]);
    }

    [Fact]
    public void Table_Format_Omits_Format_Flag_Tool_Default()
    {
        var plan = OsvScanner.ScanSource(s => s.SetSbomFile("bom.json").SetFormat(OsvScannerFormat.Table));
        Assert.DoesNotContain("--format", plan.Arguments);
    }

    [Fact]
    public void Multiple_Lockfiles_Translate_To_Repeated_Lockfile_Flags()
    {
        var plan = OsvScanner.ScanSource(s => s
            .AddLockfile("package-lock.json")
            .AddLockfile("requirements.txt")
            .AddLockfile("Cargo.lock"));

        Assert.Equal(3, plan.Arguments.Count(a => a == "--lockfile"));
        Assert.Contains("package-lock.json", plan.Arguments);
        Assert.Contains("requirements.txt", plan.Arguments);
        Assert.Contains("Cargo.lock", plan.Arguments);
    }

    [Fact]
    public void Output_File_Translates_To_Output_File_Flag()
    {
        var plan = OsvScanner.ScanSource(s => s
            .SetSbomFile("bom.json")
            .SetOutputFile("artifacts/security/tamp-cve.sarif"));

        var args = plan.Arguments.ToList();
        var idx = args.IndexOf("--output-file");
        Assert.True(idx >= 0);
        Assert.Equal("artifacts/security/tamp-cve.sarif", args[idx + 1]);
    }

    [Fact]
    public void Scan_Directories_Are_Positional_And_Appended_Last()
    {
        var plan = OsvScanner.ScanSource(s => s
            .AddScanDirectory("frontend/")
            .AddScanDirectory("services/api/"));

        var args = plan.Arguments.ToList();
        var lastTwo = args.TakeLast(2).ToList();
        Assert.Equal(new[] { "frontend/", "services/api/" }, lastTwo);
    }

    [Theory]
    [InlineData(OsvScannerDataSource.DepsDev, false)] // tool default — omit
    [InlineData(OsvScannerDataSource.Native, true)]
    public void Data_Source_Emits_Flag_Only_When_Non_Default(OsvScannerDataSource ds, bool shouldEmit)
    {
        var plan = OsvScanner.ScanSource(s => s.SetSbomFile("bom.json").SetDataSource(ds));
        var hasFlag = plan.Arguments.Contains("--data-source");
        Assert.Equal(shouldEmit, hasFlag);
        if (shouldEmit)
        {
            var idx = plan.Arguments.ToList().IndexOf("--data-source");
            Assert.Equal("native", plan.Arguments[idx + 1]);
        }
    }

    [Fact]
    public void Recursive_And_NoIgnore_And_IncludeGitRoot_And_NoResolve_All_Emit_When_Set()
    {
        var plan = OsvScanner.ScanSource(s => s
            .SetSbomFile("bom.json")
            .SetRecursive(true)
            .SetNoIgnore(true)
            .SetIncludeGitRoot(true)
            .SetNoResolve(true));

        Assert.Contains("--recursive", plan.Arguments);
        Assert.Contains("--no-ignore", plan.Arguments);
        Assert.Contains("--include-git-root", plan.Arguments);
        Assert.Contains("--no-resolve", plan.Arguments);
    }

    [Fact]
    public void Allow_No_Lockfiles_Is_On_By_Default()
    {
        // Wave 1 stance: SBOM-only scans are the common path, no lockfiles
        // should not be a failure. Adopter explicitly opts out for stricter
        // monorepo-style scans.
        var plan = OsvScanner.ScanSource(s => s.SetSbomFile("bom.json"));
        Assert.Contains("--allow-no-lockfiles", plan.Arguments);
    }

    [Fact]
    public void Allow_No_Lockfiles_Can_Be_Disabled()
    {
        var plan = OsvScanner.ScanSource(s => s.SetSbomFile("bom.json").SetAllowNoLockfiles(false));
        Assert.DoesNotContain("--allow-no-lockfiles", plan.Arguments);
    }

    [Fact]
    public void Offline_Flags_Cover_Air_Gap_Use_Cases()
    {
        var plan = OsvScanner.ScanSource(s => s
            .SetSbomFile("bom.json")
            .SetOffline(true)
            .SetOfflineVulnerabilities(true)
            .SetDownloadOfflineDatabases(true));

        Assert.Contains("--offline", plan.Arguments);
        Assert.Contains("--offline-vulnerabilities", plan.Arguments);
        Assert.Contains("--download-offline-databases", plan.Arguments);
    }

    [Fact]
    public void Config_File_Translates_To_Config_Flag()
    {
        var plan = OsvScanner.ScanSource(s => s.SetSbomFile("bom.json").SetConfigFile("./osv-scanner.toml"));
        var args = plan.Arguments.ToList();
        var idx = args.IndexOf("--config");
        Assert.True(idx >= 0);
        Assert.Equal("./osv-scanner.toml", args[idx + 1]);
    }

    [Fact]
    public void Exclude_Patterns_Translate_To_Repeated_Experimental_Exclude_Flags()
    {
        var plan = OsvScanner.ScanSource(s => s
            .SetSbomFile("bom.json")
            .AddExcludePattern("g:**/test/**")
            .AddExcludePattern("r:.*\\.generated\\.json$"));

        Assert.Equal(2, plan.Arguments.Count(a => a == "--experimental-exclude"));
        Assert.Contains("g:**/test/**", plan.Arguments);
        Assert.Contains("r:.*\\.generated\\.json$", plan.Arguments);
    }

    [Fact]
    public void All_Vulns_Flag_Emitted_When_Set()
    {
        var plan = OsvScanner.ScanSource(s => s.SetSbomFile("bom.json").SetAllVulns(true));
        Assert.Contains("--all-vulns", plan.Arguments);
    }

    [Fact]
    public void Working_Directory_Propagates_To_Plan()
    {
        var plan = OsvScanner.ScanSource(s => s.SetSbomFile("bom.json").SetWorkingDirectory("/repos/some-app"));
        Assert.Equal("/repos/some-app", plan.WorkingDirectory);
    }

    [Fact]
    public void Custom_Environment_Variables_Are_Preserved()
    {
        var plan = OsvScanner.ScanSource(s =>
        {
            s.SetSbomFile("bom.json");
            s.EnvironmentVariables["HTTPS_PROXY"] = "http://corp-proxy:3128";
        });

        Assert.Equal("http://corp-proxy:3128", plan.Environment["HTTPS_PROXY"]);
    }

    [Fact]
    public void No_Inputs_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => OsvScanner.ScanSource(_ => { }));
    }

    [Fact]
    public void Null_Configurer_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => OsvScanner.ScanSource(null!));
    }

    [Fact]
    public void Full_Wave_2_Recipe_Mirrors_Sbom_To_Sarif_Use_Case()
    {
        // The headline shape adopters will copy-paste. Pin it as a regression
        // gate — a flipped default surfaces here before adopters notice.
        var plan = OsvScanner.ScanSource(s => s
            .SetSbomFile("artifacts/security/tamp-bom.json")
            .SetOutputFile("artifacts/security/tamp-cve.sarif")
            .SetFormat(OsvScannerFormat.Sarif));

        Assert.Equal("osv-scanner", plan.Executable);
        Assert.Equal(new[]
        {
            "scan", "source",
            "--sbom", "artifacts/security/tamp-bom.json",
            "--output-file", "artifacts/security/tamp-cve.sarif",
            "--format", "sarif",
            "--allow-no-lockfiles",
        }, plan.Arguments);
    }
}
