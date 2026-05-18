using Xunit;

namespace Tamp.CycloneDx.V5.Tests;

public class CycloneDxGenerateTests
{
    [Fact]
    public void Minimal_Settings_Produce_Dotnet_CycloneDX_Plan()
    {
        var plan = CycloneDx.Generate(s => s.SetPath("./Tamp.slnx"));

        Assert.Equal("dotnet", plan.Executable);
        Assert.Equal(new[] { "CycloneDX", "./Tamp.slnx", "--json" }, plan.Arguments);
    }

    [Fact]
    public void Json_Format_Is_Default_For_Wave_1_Chain()
    {
        var plan = CycloneDx.Generate(s => s.SetPath("x.csproj"));
        Assert.Contains("--json", plan.Arguments);
    }

    [Fact]
    public void Xml_Format_Omits_Json_Flag()
    {
        var plan = CycloneDx.Generate(s => s.SetPath("x.csproj").SetFormat(CycloneDxFormat.Xml));
        Assert.DoesNotContain("--json", plan.Arguments);
        Assert.DoesNotContain("--include-xml", plan.Arguments);
    }

    [Fact]
    public void Both_Format_Includes_Json_And_Include_Xml_Flags()
    {
        var plan = CycloneDx.Generate(s => s.SetPath("x.csproj").SetFormat(CycloneDxFormat.Both));
        Assert.Contains("--json", plan.Arguments);
        Assert.Contains("--include-xml", plan.Arguments);
    }

    [Fact]
    public void Output_Directory_And_Filename_Are_Emitted()
    {
        var plan = CycloneDx.Generate(s => s
            .SetPath("./src/App.csproj")
            .SetOutputDirectory("./artifacts/sbom")
            .SetFilename("app-1.2.3"));

        Assert.Equal(new[]
        {
            "CycloneDX", "./src/App.csproj",
            "--out", "./artifacts/sbom",
            "--filename", "app-1.2.3",
            "--json",
        }, plan.Arguments);
    }

    [Fact]
    public void Exclusion_Flags_Are_Emitted_When_Set()
    {
        var plan = CycloneDx.Generate(s => s
            .SetPath(".")
            .SetExcludeDevelopment(true)
            .SetExcludeTestProjects(true)
            .SetIncludeTransitive(false)
            .SetDisableHashComputation(true));

        Assert.Contains("--exclude-dev", plan.Arguments);
        Assert.Contains("--exclude-test-projects", plan.Arguments);
        Assert.Contains("--exclude-transitive", plan.Arguments);
        Assert.Contains("--disable-hash-computation", plan.Arguments);
    }

    [Fact]
    public void Include_Transitive_True_Does_Not_Emit_Exclude_Flag()
    {
        // Default is true (federal-SBOM expectations). Verify we don't emit
        // a positive enable-flag — the tool resolves transitives by default.
        var plan = CycloneDx.Generate(s => s.SetPath("x.csproj"));
        Assert.DoesNotContain("--exclude-transitive", plan.Arguments);
        Assert.DoesNotContain("--include-transitive", plan.Arguments);
    }

    [Fact]
    public void Serial_Number_Override_Emits_Flag()
    {
        var plan = CycloneDx.Generate(s => s
            .SetPath("x.csproj")
            .SetSerialNumber("urn:uuid:fc6c1f73-86cb-4d27-bb2c-1d3a1a9b3c5a"));

        Assert.Contains("--set-serial-number", plan.Arguments);
        Assert.Contains("urn:uuid:fc6c1f73-86cb-4d27-bb2c-1d3a1a9b3c5a", plan.Arguments);
    }

    [Fact]
    public void GitHub_License_Token_Is_Registered_As_Secret()
    {
        var secret = new Secret("github-token", "ghp_token_value_redactme");
        var plan = CycloneDx.Generate(s => s.SetPath("x.csproj").SetGitHubLicenseToken(secret));

        Assert.Contains("--github-token", plan.Arguments);
        Assert.Contains("ghp_token_value_redactme", plan.Arguments);
        Assert.Single(plan.Secrets);
        Assert.Equal("github-token", plan.Secrets[0].Name);
    }

    [Fact]
    public void No_GitHub_Token_Means_No_Secrets_Registered()
    {
        var plan = CycloneDx.Generate(s => s.SetPath("x.csproj"));
        Assert.Empty(plan.Secrets);
    }

    [Fact]
    public void Working_Directory_Propagates_To_Plan()
    {
        var plan = CycloneDx.Generate(s => s.SetPath(".").SetWorkingDirectory("/tmp/build"));
        Assert.Equal("/tmp/build", plan.WorkingDirectory);
    }

    [Fact]
    public void Environment_Variables_Disable_Dotnet_Banner_And_Telemetry()
    {
        var plan = CycloneDx.Generate(s => s.SetPath("."));
        Assert.Equal("1", plan.Environment["DOTNET_NOLOGO"]);
        Assert.Equal("1", plan.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"]);
    }

    [Fact]
    public void Custom_Environment_Variables_Are_Preserved()
    {
        var plan = CycloneDx.Generate(s =>
        {
            s.SetPath(".");
            s.EnvironmentVariables["HTTPS_PROXY"] = "http://corp-proxy:3128";
        });

        Assert.Equal("http://corp-proxy:3128", plan.Environment["HTTPS_PROXY"]);
        Assert.Equal("1", plan.Environment["DOTNET_NOLOGO"]);
    }

    [Fact]
    public void Missing_Path_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => CycloneDx.Generate(_ => { }));
    }

    [Fact]
    public void Null_Configurer_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => CycloneDx.Generate(null!));
    }
}
