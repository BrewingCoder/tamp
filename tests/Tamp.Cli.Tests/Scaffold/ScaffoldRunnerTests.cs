using System;
using System.IO;
using Tamp.Cli.Scaffold;
using Tamp.Scaffold;
using Xunit;

namespace Tamp.Cli.Tests.Scaffold;

public sealed class ScaffoldRunnerTests : IDisposable
{
    private readonly string _root;
    public ScaffoldRunnerTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "tamp-runner-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }
    public void Dispose() { try { Directory.Delete(_root, recursive: true); } catch { } }

    private AbsolutePath P(string rel) => AbsolutePath.Create(Path.Combine(_root, rel));

    [Fact]
    public void Create_Mode_Writes_File_When_Absent_And_Reports_Written()
    {
        var runner = new ScaffoldRunner();
        var results = runner.Run([new FileSpec(P("a.txt"), "hello", WriteMode.Create)]);

        Assert.Single(results);
        Assert.Equal(FileWriteOutcome.Written, results[0].Outcome);
        Assert.Equal("hello", File.ReadAllText(P("a.txt").Value));
    }

    [Fact]
    public void Create_Mode_Refuses_To_Overwrite_Existing_File()
    {
        File.WriteAllText(P("a.txt").Value, "preexisting");
        var runner = new ScaffoldRunner();

        Assert.Throws<IOException>(() =>
            runner.Run([new FileSpec(P("a.txt"), "new content", WriteMode.Create)]));

        // Existing content untouched.
        Assert.Equal("preexisting", File.ReadAllText(P("a.txt").Value));
    }

    [Fact]
    public void SkipIfExists_Mode_Leaves_Existing_File_Alone_And_Reports_Skipped()
    {
        File.WriteAllText(P("tools.json").Value, "{\"existing\":true}");
        var runner = new ScaffoldRunner();
        var results = runner.Run([new FileSpec(P("tools.json"), "{\"new\":true}", WriteMode.SkipIfExists)]);

        Assert.Single(results);
        Assert.Equal(FileWriteOutcome.Skipped, results[0].Outcome);
        Assert.Equal("{\"existing\":true}", File.ReadAllText(P("tools.json").Value));
    }

    [Fact]
    public void SkipIfExists_Mode_Writes_File_When_Absent()
    {
        var runner = new ScaffoldRunner();
        var results = runner.Run([new FileSpec(P("fresh.json"), "{\"new\":true}", WriteMode.SkipIfExists)]);

        Assert.Equal(FileWriteOutcome.Written, results[0].Outcome);
        Assert.Equal("{\"new\":true}", File.ReadAllText(P("fresh.json").Value));
    }

    [Fact]
    public void Merge_Mode_Throws_NotSupported_In_v0_1_0()
    {
        var runner = new ScaffoldRunner();
        Assert.Throws<NotSupportedException>(() =>
            runner.Run([new FileSpec(P("a.json"), "{}", WriteMode.Merge)]));
    }

    [Fact]
    public void Dry_Run_Reports_Planned_And_Writes_Nothing()
    {
        var runner = new ScaffoldRunner(dryRun: true);
        var results = runner.Run([new FileSpec(P("a.txt"), "hello", WriteMode.Create)]);

        Assert.Equal(FileWriteOutcome.Planned, results[0].Outcome);
        Assert.False(File.Exists(P("a.txt").Value));
    }

    [Fact]
    public void Create_Mode_Auto_Creates_Parent_Directories()
    {
        var runner = new ScaffoldRunner();
        runner.Run([new FileSpec(P("nested/deep/file.txt"), "x", WriteMode.Create)]);

        Assert.True(File.Exists(P("nested/deep/file.txt").Value));
    }

    // ─── TAM-125: --force overwrite semantics ─────────────────────────────

    [Fact]
    public void Force_Mode_Overwrites_Existing_File_With_Create_Spec()
    {
        var path = P("Build.cs");
        File.WriteAllText(path.Value, "old content");

        var runner = new ScaffoldRunner(force: true);
        var results = runner.Run([new FileSpec(path, "new content", WriteMode.Create)]);

        Assert.Equal("new content", File.ReadAllText(path.Value));
        Assert.Single(results);
        Assert.Equal(FileWriteOutcome.Overwritten, results[0].Outcome);
    }

    [Fact]
    public void Force_Mode_Reports_Written_When_File_Did_Not_Exist()
    {
        // --force should not surface Overwritten when there was nothing to overwrite.
        var runner = new ScaffoldRunner(force: true);
        var results = runner.Run([new FileSpec(P("fresh.txt"), "content", WriteMode.Create)]);

        Assert.Equal(FileWriteOutcome.Written, results[0].Outcome);
    }

    [Fact]
    public void Force_Mode_With_SkipIfExists_Still_Skips()
    {
        // --force only changes Create-mode behavior. SkipIfExists is explicit
        // "leave it alone" semantics; --force does not override that.
        var path = P("dotnet-tools.json");
        File.WriteAllText(path.Value, "{ existing }");

        var runner = new ScaffoldRunner(force: true);
        var results = runner.Run([new FileSpec(path, "{ replacement }", WriteMode.SkipIfExists)]);

        Assert.Equal("{ existing }", File.ReadAllText(path.Value));
        Assert.Equal(FileWriteOutcome.Skipped, results[0].Outcome);
    }

    [Fact]
    public void Force_With_DryRun_Reports_Planned_Without_Writing()
    {
        var path = P("Build.cs");
        File.WriteAllText(path.Value, "old content");

        var runner = new ScaffoldRunner(dryRun: true, force: true);
        var results = runner.Run([new FileSpec(path, "new content", WriteMode.Create)]);

        // Nothing actually touched the file.
        Assert.Equal("old content", File.ReadAllText(path.Value));
        Assert.Equal(FileWriteOutcome.Planned, results[0].Outcome);
    }

    [Fact]
    public void Without_Force_Existing_File_With_Create_Spec_Throws()
    {
        var path = P("Build.cs");
        File.WriteAllText(path.Value, "old content");

        var runner = new ScaffoldRunner(force: false);
        var ex = Assert.Throws<IOException>(() =>
            runner.Run([new FileSpec(path, "new content", WriteMode.Create)]));
        Assert.Contains("--force", ex.Message);
        Assert.Equal("old content", File.ReadAllText(path.Value));   // untouched
    }
}
