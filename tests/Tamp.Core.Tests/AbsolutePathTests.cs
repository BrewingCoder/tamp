using System.Runtime.InteropServices;
using Xunit;

namespace Tamp.Core.Tests;

public sealed class AbsolutePathTests : IDisposable
{
    private readonly string _scratch;

    public AbsolutePathTests()
    {
        _scratch = Path.Combine(Path.GetTempPath(), "tamp-abspath-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_scratch);
    }

    public void Dispose()
    {
        try { Directory.Delete(_scratch, recursive: true); } catch { }
    }

    private AbsolutePath P(string relative) => AbsolutePath.Create(Path.Combine(_scratch, relative));

    // ---- Construction ----

    [Fact]
    public void Create_Throws_On_Null()
    {
        Assert.Throws<ArgumentNullException>(() => AbsolutePath.Create(null!));
    }

    [Fact]
    public void Create_Throws_On_Empty()
    {
        Assert.Throws<ArgumentException>(() => AbsolutePath.Create(""));
    }

    [Fact]
    public void Create_Resolves_Relative_To_Absolute()
    {
        var path = AbsolutePath.Create(".");
        Assert.True(Path.IsPathRooted(path.Value));
    }

    [Fact]
    public void Create_Normalizes_Redundant_Segments()
    {
        var path = AbsolutePath.Create(Path.Combine(_scratch, "a", "..", "b"));
        Assert.EndsWith("b", path.Value);
        Assert.DoesNotContain("..", path.Value);
    }

    // ---- Implicit string conversion ----

    [Fact]
    public void Implicit_String_Conversion_Returns_Value()
    {
        var path = AbsolutePath.Create(_scratch);
        string s = path;
        Assert.Equal(path.Value, s);
    }

    [Fact]
    public void ToString_Returns_Value()
    {
        var path = AbsolutePath.Create(_scratch);
        Assert.Equal(path.Value, path.ToString());
    }

    // ---- / operator ----

    [Fact]
    public void Slash_Combines_Relative_Subpath()
    {
        var combined = AbsolutePath.Create(_scratch) / "subdir" / "file.txt";
        Assert.EndsWith(Path.Combine("subdir", "file.txt"), combined.Value);
    }

    [Fact]
    public void Slash_With_Absolute_Right_Replaces()
    {
        var rooted = "/abs/right";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) rooted = @"C:\abs\right";
        var combined = AbsolutePath.Create(_scratch) / rooted;
        Assert.Equal(AbsolutePath.Create(rooted).Value, combined.Value);
    }

    [Fact]
    public void Slash_Throws_On_Null_Right()
    {
        Assert.Throws<ArgumentNullException>(() => AbsolutePath.Create(_scratch) / (string)null!);
    }

    // ---- Components ----

    [Fact]
    public void Parent_Returns_Containing_Directory()
    {
        var file = P("dir/file.txt");
        Assert.EndsWith("dir", file.Parent!.Value);
    }

    [Fact]
    public void Name_Extension_NameWithoutExtension_Round_Trip()
    {
        var p = P("dir/foo.bar.txt");
        Assert.Equal("foo.bar.txt", p.Name);
        Assert.Equal(".txt", p.Extension);
        Assert.Equal("foo.bar", p.NameWithoutExtension);
    }

    // ---- Existence ----

    [Fact]
    public void File_Operations_Round_Trip()
    {
        var p = P("hello.txt");
        Assert.False(p.FileExists());
        p.WriteAllText("hello world");
        Assert.True(p.FileExists());
        Assert.Equal("hello world", p.ReadAllText());
        p.DeleteFile();
        Assert.False(p.FileExists());
    }

    [Fact]
    public void WriteAllText_Creates_Parent_Directories()
    {
        var p = P("nested/deep/file.txt");
        p.WriteAllText("hi");
        Assert.True(p.FileExists());
    }

    [Fact]
    public void EnsureDirectoryExists_Is_Idempotent()
    {
        var p = P("subdir");
        p.EnsureDirectoryExists();
        Assert.True(p.DirectoryExists());
        p.EnsureDirectoryExists();
        Assert.True(p.DirectoryExists());
    }

    [Fact]
    public void Delete_File_And_Directory_Polymorphic()
    {
        var f = P("f.txt").WriteAllText("");
        var d = P("d").EnsureDirectoryExists();
        f.Delete();
        d.Delete();
        Assert.False(f.FileExists());
        Assert.False(d.DirectoryExists());
    }

    [Fact]
    public void Delete_On_Missing_Path_Is_Noop()
    {
        var p = P("does-not-exist");
        p.Delete();  // should not throw
    }

    [Fact]
    public void CopyTo_File_Creates_Destination()
    {
        var src = P("src.txt").WriteAllText("source");
        var dst = P("subdir/dst.txt");
        src.CopyTo(dst);
        Assert.Equal("source", dst.ReadAllText());
        Assert.True(src.FileExists()); // still there
    }

    [Fact]
    public void CopyTo_Directory_Recursively_Copies_Contents()
    {
        var srcDir = P("src").EnsureDirectoryExists();
        (srcDir / "a.txt").WriteAllText("a");
        (srcDir / "nested").EnsureDirectoryExists();
        (srcDir / "nested/b.txt").WriteAllText("b");

        var dstDir = P("dst");
        srcDir.CopyTo(dstDir);

        Assert.Equal("a", (dstDir / "a.txt").ReadAllText());
        Assert.Equal("b", (dstDir / "nested/b.txt").ReadAllText());
    }

    [Fact]
    public void MoveTo_Removes_Source()
    {
        var src = P("src.txt").WriteAllText("x");
        var dst = P("dst.txt");
        src.MoveTo(dst);
        Assert.False(src.FileExists());
        Assert.Equal("x", dst.ReadAllText());
    }

    // ---- Hashing ----

    [Fact]
    public void Sha256_Returns_Lowercase_Hex_64_Chars()
    {
        var p = P("h.txt").WriteAllText("hello");
        var hash = p.Sha256();
        Assert.Equal(64, hash.Length);
        Assert.Equal(hash.ToLowerInvariant(), hash);
        Assert.True(hash.All(c => "0123456789abcdef".Contains(c)));
    }

    [Fact]
    public void Sha256_Of_String_Matches_File_Hash()
    {
        var p = P("h.txt").WriteAllText("hello");
        Assert.Equal(p.Sha256(), AbsolutePath.Sha256Of("hello"));
    }

    [Fact]
    public void Sha256_Of_Empty_String_Matches_Known_Value()
    {
        // Sanity: well-known SHA-256 of empty input.
        Assert.Equal("e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
            AbsolutePath.Sha256Of(""));
    }

    // ---- Children ----

    [Fact]
    public void EnumerateFiles_Returns_Top_Level_Files_Only()
    {
        var dir = P("d").EnsureDirectoryExists();
        (dir / "a.txt").WriteAllText("");
        (dir / "b.txt").WriteAllText("");
        (dir / "nested").EnsureDirectoryExists();
        (dir / "nested/c.txt").WriteAllText("");

        var files = dir.EnumerateFiles().Select(p => p.Name).OrderBy(n => n).ToList();
        Assert.Equal(["a.txt", "b.txt"], files);
    }

    [Fact]
    public void EnumerateDirectories_Returns_Top_Level_Subdirs_Only()
    {
        var dir = P("d").EnsureDirectoryExists();
        (dir / "a").EnsureDirectoryExists();
        (dir / "b").EnsureDirectoryExists();
        (dir / "a/nested").EnsureDirectoryExists();

        var dirs = dir.EnumerateDirectories().Select(p => p.Name).OrderBy(n => n).ToList();
        Assert.Equal(["a", "b"], dirs);
    }

    [Fact]
    public void EnumerateFiles_On_Missing_Directory_Is_Empty()
    {
        Assert.Empty(P("nonexistent").EnumerateFiles());
    }

    // ---- Globbing ----

    [Fact]
    public void GlobFiles_Single_Pattern_Recursive()
    {
        var dir = P("g").EnsureDirectoryExists();
        (dir / "a.cs").WriteAllText("");
        (dir / "nested/b.cs").WriteAllText("");
        (dir / "nested/c.txt").WriteAllText("");

        var found = dir.GlobFiles("**/*.cs").Select(p => p.Name).OrderBy(n => n).ToList();
        Assert.Equal(["a.cs", "b.cs"], found);
    }

    [Fact]
    public void GlobFiles_Multiple_Patterns_Are_Unioned()
    {
        var dir = P("g").EnsureDirectoryExists();
        (dir / "a.cs").WriteAllText("");
        (dir / "b.fs").WriteAllText("");
        (dir / "c.txt").WriteAllText("");

        var found = dir.GlobFiles("*.cs", "*.fs").Select(p => p.Name).OrderBy(n => n).ToList();
        Assert.Equal(["a.cs", "b.fs"], found);
    }

    [Fact]
    public void GlobFiles_On_Missing_Directory_Is_Empty()
    {
        Assert.Empty(P("nonexistent").GlobFiles("**/*"));
    }

    // ---- GlobDirectories (TAM-113) ----

    private void MkDir(string rel) => Directory.CreateDirectory(Path.Combine(_scratch, rel));
    private void MkFile(string rel)
    {
        var full = Path.Combine(_scratch, rel);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, "x");
    }

    [Fact]
    public void GlobDirectories_StarStar_Slash_Name_Matches_All_Depths()
    {
        // TAM-113: `**/bin` must match `bin` directories at any depth, not zero.
        MkDir("Foo/bin");
        MkDir("Foo/obj");
        MkDir("Foo/nested/bin");
        MkDir("Bar/bin");
        MkFile("Foo/bin/Foo.dll");

        var dir = AbsolutePath.Create(_scratch);
        var dirs = dir.GlobDirectories("**/bin").Select(p => p.Name).OrderBy(n => n).ToList();

        Assert.Contains("bin", dirs);
        // 3 bin directories total (Foo/bin, Foo/nested/bin, Bar/bin), all named "bin"
        Assert.Equal(3, dirs.Count);
    }

    [Fact]
    public void GlobDirectories_Multiple_Patterns_Unioned()
    {
        MkDir("Foo/bin");
        MkDir("Foo/obj");
        MkDir("Bar/bin");
        MkDir("Bar/obj");
        MkDir("Baz/src");

        var dir = AbsolutePath.Create(_scratch);
        var dirs = dir.GlobDirectories("**/bin", "**/obj").ToList();

        Assert.Equal(4, dirs.Count);  // Foo/bin, Foo/obj, Bar/bin, Bar/obj
    }

    [Fact]
    public void GlobDirectories_Star_Slash_Only_Matches_Top_Level()
    {
        MkDir("topbin");
        MkDir("Foo/bin");
        MkDir("Foo/topbin");

        var dir = AbsolutePath.Create(_scratch);
        // `*/topbin` matches only one-level-deep — Foo/topbin matches, topbin (root) does not.
        var dirs = dir.GlobDirectories("*/topbin").Select(p => p.Name).ToList();

        Assert.Single(dirs);
        Assert.Equal("topbin", dirs[0]);
    }

    [Fact]
    public void GlobDirectories_No_Matches_Returns_Empty()
    {
        MkDir("Foo/src");
        var dir = AbsolutePath.Create(_scratch);
        Assert.Empty(dir.GlobDirectories("**/never-matches"));
    }

    [Fact]
    public void GlobDirectories_Result_Paths_Are_Absolute_And_Real()
    {
        MkDir("Foo/bin");
        var dir = AbsolutePath.Create(_scratch);
        var dirs = dir.GlobDirectories("**/bin");

        Assert.Single(dirs);
        Assert.True(Path.IsPathRooted(dirs[0].Value));
        Assert.True(Directory.Exists(dirs[0].Value));
    }

    [Fact]
    public void GlobDirectories_On_Missing_Directory_Is_Empty()
    {
        Assert.Empty(P("nonexistent").GlobDirectories("**/*"));
    }

    [Fact]
    public void GlobDirectories_Deduplicates()
    {
        MkDir("Foo/bin");
        var dir = AbsolutePath.Create(_scratch);
        // Two overlapping patterns both match the same directory.
        var dirs = dir.GlobDirectories("**/bin", "Foo/bin").ToList();
        Assert.Single(dirs);
    }

    // ---- OS temp-path factories (TAM-202) ----

    [Fact]
    public void GetTempDirectoryRoot_Returns_Existing_Absolute_Path()
    {
        var root = AbsolutePath.GetTempDirectoryRoot();
        Assert.True(Path.IsPathRooted(root.Value));
        Assert.True(root.DirectoryExists());
    }

    [Fact]
    public void CreateTempDirectory_Creates_Unique_Dirs_Under_Temp_Root()
    {
        var a = AbsolutePath.CreateTempDirectory();
        var b = AbsolutePath.CreateTempDirectory();
        try
        {
            Assert.True(a.DirectoryExists());
            Assert.True(b.DirectoryExists());
            Assert.NotEqual(a.Value, b.Value);
            Assert.StartsWith(AbsolutePath.GetTempDirectoryRoot().Value, a.Value);
            Assert.Contains("tamp-", a.Name);
        }
        finally
        {
            try { Directory.Delete(a.Value, recursive: true); } catch { }
            try { Directory.Delete(b.Value, recursive: true); } catch { }
        }
    }

    [Fact]
    public void CreateTempDirectory_Honors_NamePrefix()
    {
        var p = AbsolutePath.CreateTempDirectory("mybuild");
        try
        {
            Assert.StartsWith("mybuild-", p.Name);
        }
        finally
        {
            try { Directory.Delete(p.Value, recursive: true); } catch { }
        }
    }

    [Fact]
    public void CreateTempDirectory_Whitespace_Prefix_Falls_Back_To_Default()
    {
        var p = AbsolutePath.CreateTempDirectory("   ");
        try
        {
            Assert.StartsWith("tamp-", p.Name);
        }
        finally
        {
            try { Directory.Delete(p.Value, recursive: true); } catch { }
        }
    }

    [Fact]
    public void CreateTempFile_Creates_Empty_File()
    {
        var p = AbsolutePath.CreateTempFile();
        try
        {
            Assert.True(p.FileExists());
            Assert.Equal(0, p.SizeBytes());
            Assert.Empty(p.Extension);
        }
        finally
        {
            try { File.Delete(p.Value); } catch { }
        }
    }

    [Theory]
    [InlineData(".pfx", ".pfx")]
    [InlineData("pfx",  ".pfx")]
    [InlineData(".tar.gz", ".gz")]   // GetExtension returns the LAST dot-segment
    public void CreateTempFile_Extension_With_Or_Without_Leading_Dot(string input, string expectedReportedExt)
    {
        var p = AbsolutePath.CreateTempFile(input);
        try
        {
            Assert.True(p.FileExists());
            Assert.Equal(expectedReportedExt, p.Extension);
        }
        finally
        {
            try { File.Delete(p.Value); } catch { }
        }
    }

    [Fact]
    public void CreateTempFile_Unique_Names_Under_Concurrency()
    {
        // 50 parallel calls — Guid.NewGuid collisions are vanishingly unlikely
        // but the test asserts the collection-of-unique-names contract directly.
        var paths = Enumerable.Range(0, 50).AsParallel()
            .Select(_ => AbsolutePath.CreateTempFile(".txt"))
            .ToList();
        try
        {
            Assert.Equal(paths.Count, paths.Select(p => p.Value).Distinct().Count());
            Assert.All(paths, p => Assert.True(p.FileExists()));
        }
        finally
        {
            foreach (var p in paths) { try { File.Delete(p.Value); } catch { } }
        }
    }

    // ---- Convenience aliases (TAM-202) ----

    [Fact]
    public void CreateDirectory_Aliases_EnsureDirectoryExists()
    {
        var d = P("alias-target");
        Assert.False(d.DirectoryExists());
        var result = d.CreateDirectory();
        Assert.True(d.DirectoryExists());
        Assert.Same(d, result);
        // Idempotent.
        d.CreateDirectory();
        Assert.True(d.DirectoryExists());
    }

    [Fact]
    public void EnsureParentDirectoryExists_Creates_Missing_Parent()
    {
        var deeplyNested = P("a/b/c/file.txt");
        Assert.False(deeplyNested.Parent!.DirectoryExists());
        deeplyNested.EnsureParentDirectoryExists();
        Assert.True(deeplyNested.Parent!.DirectoryExists());
    }

    [Fact]
    public void EnsureParentDirectoryExists_Returns_Self()
    {
        var p = P("ret/file.txt");
        Assert.Same(p, p.EnsureParentDirectoryExists());
    }

    // ---- Touch (TAM-202) ----

    [Fact]
    public void Touch_Creates_Missing_File_Including_Parent_Dirs()
    {
        var p = P("touched/dir/file.txt");
        Assert.False(p.FileExists());
        p.Touch();
        Assert.True(p.FileExists());
        Assert.Equal(0, p.SizeBytes());
    }

    [Fact]
    public void Touch_Updates_Mtime_On_Existing_File()
    {
        var p = P("existing.txt").WriteAllText("hello");
        var stale = DateTime.UtcNow.AddDays(-7);
        File.SetLastWriteTimeUtc(p.Value, stale);
        Assert.Equal(stale, File.GetLastWriteTimeUtc(p.Value));

        p.Touch();

        var fresh = File.GetLastWriteTimeUtc(p.Value);
        Assert.True(fresh > stale.AddDays(6), $"mtime not bumped: {fresh} vs {stale}");
        Assert.Equal("hello", p.ReadAllText()); // content preserved
    }

    // ---- AppendAllText (TAM-202) ----

    [Fact]
    public void AppendAllText_Creates_File_With_Initial_Content()
    {
        var p = P("appended.txt");
        p.AppendAllText("line1\n");
        Assert.Equal("line1\n", p.ReadAllText());
    }

    [Fact]
    public void AppendAllText_Appends_To_Existing()
    {
        var p = P("appended.txt").WriteAllText("line1\n");
        p.AppendAllText("line2\n");
        Assert.Equal("line1\nline2\n", p.ReadAllText());
    }

    // ---- SizeBytes (TAM-202) ----

    [Fact]
    public void SizeBytes_Reports_Accurate_File_Length()
    {
        var p = P("sized.bin").WriteAllBytes(new byte[] { 1, 2, 3, 4, 5 });
        Assert.Equal(5L, p.SizeBytes());
    }

    [Fact]
    public void SizeBytes_Throws_For_Missing_File()
    {
        var p = P("nope.txt");
        Assert.Throws<FileNotFoundException>(() => p.SizeBytes());
    }

    [Fact]
    public void SizeBytes_Throws_For_Directory()
    {
        var d = P("subdir").EnsureDirectoryExists();
        Assert.Throws<FileNotFoundException>(() => d.SizeBytes());
    }

    // ---- CopyToDirectory (TAM-202) ----

    [Fact]
    public void CopyToDirectory_Preserves_Filename_And_Creates_Dest()
    {
        var src = P("src.txt").WriteAllText("payload");
        var dstDir = P("dst");
        Assert.False(dstDir.DirectoryExists());
        var copied = src.CopyToDirectory(dstDir);
        Assert.Equal("src.txt", copied.Name);
        Assert.Equal(dstDir.Value, copied.Parent!.Value);
        Assert.Equal("payload", copied.ReadAllText());
    }

    [Fact]
    public void CopyToDirectory_Overwrite_True_Replaces_Existing()
    {
        var src = P("src.txt").WriteAllText("new");
        var dstDir = P("dst").EnsureDirectoryExists();
        (dstDir / "src.txt").WriteAllText("old");
        var copied = src.CopyToDirectory(dstDir, overwrite: true);
        Assert.Equal("new", copied.ReadAllText());
    }

    [Fact]
    public void CopyToDirectory_Overwrite_False_Throws_On_Existing()
    {
        var src = P("src.txt").WriteAllText("new");
        var dstDir = P("dst").EnsureDirectoryExists();
        (dstDir / "src.txt").WriteAllText("old");
        Assert.Throws<IOException>(() => src.CopyToDirectory(dstDir, overwrite: false));
    }

    [Fact]
    public void CopyToDirectory_Throws_When_Source_Not_File()
    {
        var dirAsSrc = P("realdir").EnsureDirectoryExists();
        var dstDir = P("dst");
        Assert.Throws<InvalidOperationException>(() => dirAsSrc.CopyToDirectory(dstDir));

        var missing = P("missing.txt");
        Assert.Throws<InvalidOperationException>(() => missing.CopyToDirectory(dstDir));
    }

    [Fact]
    public void CopyToDirectory_Null_Destination_Throws()
    {
        var src = P("src.txt").WriteAllText("");
        Assert.Throws<ArgumentNullException>(() => src.CopyToDirectory(null!));
    }
}
