using Xunit;

namespace Tamp.Syft.V1.Tests;

public class SyftDirectoryTests
{
    [Fact]
    public void Minimal_Settings_Produce_Scan_Dir_Plan()
    {
        var plan = Syft.ScanDirectory(s => s.SetPath("./src").SetOutputFile("./sbom.cdx.json"));

        Assert.Equal("syft", plan.Executable);
        Assert.Equal(new[]
        {
            "scan",
            "-o", "cyclonedx-json=./sbom.cdx.json",
            "dir:./src",
        }, plan.Arguments);
    }

    [Fact]
    public void Cyclonedx_Json_Is_Default_Format_For_Wave_1_Chain()
    {
        var plan = Syft.ScanDirectory(s => s.SetPath(".").SetOutputFile("/tmp/x.json"));
        Assert.Contains("cyclonedx-json=/tmp/x.json", plan.Arguments);
    }

    [Fact]
    public void ForceDirScheme_True_Prefixes_With_Dir_Colon()
    {
        var plan = Syft.ScanDirectory(s => s.SetPath("./src"));
        Assert.Contains("dir:./src", plan.Arguments);
    }

    [Fact]
    public void ForceDirScheme_False_Emits_Bare_Path()
    {
        var plan = Syft.ScanDirectory(s => s.SetPath("./src").SetForceDirScheme(false));
        Assert.DoesNotContain("dir:./src", plan.Arguments);
        Assert.Contains("./src", plan.Arguments);
    }

    [Theory]
    [InlineData(SyftFormat.CycloneDxJson, "cyclonedx-json")]
    [InlineData(SyftFormat.CycloneDxXml, "cyclonedx-xml")]
    [InlineData(SyftFormat.SpdxJson, "spdx-json")]
    [InlineData(SyftFormat.SpdxTagValue, "spdx-tag-value")]
    [InlineData(SyftFormat.SyftJson, "syft-json")]
    [InlineData(SyftFormat.SyftTable, "syft-table")]
    [InlineData(SyftFormat.SyftText, "syft-text")]
    [InlineData(SyftFormat.GithubJson, "github-json")]
    [InlineData(SyftFormat.Purls, "purls")]
    [InlineData(SyftFormat.Template, "template")]
    public void Format_Maps_To_Documented_Wire_Value(SyftFormat format, string expectedWire)
    {
        var plan = Syft.ScanDirectory(s => s.SetPath(".").SetFormat(format).SetOutputFile("/tmp/out"));
        Assert.Contains($"{expectedWire}=/tmp/out", plan.Arguments);
    }

    [Fact]
    public void Multiple_Outputs_Override_Single_Output_Shortcut()
    {
        var plan = Syft.ScanDirectory(s => s
            .SetPath(".")
            .SetOutputFile("/should/be/ignored.json") // shortcut
            .AddOutput(SyftFormat.CycloneDxJson, "/tmp/a.cdx.json")
            .AddOutput(SyftFormat.SyftJson, "/tmp/b.syft.json")
            .AddOutput(SyftFormat.SyftTable)); // stdout — no path

        Assert.Equal(3, plan.Arguments.Count(a => a == "-o"));
        Assert.Contains("cyclonedx-json=/tmp/a.cdx.json", plan.Arguments);
        Assert.Contains("syft-json=/tmp/b.syft.json", plan.Arguments);
        Assert.Contains("syft-table", plan.Arguments);  // no =path → stdout
        Assert.DoesNotContain("/should/be/ignored.json", plan.Arguments);
    }

    [Fact]
    public void No_Output_Settings_Defers_To_Syft_Default()
    {
        var plan = Syft.ScanDirectory(s => s.SetPath("."));
        Assert.DoesNotContain("-o", plan.Arguments);
    }

    [Fact]
    public void Base_Path_Emits_Source_Specific_Flag()
    {
        var plan = Syft.ScanDirectory(s => s.SetPath("./repo/src").SetBasePath("./repo"));
        var args = plan.Arguments.ToList();
        var idx = args.IndexOf("--base-path");
        Assert.True(idx >= 0);
        Assert.Equal("./repo", args[idx + 1]);
    }

    [Fact]
    public void Exclude_Patterns_Repeat()
    {
        var plan = Syft.ScanDirectory(s => s
            .SetPath(".")
            .AddExcludePattern("./node_modules")
            .AddExcludePattern("./vendor"));

        Assert.Equal(2, plan.Arguments.Count(a => a == "--exclude"));
    }

    [Fact]
    public void Source_Name_And_Version_Override_Metadata()
    {
        var plan = Syft.ScanDirectory(s => s
            .SetPath(".")
            .SetSourceName("my-app")
            .SetSourceVersion("1.2.3")
            .SetSourceSupplier("Acme Corp"));

        Assert.Contains("--source-name", plan.Arguments);
        Assert.Contains("my-app", plan.Arguments);
        Assert.Contains("--source-version", plan.Arguments);
        Assert.Contains("1.2.3", plan.Arguments);
        Assert.Contains("--source-supplier", plan.Arguments);
        Assert.Contains("Acme Corp", plan.Arguments);
    }

    [Fact]
    public void Enrich_Sources_Repeat()
    {
        var plan = Syft.ScanDirectory(s => s
            .SetPath(".")
            .AddEnrich("golang")
            .AddEnrich("javascript"));

        Assert.Equal(2, plan.Arguments.Count(a => a == "--enrich"));
        Assert.Contains("golang", plan.Arguments);
        Assert.Contains("javascript", plan.Arguments);
    }

    [Fact]
    public void Parallelism_Emits_Invariant_Decimal()
    {
        var plan = Syft.ScanDirectory(s => s.SetPath(".").SetParallelism(8));
        var args = plan.Arguments.ToList();
        var idx = args.IndexOf("--parallelism");
        Assert.True(idx >= 0);
        Assert.Equal("8", args[idx + 1]);
    }

    [Fact]
    public void Quiet_Flag_Emitted_When_Set()
    {
        var plan = Syft.ScanDirectory(s => s.SetPath(".").SetQuiet(true));
        Assert.Contains("--quiet", plan.Arguments);
    }

    [Fact]
    public void Config_Files_Repeat_With_Short_Flag()
    {
        var plan = Syft.ScanDirectory(s => s.SetPath(".").AddConfigFile("./syft.yaml").AddConfigFile("./syft-overrides.yaml"));
        Assert.Equal(2, plan.Arguments.Count(a => a == "-c"));
    }

    [Fact]
    public void Working_Directory_Propagates_To_Plan()
    {
        var plan = Syft.ScanDirectory(s => s.SetPath(".").SetWorkingDirectory("/repos/some-app"));
        Assert.Equal("/repos/some-app", plan.WorkingDirectory);
    }

    [Fact]
    public void Missing_Path_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => Syft.ScanDirectory(_ => { }));
    }

    [Fact]
    public void Null_Configurer_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => Syft.ScanDirectory(null!));
    }

    [Fact]
    public void Source_Argument_Comes_Last()
    {
        var plan = Syft.ScanDirectory(s => s
            .SetPath("./src")
            .SetOutputFile("./sbom.cdx.json")
            .SetBasePath("./")
            .AddExcludePattern("./node_modules"));

        Assert.Equal("dir:./src", plan.Arguments[^1]);
    }
}
