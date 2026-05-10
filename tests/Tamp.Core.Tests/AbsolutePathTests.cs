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
}
