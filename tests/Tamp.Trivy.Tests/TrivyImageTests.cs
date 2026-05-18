using Xunit;

namespace Tamp.Trivy.Tests;

public class TrivyImageTests
{
    [Fact]
    public void Minimal_Settings_Produce_Image_Scan_Plan()
    {
        var plan = Trivy.ScanImage(s => s.SetImageRef("python:3.12-alpine"));

        Assert.Equal("trivy", plan.Executable);
        Assert.Equal(new[] { "image", "--format", "sarif", "python:3.12-alpine" }, plan.Arguments);
    }

    [Fact]
    public void Sarif_Format_Is_Default_For_Wave_1_Chain()
    {
        var plan = Trivy.ScanImage(s => s.SetImageRef("x:y"));
        var args = plan.Arguments.ToList();
        var idx = args.IndexOf("--format");
        Assert.True(idx >= 0);
        Assert.Equal("sarif", args[idx + 1]);
    }

    [Theory]
    [InlineData(TrivyFormat.Sarif, "sarif")]
    [InlineData(TrivyFormat.Json, "json")]
    [InlineData(TrivyFormat.CycloneDx, "cyclonedx")]
    [InlineData(TrivyFormat.Spdx, "spdx")]
    [InlineData(TrivyFormat.SpdxJson, "spdx-json")]
    [InlineData(TrivyFormat.Github, "github")]
    [InlineData(TrivyFormat.CosignVuln, "cosign-vuln")]
    [InlineData(TrivyFormat.Template, "template")]
    public void Format_Maps_To_Documented_Wire_Value(TrivyFormat format, string expectedWire)
    {
        var plan = Trivy.ScanImage(s => s.SetImageRef("x:y").SetFormat(format));
        var args = plan.Arguments.ToList();
        var idx = args.IndexOf("--format");
        Assert.True(idx >= 0);
        Assert.Equal(expectedWire, args[idx + 1]);
    }

    [Fact]
    public void Table_Format_Omits_Format_Flag()
    {
        var plan = Trivy.ScanImage(s => s.SetImageRef("x:y").SetFormat(TrivyFormat.Table));
        Assert.DoesNotContain("--format", plan.Arguments);
    }

    [Fact]
    public void Input_Tar_Translates_To_Input_Flag_And_Omits_Positional()
    {
        var plan = Trivy.ScanImage(s => s.SetInputTarFile("./ruby.tar"));

        var args = plan.Arguments.ToList();
        Assert.Equal("image", args[0]);
        Assert.Contains("--input", args);
        Assert.Contains("./ruby.tar", args);
        Assert.DoesNotContain("ruby.tar", args.SkipWhile(a => a != "./ruby.tar").Skip(1));
    }

    [Fact]
    public void ImageRef_And_InputTarFile_Together_Throw()
    {
        Assert.Throws<InvalidOperationException>(() => Trivy.ScanImage(s => s
            .SetImageRef("x:y")
            .SetInputTarFile("./img.tar")));
    }

    [Fact]
    public void Missing_Target_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => Trivy.ScanImage(_ => { }));
    }

    [Fact]
    public void Severities_Comma_Joined_And_Uppercased()
    {
        var plan = Trivy.ScanImage(s => s
            .SetImageRef("x:y")
            .AddSeverity(TrivySeverity.High)
            .AddSeverity(TrivySeverity.Critical));

        var args = plan.Arguments.ToList();
        var idx = args.IndexOf("--severity");
        Assert.True(idx >= 0);
        Assert.Equal("HIGH,CRITICAL", args[idx + 1]);
    }

    [Fact]
    public void Scanners_Comma_Joined()
    {
        var plan = Trivy.ScanImage(s => s
            .SetImageRef("x:y")
            .AddScanner(TrivyScanner.Vuln)
            .AddScanner(TrivyScanner.Secret));

        var args = plan.Arguments.ToList();
        var idx = args.IndexOf("--scanners");
        Assert.True(idx >= 0);
        Assert.Equal("vuln,secret", args[idx + 1]);
    }

    [Fact]
    public void Platform_And_Image_Sources_Emit_Image_Specific_Flags()
    {
        var plan = Trivy.ScanImage(s => s
            .SetImageRef("multi-arch:latest")
            .SetPlatform("linux/amd64")
            .AddImageSource("docker")
            .AddImageSource("remote"));

        Assert.Contains("--platform", plan.Arguments);
        Assert.Contains("linux/amd64", plan.Arguments);
        Assert.Contains("--image-src", plan.Arguments);
        Assert.Contains("docker,remote", plan.Arguments);
    }

    [Fact]
    public void Air_Gap_Flags_Skip_Db_Updates()
    {
        var plan = Trivy.ScanImage(s => s
            .SetImageRef("x:y")
            .SetSkipDbUpdate(true)
            .SetSkipJavaDbUpdate(true));

        Assert.Contains("--skip-db-update", plan.Arguments);
        Assert.Contains("--skip-java-db-update", plan.Arguments);
    }

    [Fact]
    public void Exit_Code_Lets_Adopters_Gate_CI_On_Findings()
    {
        var plan = Trivy.ScanImage(s => s.SetImageRef("x:y").SetExitCode(1));
        var args = plan.Arguments.ToList();
        var idx = args.IndexOf("--exit-code");
        Assert.True(idx >= 0);
        Assert.Equal("1", args[idx + 1]);
    }

    [Fact]
    public void Ignore_Unfixed_And_Quiet_And_No_Progress_And_Ignorefile()
    {
        var plan = Trivy.ScanImage(s => s
            .SetImageRef("x:y")
            .SetIgnoreUnfixed(true)
            .SetQuiet(true)
            .SetNoProgress(true)
            .SetIgnoreFile("./.trivyignore"));

        Assert.Contains("--ignore-unfixed", plan.Arguments);
        Assert.Contains("--quiet", plan.Arguments);
        Assert.Contains("--no-progress", plan.Arguments);
        Assert.Contains("--ignorefile", plan.Arguments);
        Assert.Contains("./.trivyignore", plan.Arguments);
    }

    [Fact]
    public void Null_Configurer_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => Trivy.ScanImage(null!));
    }

    [Fact]
    public void Common_Flags_Precede_Subcommand_Specific_And_Positional_Comes_Last()
    {
        // Plan layout: subcommand, common report flags, image-specific flags, positional ref.
        var plan = Trivy.ScanImage(s => s
            .SetImageRef("alpine:3.20")
            .SetOutputFile("/tmp/image.sarif")
            .SetPlatform("linux/arm64"));

        var args = plan.Arguments.ToList();
        Assert.Equal("image", args[0]);
        Assert.True(args.IndexOf("--format") < args.IndexOf("--platform"),
            "common --format should precede image-specific --platform");
        Assert.True(args.IndexOf("--platform") < args.IndexOf("alpine:3.20"),
            "image-specific flags should precede positional image ref");
        Assert.Equal("alpine:3.20", args[^1]);
    }
}
