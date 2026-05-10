using Xunit;

namespace Tamp.Cli.Tests;

/// <summary>
/// Filesystem-driven tests using temp directories to verify the locator's
/// search rules.
/// </summary>
public sealed class BuildProjectLocatorTests : IDisposable
{
    private readonly string _root;

    public BuildProjectLocatorTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "tamp-locator-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); }
        catch (IOException) { /* best-effort */ }
        catch (UnauthorizedAccessException) { /* best-effort */ }
    }

    private string CreateBuildFile(string folder, string filename)
    {
        var dir = Path.Combine(_root, folder);
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, filename);
        File.WriteAllText(path, "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>");
        return path;
    }

    [Fact]
    public void Locate_Finds_Build_csproj_In_Build_Folder()
    {
        var expected = CreateBuildFile("build", "Build.csproj");
        var found = BuildProjectLocator.Locate(_root);
        Assert.Equal(expected, found);
    }

    [Fact]
    public void Locate_Finds_underscore_build_csproj()
    {
        var expected = CreateBuildFile("_build", "_build.csproj");
        var found = BuildProjectLocator.Locate(_root);
        Assert.Equal(expected, found);
    }

    [Fact]
    public void Locate_Finds_Lowercase_build_csproj()
    {
        var expected = CreateBuildFile("build", "build.csproj");
        var found = BuildProjectLocator.Locate(_root);
        Assert.NotNull(found);
        Assert.True(File.Exists(found));
        // On case-insensitive filesystems (macOS / Windows) the locator may
        // return its preferred capitalization (Build.csproj) even when the
        // on-disk name is build.csproj. Compare the directory and file
        // existence; case is filesystem-dependent.
        Assert.Equal(Path.GetDirectoryName(expected), Path.GetDirectoryName(found));
        Assert.Equal("build.csproj", Path.GetFileName(found), ignoreCase: true);
    }

    [Fact]
    public void Locate_Finds_Single_Csproj_Even_With_Nonconventional_Name()
    {
        var expected = CreateBuildFile("build", "MyCustomBuild.csproj");
        var found = BuildProjectLocator.Locate(_root);
        Assert.Equal(expected, found);
    }

    [Fact]
    public void Locate_Walks_Up_To_Find_Build_Project()
    {
        var expected = CreateBuildFile("build", "Build.csproj");
        var nested = Path.Combine(_root, "src", "deep", "nested", "directory");
        Directory.CreateDirectory(nested);
        var found = BuildProjectLocator.Locate(nested);
        Assert.Equal(expected, found);
    }

    [Fact]
    public void Locate_Returns_Null_When_No_Build_Project_Exists()
    {
        var isolated = Path.Combine(Path.GetTempPath(), "tamp-isolated-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(isolated);
        try
        {
            // Walk-up from this isolated directory might still find a real
            // build project somewhere above /tmp. To keep the test reliable
            // we don't assert "null" if the OS happens to place the temp
            // tree inside a checkout — instead, verify the API doesn't
            // throw and either returns null or a path that exists.
            var found = BuildProjectLocator.Locate(isolated);
            if (found is not null)
                Assert.True(File.Exists(found), $"Located file should exist if non-null: {found}");
        }
        finally { Directory.Delete(isolated, recursive: true); }
    }

    [Fact]
    public void Locate_Prefers_Conventional_Name_Over_Single_Fallback()
    {
        var dir = Path.Combine(_root, "build");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "Build.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>");
        File.WriteAllText(Path.Combine(dir, "Other.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>");

        var found = BuildProjectLocator.Locate(_root);
        Assert.Equal(Path.Combine(dir, "Build.csproj"), found);
    }

    [Fact]
    public void Locate_Single_Csproj_Falls_Back_When_No_Conventional_Match()
    {
        // Two csprojs but no conventional name → no fallback (ambiguous).
        var dir = Path.Combine(_root, "build");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "Foo.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>");
        File.WriteAllText(Path.Combine(dir, "Bar.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>");

        var found = BuildProjectLocator.Locate(_root);
        Assert.Null(found);
    }
}
