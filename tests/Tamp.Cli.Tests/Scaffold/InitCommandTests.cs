using System;
using System.IO;
using Tamp.Cli.Scaffold.Commands;
using Xunit;

namespace Tamp.Cli.Tests.Scaffold;

/// <summary>
/// End-to-end exercises for the `tamp init` flow against fabricated working dirs.
/// </summary>
public sealed class InitCommandTests : IDisposable
{
    private readonly string _root;
    private readonly StringWriter _stdout = new();
    private readonly StringWriter _stderr = new();

    public InitCommandTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "tamp-init-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public void Dispose() { try { Directory.Delete(_root, recursive: true); } catch { } }

    private int Run(params string[] args)
        => InitCommand.Run([.. args, _root], _stdout, _stderr);

    [Fact]
    public void Bare_Init_In_Empty_Dir_Writes_Five_Files()
    {
        var exit = Run();
        Assert.Equal(InitCommand.ExitOk, exit);

        Assert.True(File.Exists(Path.Combine(_root, "build", "Build.cs")));
        Assert.True(File.Exists(Path.Combine(_root, "build", "Build.csproj")));
        Assert.True(File.Exists(Path.Combine(_root, ".config", "dotnet-tools.json")));
        Assert.True(File.Exists(Path.Combine(_root, "tamp.sh")));
        Assert.True(File.Exists(Path.Combine(_root, "tamp.cmd")));

        var stdout = _stdout.ToString();
        Assert.Contains("wrote", stdout);
        Assert.Contains("Next steps:", stdout);
    }

    [Fact]
    public void Tamp_Sh_Is_Emitted_With_Unix_Executable_Bit_On_Posix()
    {
        if (OperatingSystem.IsWindows()) return;                                // POSIX-only check
        Run();
        var mode = File.GetUnixFileMode(Path.Combine(_root, "tamp.sh"));
        Assert.True(mode.HasFlag(UnixFileMode.UserExecute), "owner execute bit must be set");
        Assert.True(mode.HasFlag(UnixFileMode.GroupExecute), "group execute bit must be set");
        Assert.True(mode.HasFlag(UnixFileMode.OtherExecute), "other execute bit must be set");
    }

    [Fact]
    public void Init_Refuses_When_Build_Cs_Already_Exists()
    {
        Directory.CreateDirectory(Path.Combine(_root, "build"));
        File.WriteAllText(Path.Combine(_root, "build", "Build.cs"), "// preexisting");

        var exit = Run();
        Assert.Equal(InitCommand.ExitFileExists, exit);

        // The preexisting file is intact.
        Assert.Equal("// preexisting", File.ReadAllText(Path.Combine(_root, "build", "Build.cs")));
    }

    [Fact]
    public void Init_DryRun_Writes_Nothing_But_Lists_Planned_Files()
    {
        var exit = Run("--dry-run");
        Assert.Equal(InitCommand.ExitOk, exit);

        Assert.False(File.Exists(Path.Combine(_root, "build", "Build.cs")));
        Assert.False(File.Exists(Path.Combine(_root, "build", "Build.csproj")));

        var stdout = _stdout.ToString();
        Assert.Contains("would-write", stdout);
        Assert.Contains("(dry-run; nothing was written.)", stdout);
    }

    [Fact]
    public void Init_Preserves_Existing_Dotnet_Tools_Json()
    {
        Directory.CreateDirectory(Path.Combine(_root, ".config"));
        var existing = "{\"version\":1,\"isRoot\":true,\"tools\":{\"other-tool\":{\"version\":\"1.0.0\",\"commands\":[\"other-tool\"]}}}";
        File.WriteAllText(Path.Combine(_root, ".config", "dotnet-tools.json"), existing);

        var exit = Run();
        Assert.Equal(InitCommand.ExitOk, exit);

        // Existing tools.json untouched (SkipIfExists mode).
        Assert.Equal(existing, File.ReadAllText(Path.Combine(_root, ".config", "dotnet-tools.json")));
        // But the other two files DID land.
        Assert.True(File.Exists(Path.Combine(_root, "build", "Build.cs")));
    }

    [Fact]
    public void Init_Surfaces_Solution_Probe_Diagnostic_When_No_Solution_Present()
    {
        var exit = Run();
        Assert.Equal(InitCommand.ExitOk, exit);

        Assert.Contains("no-solution-found", _stdout.ToString());
    }

    [Fact]
    public void Init_Lists_Templates_With_List_Templates_Flag()
    {
        // List flag is informational; doesn't need a fabricated working dir to exist.
        var exit = InitCommand.Run(["--list-templates"], _stdout, _stderr);
        Assert.Equal(InitCommand.ExitOk, exit);

        var stdout = _stdout.ToString();
        Assert.Contains("minimal", stdout);
        Assert.Contains("(embedded)", stdout);
    }

    [Theory]
    [InlineData("--template-source")]
    [InlineData("--offline")]
    [InlineData("--with-ci")]
    [InlineData("--interactive")]
    public void Reserved_Flags_Exit_With_Clean_Message(string flag)
    {
        // Some of these reserve a value-token; pass a value to satisfy any consumer.
        var args = flag is "--template-source" or "--with-ci"
            ? new[] { flag, "x", _root }
            : new[] { flag, _root };

        var exit = InitCommand.Run(args, _stdout, _stderr);
        Assert.Equal(InitCommand.ExitNotImplemented, exit);

        var stderr = _stderr.ToString();
        Assert.Contains(flag, stderr);
        Assert.Contains("lands in", stderr);
    }

    // ─── TAM-122 / TAM-125: --template + --force ─────────────────────────

    [Theory]
    [InlineData("minimal")]
    [InlineData("library")]
    [InlineData("monorepo")]
    public void Template_Flag_Selects_Embedded_Template(string templateName)
    {
        var exit = InitCommand.Run(
            new[] { "--template", templateName, _root },
            _stdout, _stderr);
        Assert.Equal(InitCommand.ExitOk, exit);
        var buildCs = System.IO.Path.Combine(_root, "build", "Build.cs");
        Assert.True(System.IO.File.Exists(buildCs));
    }

    [Fact]
    public void Template_Flag_Without_Value_Errors_With_Usage()
    {
        var exit = InitCommand.Run(new[] { "--template" }, _stdout, _stderr);
        Assert.Equal(InitCommand.ExitUsage, exit);
        Assert.Contains("--template requires a name", _stderr.ToString());
    }

    [Fact]
    public void Unknown_Template_Name_Exits_With_TemplateNotFound()
    {
        var exit = InitCommand.Run(
            new[] { "--template", "unknown-template", _root },
            _stdout, _stderr);
        Assert.Equal(InitCommand.ExitTemplateNotFound, exit);
        Assert.Contains("no template named 'unknown-template'", _stderr.ToString());
    }

    [Fact]
    public void Library_Template_Writes_Pack_Target()
    {
        var exit = InitCommand.Run(
            new[] { "--template", "library", _root },
            _stdout, _stderr);
        Assert.Equal(InitCommand.ExitOk, exit);
        var buildCs = System.IO.Path.Combine(_root, "build", "Build.cs");
        var content = System.IO.File.ReadAllText(buildCs);
        Assert.Contains("Target Pack", content);
        Assert.Contains("NupkgOut", content);
        Assert.Contains("DotNet.Pack", content);
    }

    [Fact]
    public void Monorepo_Template_Writes_Test_Fanout_And_Ci_Aggregate()
    {
        var exit = InitCommand.Run(
            new[] { "--template", "monorepo", _root },
            _stdout, _stderr);
        Assert.Equal(InitCommand.ExitOk, exit);
        var content = System.IO.File.ReadAllText(System.IO.Path.Combine(_root, "build", "Build.cs"));
        Assert.Contains("Target Test", content);
        Assert.Contains("GlobFiles(\"**/*.Tests.csproj\")", content);
        Assert.Contains("Target Ci", content);
    }

    [Fact]
    public void Force_Flag_Overwrites_Existing_BuildCs()
    {
        // First run — succeeds.
        var first = InitCommand.Run(new[] { _root }, _stdout, _stderr);
        Assert.Equal(InitCommand.ExitOk, first);

        // Second run without --force — refused.
        var refused = InitCommand.Run(new[] { _root }, _stdout, _stderr);
        Assert.Equal(InitCommand.ExitFileExists, refused);
        Assert.Contains("already exists", _stderr.ToString());

        _stdout.GetStringBuilder().Clear();
        _stderr.GetStringBuilder().Clear();

        // Third run WITH --force — succeeds, and the Build.cs differs since
        // we used a different template.
        var forced = InitCommand.Run(
            new[] { "--template", "library", "--force", _root },
            _stdout, _stderr);
        Assert.Equal(InitCommand.ExitOk, forced);

        var content = System.IO.File.ReadAllText(System.IO.Path.Combine(_root, "build", "Build.cs"));
        Assert.Contains("Target Pack", content);    // library template overwrote the minimal one
    }

    [Fact]
    public void Force_Flag_Result_Reports_Overwrote_Distinct_From_Wrote()
    {
        InitCommand.Run(new[] { _root }, _stdout, _stderr);
        _stdout.GetStringBuilder().Clear();

        var exit = InitCommand.Run(new[] { "--force", _root }, _stdout, _stderr);
        Assert.Equal(InitCommand.ExitOk, exit);
        Assert.Contains("overwrote", _stdout.ToString());
    }

    [Fact]
    public void ListTemplates_Surface_Includes_All_Three_Embedded()
    {
        var exit = InitCommand.Run(new[] { "--list-templates" }, _stdout, _stderr);
        Assert.Equal(InitCommand.ExitOk, exit);
        var stdout = _stdout.ToString();
        Assert.Contains("minimal", stdout);
        Assert.Contains("library", stdout);
        Assert.Contains("monorepo", stdout);
    }

    [Fact]
    public void Unknown_Flag_Exits_Usage_With_Help()
    {
        var exit = InitCommand.Run(["--made-up-flag", _root], _stdout, _stderr);
        Assert.Equal(InitCommand.ExitUsage, exit);
        Assert.Contains("unknown flag", _stderr.ToString());
        Assert.Contains("tamp init —", _stderr.ToString());           // help text follows
    }

    [Fact]
    public void Solution_Override_Bypasses_Probe()
    {
        var explicitSolution = Path.Combine(_root, "Foo.slnx");
        File.WriteAllText(explicitSolution, "");
        Run("--solution", explicitSolution);

        var buildCs = File.ReadAllText(Path.Combine(_root, "build", "Build.cs"));
        Assert.Contains("[Solution] readonly Solution Solution = null!;", buildCs);
        Assert.DoesNotContain("no-solution-found", _stdout.ToString());
    }

    [Fact]
    public void Drift_Gate_Helper_Compares_SemVer_Coarsely()
    {
        Assert.True(InitCommand.TampCoreVersionIsAtLeast("1.4.0", "1.3.0"));
        Assert.True(InitCommand.TampCoreVersionIsAtLeast("1.3.0", "1.3.0"));
        Assert.False(InitCommand.TampCoreVersionIsAtLeast("1.2.0", "1.3.0"));
        Assert.True(InitCommand.TampCoreVersionIsAtLeast("2.0.0", "1.5.0"));
        // Prerelease suffix tolerated (coarse compare strips it).
        Assert.True(InitCommand.TampCoreVersionIsAtLeast("1.4.0-alpha.1", "1.3.0"));
    }

    [Fact]
    public void Resolve_Cli_Version_Yields_Three_Dotted_Components()
    {
        var v = InitCommand.ResolveCliVersion();
        var parts = v.Split('.');
        Assert.Equal(3, parts.Length);
        Assert.All(parts, p => Assert.True(int.TryParse(p, out _), $"non-integer in '{v}': '{p}'"));
    }
}
