using Xunit;

namespace Tamp.Syft.V1.Tests;

public class SyftImageTests
{
    [Theory]
    [InlineData(SyftImageScheme.Registry, "registry:nginx:1.27")]
    [InlineData(SyftImageScheme.Docker, "docker:nginx:1.27")]
    [InlineData(SyftImageScheme.Podman, "podman:nginx:1.27")]
    [InlineData(SyftImageScheme.OciArchive, "oci-archive:nginx:1.27")]
    [InlineData(SyftImageScheme.OciDir, "oci-dir:nginx:1.27")]
    [InlineData(SyftImageScheme.DockerArchive, "docker-archive:nginx:1.27")]
    [InlineData(SyftImageScheme.SingularityImage, "singularity:nginx:1.27")]
    public void Scheme_Plus_Ref_Becomes_Positional_Source(SyftImageScheme scheme, string expected)
    {
        var plan = Syft.ScanImage(s => s.SetImageRef("nginx:1.27").SetScheme(scheme));
        Assert.Equal(expected, plan.Arguments[^1]);
    }

    [Fact]
    public void Registry_Is_Default_Scheme()
    {
        var plan = Syft.ScanImage(s => s.SetImageRef("alpine:3.20"));
        Assert.Equal("registry:alpine:3.20", plan.Arguments[^1]);
    }

    [Fact]
    public void Squashed_Scope_Omits_Flag_Tool_Default()
    {
        var plan = Syft.ScanImage(s => s.SetImageRef("x:y").SetScope(SyftScope.Squashed));
        Assert.DoesNotContain("--scope", plan.Arguments);
    }

    [Theory]
    [InlineData(SyftScope.AllLayers, "all-layers")]
    [InlineData(SyftScope.DeepSquashed, "deep-squashed")]
    public void Non_Default_Scope_Emits_Flag(SyftScope scope, string expectedWire)
    {
        var plan = Syft.ScanImage(s => s.SetImageRef("x:y").SetScope(scope));
        var args = plan.Arguments.ToList();
        var idx = args.IndexOf("--scope");
        Assert.True(idx >= 0);
        Assert.Equal(expectedWire, args[idx + 1]);
    }

    [Fact]
    public void Platform_Emits_Flag()
    {
        var plan = Syft.ScanImage(s => s.SetImageRef("x:y").SetPlatform("linux/arm64"));
        var args = plan.Arguments.ToList();
        var idx = args.IndexOf("--platform");
        Assert.True(idx >= 0);
        Assert.Equal("linux/arm64", args[idx + 1]);
    }

    [Fact]
    public void From_Sources_Repeat()
    {
        var plan = Syft.ScanImage(s => s.SetImageRef("x:y").AddFrom("docker").AddFrom("registry"));
        Assert.Equal(2, plan.Arguments.Count(a => a == "--from"));
        Assert.Contains("docker", plan.Arguments);
        Assert.Contains("registry", plan.Arguments);
    }

    [Fact]
    public void Missing_Image_Ref_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => Syft.ScanImage(_ => { }));
    }

    [Fact]
    public void Null_Configurer_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => Syft.ScanImage(null!));
    }

    [Fact]
    public void Full_Image_Recipe_Common_Flags_Then_Image_Specific_Then_Positional()
    {
        var plan = Syft.ScanImage(s => s
            .SetImageRef("private.example.com/team/app:v2.3.4")
            .SetScheme(SyftImageScheme.Registry)
            .SetScope(SyftScope.AllLayers)
            .SetPlatform("linux/amd64")
            .SetOutputFile("./artifacts/app.cdx.json")
            .SetSourceName("app")
            .SetSourceVersion("2.3.4")
            .SetQuiet(true));

        Assert.Equal("syft", plan.Executable);
        var args = plan.Arguments.ToList();
        Assert.Equal("scan", args[0]);
        Assert.True(args.IndexOf("-o") < args.IndexOf("--scope"),
            "common -o should precede image-specific --scope");
        Assert.True(args.IndexOf("--scope") < args.IndexOf("registry:private.example.com/team/app:v2.3.4"),
            "image-specific flags should precede the positional source");
        Assert.Equal("registry:private.example.com/team/app:v2.3.4", args[^1]);
    }
}
