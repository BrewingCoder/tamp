using Xunit;

namespace Tamp.SonarScanner.V10.Tests;

public sealed class SonarScannerTests
{
    private static Tool FakeTool() => new(AbsolutePath.Create("/fake/dotnet-sonarscanner"));

    private static int IndexStartingWith(IReadOnlyList<string> args, string prefix, int start = 0)
    {
        for (var i = start; i < args.Count; i++)
            if (args[i].StartsWith(prefix, StringComparison.Ordinal)) return i;
        return -1;
    }

    // ---- Begin: required ----

    [Fact]
    public void Begin_Throws_On_Null_Tool()
    {
        Assert.Throws<ArgumentNullException>(() => SonarScanner.Begin(null!, _ => { }));
    }

    [Fact]
    public void Begin_Throws_On_Null_Configurer()
    {
        Assert.Throws<ArgumentNullException>(() => SonarScanner.Begin(FakeTool(), null!));
    }

    [Fact]
    public void Begin_Requires_ProjectKey()
    {
        Assert.Throws<InvalidOperationException>(() => SonarScanner.Begin(FakeTool(), s => { }));
    }

    [Fact]
    public void Begin_Minimal_ProjectKey_Only()
    {
        var plan = SonarScanner.Begin(FakeTool(), s => s.SetProjectKey("my-project"));
        Assert.Equal("/fake/dotnet-sonarscanner", plan.Executable);
        Assert.Equal(["begin", "/k:my-project"], plan.Arguments);
        Assert.Empty(plan.Secrets);
    }

    // ---- Begin: shape of every flag ----

    [Fact]
    public void Begin_ProjectKey_Uses_K_Slash()
    {
        var plan = SonarScanner.Begin(FakeTool(), s => s.SetProjectKey("acme:lib"));
        Assert.Equal("/k:acme:lib", plan.Arguments[1]);
    }

    [Fact]
    public void Begin_ProjectName_Uses_N_Slash()
    {
        var plan = SonarScanner.Begin(FakeTool(), s => s.SetProjectKey("k").SetProjectName("My Library"));
        Assert.Contains("/n:My Library", plan.Arguments);
    }

    [Fact]
    public void Begin_ProjectVersion_Uses_V_Slash()
    {
        var plan = SonarScanner.Begin(FakeTool(), s => s.SetProjectKey("k").SetProjectVersion("1.2.3"));
        Assert.Contains("/v:1.2.3", plan.Arguments);
    }

    [Fact]
    public void Begin_Organization_Uses_O_Slash()
    {
        var plan = SonarScanner.Begin(FakeTool(), s => s.SetProjectKey("k").SetOrganization("acme-org"));
        Assert.Contains("/o:acme-org", plan.Arguments);
    }

    [Fact]
    public void Begin_AnalysisXml_Uses_S_Slash()
    {
        var plan = SonarScanner.Begin(FakeTool(), s => s.SetProjectKey("k").SetAnalysisXml("/abs/Analysis.xml"));
        Assert.Contains("/s:/abs/Analysis.xml", plan.Arguments);
    }

    [Fact]
    public void Begin_HostUrl_Emits_As_DotD_Property()
    {
        var plan = SonarScanner.Begin(FakeTool(), s => s
            .SetProjectKey("k")
            .SetHostUrl("https://sonar.example.com"));
        Assert.Contains("/d:sonar.host.url=https://sonar.example.com", plan.Arguments);
    }

    [Fact]
    public void Begin_Token_Emits_DotD_Sonar_Token_And_Registers_Secret()
    {
        var token = new Secret("SonarToken", "abc123");
        var plan = SonarScanner.Begin(FakeTool(), s => s.SetProjectKey("k").SetToken(token));
        Assert.Contains("/d:sonar.token=abc123", plan.Arguments);
        Assert.Single(plan.Secrets);
        Assert.Same(token, plan.Secrets[0]);
    }

    [Fact]
    public void Begin_Verbose_Emits_DotD_Sonar_Verbose_True()
    {
        var plan = SonarScanner.Begin(FakeTool(), s => s.SetProjectKey("k").SetVerbose(true));
        Assert.Contains("/d:sonar.verbose=true", plan.Arguments);
    }

    [Fact]
    public void Begin_AdditionalProperties_All_Emit_As_DotD_Pairs()
    {
        var plan = SonarScanner.Begin(FakeTool(), s => s
            .SetProjectKey("k")
            .SetProperty("sonar.exclusions", "**/*.generated.cs")
            .SetProperty("sonar.cs.opencover.reportsPaths", "coverage.xml"));
        Assert.Contains("/d:sonar.exclusions=**/*.generated.cs", plan.Arguments);
        Assert.Contains("/d:sonar.cs.opencover.reportsPaths=coverage.xml", plan.Arguments);
    }

    [Fact]
    public void Begin_Working_Directory_From_Settings_Wins_Over_Tool()
    {
        var tool = new Tool(AbsolutePath.Create("/fake/scanner"), workingDirectory: "/from-tool");
        var plan = SonarScanner.Begin(tool, s => s.SetProjectKey("k").SetWorkingDirectory("/from-settings"));
        Assert.Equal("/from-settings", plan.WorkingDirectory);
    }

    [Fact]
    public void Begin_Working_Directory_Falls_Back_To_Tool_When_Not_Set_On_Settings()
    {
        var tool = new Tool(AbsolutePath.Create("/fake/scanner"), workingDirectory: "/from-tool");
        var plan = SonarScanner.Begin(tool, s => s.SetProjectKey("k"));
        Assert.Equal("/from-tool", plan.WorkingDirectory);
    }

    // ---- End ----

    [Fact]
    public void End_Throws_On_Null_Tool()
    {
        Assert.Throws<ArgumentNullException>(() => SonarScanner.End(null!));
    }

    [Fact]
    public void End_With_No_Configurer_Is_Just_End_Verb()
    {
        var plan = SonarScanner.End(FakeTool());
        Assert.Equal(["end"], plan.Arguments);
        Assert.Empty(plan.Secrets);
    }

    [Fact]
    public void End_With_Token_Emits_DotD_Sonar_Token_And_Registers_Secret()
    {
        var token = new Secret("SonarToken", "abc123");
        var plan = SonarScanner.End(FakeTool(), s => s.SetToken(token));
        Assert.Contains("/d:sonar.token=abc123", plan.Arguments);
        Assert.Single(plan.Secrets);
    }

    [Fact]
    public void End_AdditionalProperties_Emit_As_DotD_Pairs()
    {
        var plan = SonarScanner.End(FakeTool(), s => s
            .SetProperty("sonar.qualitygate.wait", "true"));
        Assert.Contains("/d:sonar.qualitygate.wait=true", plan.Arguments);
    }
}
