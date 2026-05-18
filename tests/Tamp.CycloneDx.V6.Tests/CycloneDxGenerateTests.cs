using Xunit;

namespace Tamp.CycloneDx.V6.Tests;

public class CycloneDxGenerateTests
{
    [Fact]
    public void Minimal_Settings_Produce_Dotnet_CycloneDX_Plan()
    {
        var plan = CycloneDx.Generate(s => s.SetPath("./Tamp.slnx"));

        Assert.Equal("dotnet", plan.Executable);
        Assert.Equal(new[] { "CycloneDX", "./Tamp.slnx", "--output-format", "Json" }, plan.Arguments);
    }

    [Fact]
    public void Json_Format_Is_Default_For_Wave_1_Chain()
    {
        var plan = CycloneDx.Generate(s => s.SetPath("x.csproj"));
        var args = plan.Arguments.ToList();
        var idx = args.IndexOf("--output-format");
        Assert.True(idx >= 0);
        Assert.Equal("Json", args[idx + 1]);
    }

    [Fact]
    public void Xml_Format_Emits_Output_Format_Xml()
    {
        var plan = CycloneDx.Generate(s => s.SetPath("x.csproj").SetFormat(CycloneDxFormat.Xml));
        var args = plan.Arguments.ToList();
        var idx = args.IndexOf("--output-format");
        Assert.True(idx >= 0);
        Assert.Equal("Xml", args[idx + 1]);
    }

    [Fact]
    public void Auto_Format_Omits_Output_Format_Flag()
    {
        var plan = CycloneDx.Generate(s => s.SetPath("x.csproj").SetFormat(CycloneDxFormat.Auto));
        Assert.DoesNotContain("--output-format", plan.Arguments);
    }

    [Fact]
    public void Unsafe_Json_Format_Emits_Output_Format_UnsafeJson()
    {
        var plan = CycloneDx.Generate(s => s.SetPath("x.csproj").SetFormat(CycloneDxFormat.UnsafeJson));
        var args = plan.Arguments.ToList();
        var idx = args.IndexOf("--output-format");
        Assert.True(idx >= 0);
        Assert.Equal("UnsafeJson", args[idx + 1]);
    }

    [Fact]
    public void Spec_Version_Override_Emits_Flag()
    {
        var plan = CycloneDx.Generate(s => s.SetPath("x.csproj").SetSpecVersion("1.6"));
        var args = plan.Arguments.ToList();
        var idx = args.IndexOf("--spec-version");
        Assert.True(idx >= 0);
        Assert.Equal("1.6", args[idx + 1]);
    }

    [Fact]
    public void Output_Directory_And_Filename_Are_Emitted()
    {
        var plan = CycloneDx.Generate(s => s
            .SetPath("./src/App.csproj")
            .SetOutputDirectory("./artifacts/sbom")
            .SetFilename("app-1.2.3.json"));

        Assert.Equal(new[]
        {
            "CycloneDX", "./src/App.csproj",
            "--output", "./artifacts/sbom",
            "--filename", "app-1.2.3.json",
            "--output-format", "Json",
        }, plan.Arguments);
    }

    [Fact]
    public void All_Exclusion_And_Scope_Flags_Are_Emitted_When_Set()
    {
        var plan = CycloneDx.Generate(s => s
            .SetPath(".")
            .SetExcludeDevelopment(true)
            .SetExcludeTestProjects(true)
            .SetRecursive(true)
            .SetIncludeProjectReferences(true)
            .SetDisableHashComputation(true)
            .SetDisablePackageRestore(true)
            .SetNoSerialNumber(true));

        Assert.Contains("--exclude-dev", plan.Arguments);
        Assert.Contains("--exclude-test-projects", plan.Arguments);
        Assert.Contains("--recursive", plan.Arguments);
        Assert.Contains("--include-project-references", plan.Arguments);
        Assert.Contains("--disable-hash-computation", plan.Arguments);
        Assert.Contains("--disable-package-restore", plan.Arguments);
        Assert.Contains("--no-serial-number", plan.Arguments);
    }

    [Fact]
    public void Metadata_Component_Overrides_Are_Emitted()
    {
        var plan = CycloneDx.Generate(s => s
            .SetPath(".")
            .SetMetadataComponentName("TampSampleApp")
            .SetMetadataComponentVersion("1.2.3"));

        var args = plan.Arguments.ToList();
        var nameIdx = args.IndexOf("--set-name");
        Assert.True(nameIdx >= 0);
        Assert.Equal("TampSampleApp", args[nameIdx + 1]);

        var verIdx = args.IndexOf("--set-version");
        Assert.True(verIdx >= 0);
        Assert.Equal("1.2.3", args[verIdx + 1]);
    }

    [Fact]
    public void GitHub_License_Pair_Registers_Token_As_Secret()
    {
        var secret = new Secret("github-token", "ghp_token_value_redactme");
        var plan = CycloneDx.Generate(s => s.SetPath("x.csproj").SetGitHubLicense("BrewingCoder", secret));

        Assert.Contains("--github-username", plan.Arguments);
        Assert.Contains("BrewingCoder", plan.Arguments);
        Assert.Contains("--github-token", plan.Arguments);
        Assert.Contains("ghp_token_value_redactme", plan.Arguments);
        Assert.Contains("--enable-github-licenses", plan.Arguments);
        Assert.Single(plan.Secrets);
        Assert.Equal("github-token", plan.Secrets[0].Name);
    }

    [Fact]
    public void GitHub_Token_Without_Username_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => CycloneDx.Generate(s =>
        {
            s.SetPath("x.csproj");
            s.GitHubLicenseToken = new Secret("token", "ghp_x");
            // GitHubLicenseUsername left null
        }));
    }

    [Fact]
    public void No_GitHub_Credentials_Means_No_Secrets_Registered()
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
