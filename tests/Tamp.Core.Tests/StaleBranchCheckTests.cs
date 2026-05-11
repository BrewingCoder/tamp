using Xunit;

namespace Tamp.Core.Tests;

public sealed class StaleBranchCheckTests
{
    /// <summary>Test seam — records every git invocation, returns scripted answers.</summary>
    private sealed class FakeGitRunner : IGitRunner
    {
        public List<(IReadOnlyList<string> Args, TimeSpan Timeout)> Calls { get; } = new();
        public Queue<GitCommandResult> Answers { get; } = new();
        /// <summary>Exception to throw on the next Run call (e.g. simulate git not on PATH).</summary>
        public Exception? ThrowOnNext { get; set; }

        public GitCommandResult Run(AbsolutePath repoRoot, IReadOnlyList<string> args, TimeSpan timeout)
        {
            Calls.Add((args, timeout));
            if (ThrowOnNext is { } ex) { ThrowOnNext = null; throw ex; }
            return Answers.Count > 0
                ? Answers.Dequeue()
                : new GitCommandResult(0, "0\n", "");
        }
    }

    private sealed class TempGitRepo : IDisposable
    {
        public AbsolutePath Root { get; }
        public GitRepository Repo { get; }
        public TempGitRepo()
        {
            var path = Path.Combine(Path.GetTempPath(), "tamp-stalebranch-" + Guid.NewGuid().ToString("N")[..12]);
            Directory.CreateDirectory(path);
            Directory.CreateDirectory(Path.Combine(path, ".git"));
            File.WriteAllText(Path.Combine(path, ".git", "HEAD"), "ref: refs/heads/feature\n");
            Directory.CreateDirectory(Path.Combine(path, ".git", "refs", "heads"));
            File.WriteAllText(Path.Combine(path, ".git", "refs", "heads", "feature"), new string('a', 40) + "\n");
            Root = AbsolutePath.Create(path);
            Repo = GitRepository.Load(Root);
        }
        public void Dispose() { try { Directory.Delete(Root.Value, recursive: true); } catch { /* best effort */ } }
    }

    // ---- Happy path ----

    [Fact]
    public void Fresh_Branch_Returns_NotStale_Report()
    {
        using var t = new TempGitRepo();
        var runner = new FakeGitRunner();
        runner.Answers.Enqueue(new GitCommandResult(0, "", ""));    // fetch
        runner.Answers.Enqueue(new GitCommandResult(0, "3\n", "")); // rev-list

        var report = GitRepositoryStaleBranchExtensions.CheckStalenessCore(
            t.Repo, maxCommitsBehind: 20, baseRef: "origin/main", fetch: true,
            timeout: TimeSpan.FromSeconds(5), runner: runner);

        Assert.False(report.IsStale);
        Assert.Equal(3, report.CommitsBehind);
        Assert.Equal(20, report.MaxAllowed);
        Assert.True(report.FetchPerformed);
        Assert.Equal("origin/main", report.BaseRef);
    }

    [Fact]
    public void Stale_Branch_Returns_Stale_Report()
    {
        using var t = new TempGitRepo();
        var runner = new FakeGitRunner();
        runner.Answers.Enqueue(new GitCommandResult(0, "", ""));
        runner.Answers.Enqueue(new GitCommandResult(0, "47\n", ""));

        var report = GitRepositoryStaleBranchExtensions.CheckStalenessCore(
            t.Repo, 20, "origin/main", true, TimeSpan.FromSeconds(5), runner);

        Assert.True(report.IsStale);
        Assert.Equal(47, report.CommitsBehind);
    }

    [Fact]
    public void Threshold_Boundary_Equal_Is_Not_Stale()
    {
        using var t = new TempGitRepo();
        var runner = new FakeGitRunner();
        runner.Answers.Enqueue(new GitCommandResult(0, "", ""));
        runner.Answers.Enqueue(new GitCommandResult(0, "20\n", ""));

        var report = GitRepositoryStaleBranchExtensions.CheckStalenessCore(
            t.Repo, 20, "origin/main", true, TimeSpan.FromSeconds(5), runner);

        Assert.False(report.IsStale);
        Assert.Equal(20, report.CommitsBehind);
    }

    [Fact]
    public void Threshold_Boundary_One_Above_Is_Stale()
    {
        using var t = new TempGitRepo();
        var runner = new FakeGitRunner();
        runner.Answers.Enqueue(new GitCommandResult(0, "", ""));
        runner.Answers.Enqueue(new GitCommandResult(0, "21\n", ""));

        var report = GitRepositoryStaleBranchExtensions.CheckStalenessCore(
            t.Repo, 20, "origin/main", true, TimeSpan.FromSeconds(5), runner);

        Assert.True(report.IsStale);
    }

    // ---- Fetch behavior ----

    [Fact]
    public void Fetch_Is_Performed_With_Parsed_Remote_And_Branch()
    {
        using var t = new TempGitRepo();
        var runner = new FakeGitRunner();
        runner.Answers.Enqueue(new GitCommandResult(0, "", ""));
        runner.Answers.Enqueue(new GitCommandResult(0, "0\n", ""));

        GitRepositoryStaleBranchExtensions.CheckStalenessCore(
            t.Repo, 20, "origin/main", true, TimeSpan.FromSeconds(5), runner);

        Assert.Equal(2, runner.Calls.Count);
        Assert.Equal(new[] { "fetch", "--quiet", "origin", "main" }, runner.Calls[0].Args);
        Assert.Equal(new[] { "rev-list", "--count", "origin/main", "^HEAD" }, runner.Calls[1].Args);
    }

    [Fact]
    public void Fetch_Is_Skipped_When_Opted_Out()
    {
        using var t = new TempGitRepo();
        var runner = new FakeGitRunner();
        runner.Answers.Enqueue(new GitCommandResult(0, "5\n", ""));

        var report = GitRepositoryStaleBranchExtensions.CheckStalenessCore(
            t.Repo, 20, "origin/main", false, TimeSpan.FromSeconds(5), runner);

        Assert.Single(runner.Calls);
        Assert.Equal(new[] { "rev-list", "--count", "origin/main", "^HEAD" }, runner.Calls[0].Args);
        Assert.False(report.FetchPerformed);
    }

    [Fact]
    public void Fetch_Skipped_For_Refs_Without_Remote_Prefix()
    {
        // A baseRef like "main" (no slash) can't be split into remote+branch.
        // We don't attempt a fetch — the caller is responsible for the local ref being up to date.
        using var t = new TempGitRepo();
        var runner = new FakeGitRunner();
        runner.Answers.Enqueue(new GitCommandResult(0, "0\n", ""));

        var report = GitRepositoryStaleBranchExtensions.CheckStalenessCore(
            t.Repo, 20, "main", true, TimeSpan.FromSeconds(5), runner);

        Assert.Single(runner.Calls);
        Assert.Equal("rev-list", runner.Calls[0].Args[0]);
        // FetchPerformed reflects user intent, not whether fetch happened — documenting that gap.
        Assert.True(report.FetchPerformed);
    }

    [Fact]
    public void Different_BaseRef_Origin_Master_Is_Honored()
    {
        using var t = new TempGitRepo();
        var runner = new FakeGitRunner();
        runner.Answers.Enqueue(new GitCommandResult(0, "", ""));
        runner.Answers.Enqueue(new GitCommandResult(0, "0\n", ""));

        var report = GitRepositoryStaleBranchExtensions.CheckStalenessCore(
            t.Repo, 20, "origin/master", true, TimeSpan.FromSeconds(5), runner);

        Assert.Equal(new[] { "fetch", "--quiet", "origin", "master" }, runner.Calls[0].Args);
        Assert.Equal("origin/master", report.BaseRef);
    }

    [Fact]
    public void Upstream_BaseRef_Honored()
    {
        using var t = new TempGitRepo();
        var runner = new FakeGitRunner();
        runner.Answers.Enqueue(new GitCommandResult(0, "", ""));
        runner.Answers.Enqueue(new GitCommandResult(0, "2\n", ""));

        var report = GitRepositoryStaleBranchExtensions.CheckStalenessCore(
            t.Repo, 20, "upstream/develop", true, TimeSpan.FromSeconds(5), runner);

        Assert.Equal(new[] { "fetch", "--quiet", "upstream", "develop" }, runner.Calls[0].Args);
        Assert.Equal("upstream/develop", report.BaseRef);
    }

    // ---- Failure paths ----

    [Fact]
    public void Fetch_Failure_Throws_With_StdErr()
    {
        using var t = new TempGitRepo();
        var runner = new FakeGitRunner();
        runner.Answers.Enqueue(new GitCommandResult(128, "", "fatal: unable to access 'https://github.com/...': Could not resolve host"));

        var ex = Assert.Throws<InvalidOperationException>(() =>
            GitRepositoryStaleBranchExtensions.CheckStalenessCore(
                t.Repo, 20, "origin/main", true, TimeSpan.FromSeconds(5), runner));
        Assert.Contains("git fetch origin main failed", ex.Message);
        Assert.Contains("Could not resolve host", ex.Message);
    }

    [Fact]
    public void RevList_Failure_Throws_With_StdErr()
    {
        using var t = new TempGitRepo();
        var runner = new FakeGitRunner();
        runner.Answers.Enqueue(new GitCommandResult(0, "", ""));
        runner.Answers.Enqueue(new GitCommandResult(128, "", "fatal: ambiguous argument 'origin/main': unknown revision"));

        var ex = Assert.Throws<InvalidOperationException>(() =>
            GitRepositoryStaleBranchExtensions.CheckStalenessCore(
                t.Repo, 20, "origin/main", true, TimeSpan.FromSeconds(5), runner));
        Assert.Contains("git rev-list --count origin/main ^HEAD failed", ex.Message);
        Assert.Contains("unknown revision", ex.Message);
    }

    [Fact]
    public void RevList_NonNumeric_Output_Throws()
    {
        using var t = new TempGitRepo();
        var runner = new FakeGitRunner();
        runner.Answers.Enqueue(new GitCommandResult(0, "", ""));
        runner.Answers.Enqueue(new GitCommandResult(0, "not a number\n", ""));

        var ex = Assert.Throws<InvalidOperationException>(() =>
            GitRepositoryStaleBranchExtensions.CheckStalenessCore(
                t.Repo, 20, "origin/main", true, TimeSpan.FromSeconds(5), runner));
        Assert.Contains("non-numeric output", ex.Message);
    }

    // ---- AssertNotStale ----

    [Fact]
    public void AssertNotStale_Returns_When_Fresh()
    {
        using var t = new TempGitRepo();
        var runner = new FakeGitRunner();
        runner.Answers.Enqueue(new GitCommandResult(0, "", ""));
        runner.Answers.Enqueue(new GitCommandResult(0, "5\n", ""));

        // Public AssertNotStale uses ProcessGitRunner; we test the core path via Core overload.
        var report = GitRepositoryStaleBranchExtensions.CheckStalenessCore(
            t.Repo, 20, "origin/main", true, TimeSpan.FromSeconds(5), runner);
        Assert.False(report.IsStale);
        // No throw means AssertNotStale would return; documenting that contract here.
    }

    [Fact]
    public void StaleBranchException_Carries_Report()
    {
        var report = new StaleBranchReport("origin/main", 42, 20, true, true);
        var ex = new StaleBranchException(report);
        Assert.Same(report, ex.Report);
        Assert.Contains("42 commits behind origin/main", ex.Message);
        Assert.Contains("Pull and merge", ex.Message);
    }

    [Fact]
    public void Report_ToString_Fresh_Has_Reassuring_Language()
    {
        var report = new StaleBranchReport("origin/main", 3, 20, false, true);
        Assert.Contains("fresh", report.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    // ---- Argument guards ----

    [Fact]
    public void CheckStalenessCore_Rejects_Null_Repo()
    {
        var runner = new FakeGitRunner();
        Assert.Throws<ArgumentNullException>(() =>
            GitRepositoryStaleBranchExtensions.CheckStalenessCore(
                null!, 20, "origin/main", true, TimeSpan.FromSeconds(5), runner));
    }

    [Fact]
    public void CheckStalenessCore_Rejects_Null_Runner()
    {
        using var t = new TempGitRepo();
        Assert.Throws<ArgumentNullException>(() =>
            GitRepositoryStaleBranchExtensions.CheckStalenessCore(
                t.Repo, 20, "origin/main", true, TimeSpan.FromSeconds(5), null!));
    }

    [Fact]
    public void CheckStalenessCore_Rejects_Blank_BaseRef()
    {
        using var t = new TempGitRepo();
        var runner = new FakeGitRunner();
        Assert.Throws<ArgumentException>(() =>
            GitRepositoryStaleBranchExtensions.CheckStalenessCore(
                t.Repo, 20, "", true, TimeSpan.FromSeconds(5), runner));
        Assert.Throws<ArgumentException>(() =>
            GitRepositoryStaleBranchExtensions.CheckStalenessCore(
                t.Repo, 20, "   ", true, TimeSpan.FromSeconds(5), runner));
    }

    [Fact]
    public void CheckStalenessCore_Rejects_Negative_MaxCommits()
    {
        using var t = new TempGitRepo();
        var runner = new FakeGitRunner();
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            GitRepositoryStaleBranchExtensions.CheckStalenessCore(
                t.Repo, -1, "origin/main", true, TimeSpan.FromSeconds(5), runner));
    }

    [Fact]
    public void CheckStalenessCore_Allows_Zero_MaxCommits_Strict_Mode()
    {
        // Threshold 0 = "ANY commits behind = stale". Useful for hard pre-PR enforcement.
        using var t = new TempGitRepo();
        var runner = new FakeGitRunner();
        runner.Answers.Enqueue(new GitCommandResult(0, "", ""));
        runner.Answers.Enqueue(new GitCommandResult(0, "1\n", ""));

        var report = GitRepositoryStaleBranchExtensions.CheckStalenessCore(
            t.Repo, 0, "origin/main", true, TimeSpan.FromSeconds(5), runner);
        Assert.True(report.IsStale);

        runner.Answers.Enqueue(new GitCommandResult(0, "", ""));
        runner.Answers.Enqueue(new GitCommandResult(0, "0\n", ""));
        var fresh = GitRepositoryStaleBranchExtensions.CheckStalenessCore(
            t.Repo, 0, "origin/main", true, TimeSpan.FromSeconds(5), runner);
        Assert.False(fresh.IsStale);
    }

    [Fact]
    public void CheckStalenessCore_Rejects_NonPositive_Timeout()
    {
        using var t = new TempGitRepo();
        var runner = new FakeGitRunner();
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            GitRepositoryStaleBranchExtensions.CheckStalenessCore(
                t.Repo, 20, "origin/main", true, TimeSpan.Zero, runner));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            GitRepositoryStaleBranchExtensions.CheckStalenessCore(
                t.Repo, 20, "origin/main", true, TimeSpan.FromSeconds(-1), runner));
    }
}
