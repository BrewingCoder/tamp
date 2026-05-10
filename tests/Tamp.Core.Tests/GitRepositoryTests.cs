using Xunit;

namespace Tamp.Core.Tests;

public sealed class GitRepositoryTests : IDisposable
{
    private readonly string _scratch;

    public GitRepositoryTests()
    {
        _scratch = Path.Combine(Path.GetTempPath(), "tamp-git-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_scratch);
    }

    public void Dispose()
    {
        try { Directory.Delete(_scratch, recursive: true); } catch { }
    }

    private void WriteFile(string relative, string content)
    {
        var p = Path.Combine(_scratch, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(p)!);
        File.WriteAllText(p, content);
    }

    private void Configure(
        string headContent,
        string? mainBranchSha = null,
        string? remoteUrl = null,
        string? packedRefs = null)
    {
        Directory.CreateDirectory(Path.Combine(_scratch, ".git"));
        WriteFile(".git/HEAD", headContent);
        if (mainBranchSha is not null)
        {
            Directory.CreateDirectory(Path.Combine(_scratch, ".git/refs/heads"));
            WriteFile(".git/refs/heads/main", mainBranchSha);
        }
        if (packedRefs is not null)
            WriteFile(".git/packed-refs", packedRefs);
        if (remoteUrl is not null)
        {
            WriteFile(".git/config",
                "[core]\n\trepositoryformatversion = 0\n" +
                "[remote \"origin\"]\n\turl = " + remoteUrl + "\n\tfetch = +refs/heads/*:refs/remotes/origin/*\n");
        }
    }

    [Fact]
    public void Loose_Ref_Is_Resolved_Through_Refs_Heads()
    {
        Configure(
            headContent: "ref: refs/heads/main\n",
            mainBranchSha: "abc1234567890abcdef1234567890abcdef12345",
            remoteUrl: "git@github.com:owner/repo.git");

        var repo = GitRepository.Load(AbsolutePath.Create(_scratch));
        Assert.Equal("main", repo.Branch);
        Assert.Equal("abc1234567890abcdef1234567890abcdef12345", repo.Commit);
        Assert.Equal("git@github.com:owner/repo.git", repo.RemoteUrl);
        Assert.False(repo.IsDetachedHead);
    }

    [Fact]
    public void Detached_Head_Reports_Sha_Without_Branch()
    {
        Configure(headContent: "abc1234567890abcdef1234567890abcdef12345\n");
        var repo = GitRepository.Load(AbsolutePath.Create(_scratch));
        Assert.Null(repo.Branch);
        Assert.True(repo.IsDetachedHead);
        Assert.Equal("abc1234567890abcdef1234567890abcdef12345", repo.Commit);
    }

    [Fact]
    public void Packed_Ref_Is_Resolved_When_Loose_Ref_Missing()
    {
        Configure(
            headContent: "ref: refs/heads/main\n",
            packedRefs:
                "# pack-refs with: peeled fully-peeled sorted \n" +
                "deadbeefdeadbeefdeadbeefdeadbeefdeadbeef refs/heads/main\n" +
                "abcdef0123456789abcdef0123456789abcdef01 refs/tags/v1.0.0\n");
        var repo = GitRepository.Load(AbsolutePath.Create(_scratch));
        Assert.Equal("main", repo.Branch);
        Assert.Equal("deadbeefdeadbeefdeadbeefdeadbeefdeadbeef", repo.Commit);
    }

    [Fact]
    public void Missing_Origin_Remote_Yields_Null_RemoteUrl()
    {
        Configure(
            headContent: "ref: refs/heads/main\n",
            mainBranchSha: "1234567890123456789012345678901234567890");
        var repo = GitRepository.Load(AbsolutePath.Create(_scratch));
        Assert.Null(repo.RemoteUrl);
    }

    [Fact]
    public void HTTPS_Remote_Url_Round_Trips()
    {
        Configure(
            headContent: "ref: refs/heads/main\n",
            mainBranchSha: "1234567890123456789012345678901234567890",
            remoteUrl: "https://github.com/owner/repo.git");
        var repo = GitRepository.Load(AbsolutePath.Create(_scratch));
        Assert.Equal("https://github.com/owner/repo.git", repo.RemoteUrl);
    }

    [Fact]
    public void Walks_Up_From_Subdirectory_To_Find_Git_Root()
    {
        Configure(
            headContent: "ref: refs/heads/main\n",
            mainBranchSha: "1234567890123456789012345678901234567890");
        var nested = Path.Combine(_scratch, "src", "deep", "nested");
        Directory.CreateDirectory(nested);
        var repo = GitRepository.Load(AbsolutePath.Create(nested));
        Assert.Equal(_scratch, repo.Root.Value);
    }

    [Fact]
    public void Throws_When_No_Git_Directory_Found()
    {
        var nonGitDir = Path.Combine(_scratch, "no-git");
        Directory.CreateDirectory(nonGitDir);
        Assert.Throws<InvalidOperationException>(
            () => GitRepository.Load(AbsolutePath.Create(nonGitDir)));
    }

    [Fact]
    public void Multiple_Remote_Sections_Pick_Origin_Only()
    {
        Configure(headContent: "ref: refs/heads/main\n", mainBranchSha: "1234567890123456789012345678901234567890");
        // Overwrite the config with multiple remotes.
        WriteFile(".git/config",
            "[remote \"upstream\"]\n\turl = https://example.com/upstream.git\n" +
            "[remote \"origin\"]\n\turl = https://example.com/origin.git\n" +
            "[remote \"backup\"]\n\turl = https://example.com/backup.git\n");
        var repo = GitRepository.Load(AbsolutePath.Create(_scratch));
        Assert.Equal("https://example.com/origin.git", repo.RemoteUrl);
    }

    // ---- Live test against the Tamp repo itself ----

    [Fact]
    public void Loads_Against_The_Tamp_Repo()
    {
        TampBuild.ResetCachedDirectories();
        var repo = GitRepository.Load(TampBuild.RootDirectory);
        Assert.NotNull(repo.Commit);
        Assert.Equal(40, repo.Commit.Length);
    }
}
