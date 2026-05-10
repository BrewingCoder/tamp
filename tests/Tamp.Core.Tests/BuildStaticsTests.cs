using Xunit;

namespace Tamp.Core.Tests;

public sealed class BuildStaticsTests
{
    [Fact]
    public void RootDirectory_Locates_The_Tamp_Repo_Root()
    {
        // Tests run from inside the Tamp repo, so RootDirectory should
        // resolve to a directory that contains either .git, the Tamp.slnx
        // file, or the .tamp dir we use as a marker.
        TampBuild.ResetCachedDirectories();
        var root = TampBuild.RootDirectory;
        Assert.NotNull(root);
        Assert.True(root.DirectoryExists(), $"Expected root to exist: {root}");

        var hasMarker = (root / ".git").DirectoryExists()
            || (root / "Tamp.slnx").FileExists()
            || (root / ".tamp").DirectoryExists();
        Assert.True(hasMarker, $"Expected a marker (.git, .slnx/.sln, or .tamp) at {root}");
    }

    [Fact]
    public void RootDirectory_Is_Cached_Across_Calls()
    {
        TampBuild.ResetCachedDirectories();
        var first = TampBuild.RootDirectory;
        var second = TampBuild.RootDirectory;
        Assert.Equal(first.Value, second.Value);
    }

    [Fact]
    public void TemporaryDirectory_Is_Created_On_Access()
    {
        TampBuild.ResetCachedDirectories();
        var temp = TampBuild.TemporaryDirectory;
        Assert.True(temp.DirectoryExists());
        Assert.EndsWith("temp", temp.Name);
        Assert.EndsWith(".tamp", temp.Parent!.Name);
    }
}
