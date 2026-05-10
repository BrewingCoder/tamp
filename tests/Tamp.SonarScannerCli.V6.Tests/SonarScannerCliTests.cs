using Xunit;

namespace Tamp.SonarScannerCli.V6.Tests;

public sealed class SonarScannerCliTests
{
    private static Tool FakeTool() => new(AbsolutePath.Create("/fake/sonar-scanner"));

    [Fact]
    public void Scan_Throws_On_Null_Tool()
    {
        Assert.Throws<ArgumentNullException>(() => SonarScannerCli.Scan(null!));
    }

    [Fact]
    public void Scan_With_No_Configurer_Has_Empty_Args()
    {
        // Pure properties-file mode: scanner reads sonar-project.properties
        // from the working directory; the wrapper passes nothing.
        var plan = SonarScannerCli.Scan(FakeTool());
        Assert.Empty(plan.Arguments);
        Assert.Empty(plan.Secrets);
    }

    [Fact]
    public void Scan_Executable_Is_The_Tool_Path()
    {
        var plan = SonarScannerCli.Scan(FakeTool());
        Assert.Equal("/fake/sonar-scanner", plan.Executable);
    }

    [Fact]
    public void Scan_ProjectKey_Emits_DashD_Sonar_ProjectKey()
    {
        var plan = SonarScannerCli.Scan(FakeTool(), s => s.SetProjectKey("acme:lib"));
        Assert.Contains("-Dsonar.projectKey=acme:lib", plan.Arguments);
    }

    [Fact]
    public void Scan_HostUrl_Emits_DashD_Sonar_Host_Url()
    {
        var plan = SonarScannerCli.Scan(FakeTool(), s => s.SetHostUrl("https://sonar.example.com"));
        Assert.Contains("-Dsonar.host.url=https://sonar.example.com", plan.Arguments);
    }

    [Fact]
    public void Scan_Token_Emits_DashD_Sonar_Token_And_Registers_Secret()
    {
        var token = new Secret("SonarToken", "abc123");
        var plan = SonarScannerCli.Scan(FakeTool(), s => s.SetToken(token));
        Assert.Contains("-Dsonar.token=abc123", plan.Arguments);
        Assert.Single(plan.Secrets);
        Assert.Same(token, plan.Secrets[0]);
    }

    [Fact]
    public void Scan_Organization_Emits_DashD_Sonar_Organization()
    {
        var plan = SonarScannerCli.Scan(FakeTool(), s => s.SetOrganization("acme-org"));
        Assert.Contains("-Dsonar.organization=acme-org", plan.Arguments);
    }

    [Fact]
    public void Scan_ProjectBaseDir_And_Sources_Round_Trip()
    {
        var plan = SonarScannerCli.Scan(FakeTool(), s => s
            .SetProjectBaseDir("/repo")
            .SetSources("src,lib"));
        Assert.Contains("-Dsonar.projectBaseDir=/repo", plan.Arguments);
        Assert.Contains("-Dsonar.sources=src,lib", plan.Arguments);
    }

    [Fact]
    public void Scan_PropertiesFile_Emits_DashD_Project_Settings()
    {
        var plan = SonarScannerCli.Scan(FakeTool(), s => s
            .SetPropertiesFile("/abs/sonar-other.properties"));
        Assert.Contains("-Dproject.settings=/abs/sonar-other.properties", plan.Arguments);
    }

    [Fact]
    public void Scan_AdditionalProperties_All_Emit_As_DashD_Pairs()
    {
        var plan = SonarScannerCli.Scan(FakeTool(), s => s
            .SetProperty("sonar.exclusions", "**/*.spec.ts")
            .SetProperty("sonar.javascript.lcov.reportPaths", "coverage/lcov.info"));
        Assert.Contains("-Dsonar.exclusions=**/*.spec.ts", plan.Arguments);
        Assert.Contains("-Dsonar.javascript.lcov.reportPaths=coverage/lcov.info", plan.Arguments);
    }

    [Fact]
    public void Scan_Hybrid_Pattern_Properties_File_Plus_Override()
    {
        // Most common CI shape: sonar-project.properties carries the
        // project key + sources, the wrapper supplies the runtime-only
        // bits (host URL + token from CI secrets).
        var token = new Secret("SonarToken", "ci-token");
        var plan = SonarScannerCli.Scan(FakeTool(), s => s
            .SetHostUrl("https://sonar.example.com")
            .SetToken(token));
        Assert.Contains("-Dsonar.host.url=https://sonar.example.com", plan.Arguments);
        Assert.Contains("-Dsonar.token=ci-token", plan.Arguments);
        Assert.DoesNotContain("-Dsonar.projectKey=", string.Join(' ', plan.Arguments));
    }

    [Fact]
    public void Scan_WorkingDirectory_From_Settings_Wins_Over_Tool()
    {
        var tool = new Tool(AbsolutePath.Create("/fake/sonar-scanner"), workingDirectory: "/from-tool");
        var plan = SonarScannerCli.Scan(tool, s => s.SetWorkingDirectory("/from-settings"));
        Assert.Equal("/from-settings", plan.WorkingDirectory);
    }
}
