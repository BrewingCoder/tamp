using Xunit;

namespace Tamp.Syft.V1.Tests;

public class SyftArchiveTests
{
    [Fact]
    public void Minimal_Settings_Produce_Scan_File_Plan()
    {
        var plan = Syft.ScanArchive(s => s.SetPath("/tmp/app.jar"));

        Assert.Equal("syft", plan.Executable);
        Assert.Equal("file:/tmp/app.jar", plan.Arguments[^1]);
    }

    [Fact]
    public void Output_File_Emits_Cyclonedx_Json_Equals_Path()
    {
        var plan = Syft.ScanArchive(s => s.SetPath("/tmp/app.war").SetOutputFile("/tmp/sbom.cdx.json"));
        Assert.Contains("cyclonedx-json=/tmp/sbom.cdx.json", plan.Arguments);
    }

    [Fact]
    public void Source_Name_Version_Set_For_Archives_Without_Embedded_Metadata()
    {
        var plan = Syft.ScanArchive(s => s
            .SetPath("/tmp/legacy.zip")
            .SetSourceName("legacy-bundle")
            .SetSourceVersion("1.0.0"));

        Assert.Contains("--source-name", plan.Arguments);
        Assert.Contains("legacy-bundle", plan.Arguments);
        Assert.Contains("--source-version", plan.Arguments);
        Assert.Contains("1.0.0", plan.Arguments);
    }

    [Fact]
    public void Missing_Path_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => Syft.ScanArchive(_ => { }));
    }

    [Fact]
    public void Null_Configurer_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => Syft.ScanArchive(null!));
    }
}
