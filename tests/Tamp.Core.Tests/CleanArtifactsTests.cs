using Xunit;

namespace Tamp.Core.Tests;

public sealed class CleanArtifactsTests : IDisposable
{
    private readonly string _scratch;

    public CleanArtifactsTests()
    {
        _scratch = Path.Combine(Path.GetTempPath(), "tamp-cleanartifacts-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_scratch);
    }

    public void Dispose()
    {
        try { Directory.Delete(_scratch, recursive: true); } catch { /* best effort */ }
    }

    /// <summary>Creates a real .slnx file with the named projects pointed at empty .csprojs.</summary>
    private Solution WriteSolution(params string[] projectRelativePaths)
    {
        var slnx = new System.Xml.Linq.XElement("Solution");
        foreach (var rel in projectRelativePaths)
        {
            slnx.Add(new System.Xml.Linq.XElement("Project", new System.Xml.Linq.XAttribute("Path", rel)));
            var projDir = Path.Combine(_scratch, Path.GetDirectoryName(rel) ?? "");
            Directory.CreateDirectory(projDir);
            File.WriteAllText(Path.Combine(_scratch, rel), "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        }
        var slnxPath = Path.Combine(_scratch, "Test.slnx");
        new System.Xml.Linq.XDocument(slnx).Save(slnxPath);
        return Solution.Load(AbsolutePath.Create(slnxPath));
    }

    private void MkDir(string rel) => Directory.CreateDirectory(Path.Combine(_scratch, rel));
    private void MkFile(string rel)
    {
        var full = Path.Combine(_scratch, rel);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, "x");
    }

    /// <summary>Concrete TampBuild subclass for testing — exposes CleanArtifacts via a wrapper.</summary>
    private sealed class TestBuild : TampBuild
    {
        public void RunCleanArtifacts(Solution? s = null, AbsolutePath? artifacts = null) => CleanArtifacts(s, artifacts);
        public Solution? ResolveSolution() => ResolveInjectedSolution();
    }

    // ---- Scope: deletes ONLY solution projects' bin/obj ----

    [Fact]
    public void Deletes_Solution_Projects_Bin_And_Obj_Directories()
    {
        var solution = WriteSolution("src/Foo/Foo.csproj", "src/Bar/Bar.csproj");
        MkDir("src/Foo/bin/Debug");
        MkDir("src/Foo/obj/Release");
        MkDir("src/Bar/bin");
        MkDir("src/Bar/obj");

        var build = new TestBuild();
        build.RunCleanArtifacts(solution, AbsolutePath.Create(Path.Combine(_scratch, "artifacts")));

        Assert.False(Directory.Exists(Path.Combine(_scratch, "src/Foo/bin")));
        Assert.False(Directory.Exists(Path.Combine(_scratch, "src/Foo/obj")));
        Assert.False(Directory.Exists(Path.Combine(_scratch, "src/Bar/bin")));
        Assert.False(Directory.Exists(Path.Combine(_scratch, "src/Bar/obj")));
    }

    [Fact]
    public void Does_NOT_Delete_node_modules_Bin_Directories()
    {
        // The whole reason this helper exists — guard against TAM-127's blast radius.
        var solution = WriteSolution("src/Foo/Foo.csproj");
        MkFile("node_modules/turbo/bin/turbo");
        MkFile("node_modules/prettier/bin/prettier.cjs");
        MkDir("src/Foo/bin");

        var build = new TestBuild();
        build.RunCleanArtifacts(solution, AbsolutePath.Create(Path.Combine(_scratch, "artifacts")));

        Assert.True(File.Exists(Path.Combine(_scratch, "node_modules/turbo/bin/turbo")));
        Assert.True(File.Exists(Path.Combine(_scratch, "node_modules/prettier/bin/prettier.cjs")));
        Assert.False(Directory.Exists(Path.Combine(_scratch, "src/Foo/bin")));  // confirms it DID delete the real one
    }

    [Fact]
    public void Does_NOT_Delete_Tracked_Source_Script_Bin_Dirs()
    {
        // Ruby/Node SDKs check in bin/console, bin/setup, bin/clean-dist.sh
        // as source files. These are NOT build outputs.
        var solution = WriteSolution("src/MyApp/MyApp.csproj");
        MkFile("sdk/highlight-next/bin/clean-dist.sh");
        MkFile("sdk/highlight-ruby/highlight/bin/console");
        MkDir("src/MyApp/bin");

        var build = new TestBuild();
        build.RunCleanArtifacts(solution, AbsolutePath.Create(Path.Combine(_scratch, "artifacts")));

        Assert.True(File.Exists(Path.Combine(_scratch, "sdk/highlight-next/bin/clean-dist.sh")));
        Assert.True(File.Exists(Path.Combine(_scratch, "sdk/highlight-ruby/highlight/bin/console")));
    }

    [Fact]
    public void Does_NOT_Delete_Tracked_Test_Fixtures_Outside_Solution_Projects()
    {
        // Playwright pre-fetched fixtures at unrelated bin paths must be preserved.
        var solution = WriteSolution("src/Foo/Foo.csproj");
        MkFile("vendored/playwright/bin/Debug/.playwright/node/win32_x64/node.exe");
        MkDir("src/Foo/bin");

        var build = new TestBuild();
        build.RunCleanArtifacts(solution, AbsolutePath.Create(Path.Combine(_scratch, "artifacts")));

        Assert.True(File.Exists(Path.Combine(_scratch, "vendored/playwright/bin/Debug/.playwright/node/win32_x64/node.exe")));
    }

    // ---- Artifacts directory ----

    [Fact]
    public void Deletes_Default_Artifacts_Directory_When_Present()
    {
        var solution = WriteSolution("src/Foo/Foo.csproj");
        // Override RootDirectory to our scratch for this test by passing artifactsDir explicitly.
        MkDir("artifacts/nupkgs");
        MkFile("artifacts/nupkgs/Foo.1.0.0.nupkg");

        var build = new TestBuild();
        build.RunCleanArtifacts(solution, AbsolutePath.Create(Path.Combine(_scratch, "artifacts")));

        Assert.False(Directory.Exists(Path.Combine(_scratch, "artifacts")));
    }

    [Fact]
    public void Custom_Artifacts_Dir_Override_Honored()
    {
        var solution = WriteSolution("src/Foo/Foo.csproj");
        MkDir("output/packages");
        MkDir("artifacts/should-survive");

        var build = new TestBuild();
        build.RunCleanArtifacts(solution, AbsolutePath.Create(Path.Combine(_scratch, "output")));

        Assert.False(Directory.Exists(Path.Combine(_scratch, "output")));
        Assert.True(Directory.Exists(Path.Combine(_scratch, "artifacts")));  // not the override
    }

    [Fact]
    public void Missing_Artifacts_Directory_Is_No_Op()
    {
        var solution = WriteSolution("src/Foo/Foo.csproj");
        var build = new TestBuild();
        // No artifacts/ at all — should not throw.
        var ex = Record.Exception(() => build.RunCleanArtifacts(solution, AbsolutePath.Create(Path.Combine(_scratch, "artifacts"))));
        Assert.Null(ex);
    }

    // ---- Idempotency ----

    [Fact]
    public void Re_Running_Is_A_No_Op()
    {
        var solution = WriteSolution("src/Foo/Foo.csproj");
        MkDir("src/Foo/bin");
        var build = new TestBuild();
        var artifacts = AbsolutePath.Create(Path.Combine(_scratch, "artifacts"));
        build.RunCleanArtifacts(solution, artifacts);

        var ex = Record.Exception(() => build.RunCleanArtifacts(solution, artifacts));
        Assert.Null(ex);
    }

    // ---- Solution resolution ----

    [Fact]
    public void Throws_When_No_Solution_Provided_And_None_Injected()
    {
        var build = new TestBuild();
        var ex = Assert.Throws<InvalidOperationException>(() => build.RunCleanArtifacts(null, null));
        Assert.Contains("Solution", ex.Message);
        Assert.Contains("[Solution]", ex.Message);
    }

    [Fact]
    public void Resolves_Injected_Solution_From_Field()
    {
        var solution = WriteSolution("src/Foo/Foo.csproj");
        MkDir("src/Foo/bin");

        var build = new BuildWithInjectedField { Solution = solution };
        Assert.Same(solution, build.ResolveSolution());

        // Verify it actually works in the CleanArtifacts call path too.
        build.RunCleanArtifacts(null, AbsolutePath.Create(Path.Combine(_scratch, "artifacts")));
        Assert.False(Directory.Exists(Path.Combine(_scratch, "src/Foo/bin")));
    }

    [Fact]
    public void Resolves_Injected_Solution_From_Property()
    {
        var solution = WriteSolution("src/Foo/Foo.csproj");
        var build = new BuildWithInjectedProperty(solution);
        Assert.Same(solution, build.ResolveSolution());
    }

    [Fact]
    public void Resolves_Injected_Solution_Tolerates_Private_Member()
    {
        var solution = WriteSolution("src/Foo/Foo.csproj");
        var build = new BuildWithPrivateField(solution);
        Assert.Same(solution, build.ResolveSolution());
    }

    [Fact]
    public void Explicit_Solution_Argument_Wins_Over_Injected()
    {
        var injected = WriteSolution("src/Injected/Injected.csproj");
        var explicit_ = WriteSolution("src/Explicit/Explicit.csproj");
        MkDir("src/Injected/bin");
        MkDir("src/Explicit/bin");

        var build = new BuildWithInjectedField { Solution = injected };
        build.RunCleanArtifacts(explicit_, AbsolutePath.Create(Path.Combine(_scratch, "artifacts")));

        Assert.True(Directory.Exists(Path.Combine(_scratch, "src/Injected/bin")));   // injected survives
        Assert.False(Directory.Exists(Path.Combine(_scratch, "src/Explicit/bin")));  // explicit was cleaned
    }

    private sealed class BuildWithInjectedField : TampBuild
    {
        public Solution? Solution;
        public void RunCleanArtifacts(Solution? s, AbsolutePath? a) => CleanArtifacts(s, a);
        public Solution? ResolveSolution() => ResolveInjectedSolution();
    }

    private sealed class BuildWithInjectedProperty : TampBuild
    {
        public BuildWithInjectedProperty(Solution s) { Solution = s; }
        public Solution Solution { get; }
        public void RunCleanArtifacts(Solution? s, AbsolutePath? a) => CleanArtifacts(s, a);
        public Solution? ResolveSolution() => ResolveInjectedSolution();
    }

    private sealed class BuildWithPrivateField : TampBuild
    {
#pragma warning disable CS0414 // The field is assigned but its value is never used (reflection).
        private readonly Solution _solution;
#pragma warning restore CS0414
        public BuildWithPrivateField(Solution s) { _solution = s; }
        public Solution? ResolveSolution() => ResolveInjectedSolution();
    }

    // ---- Self-deletion guard ----

    [Fact]
    public void Skips_Project_Whose_Bin_Contains_Entry_Assembly()
    {
        // The entry assembly under test is the xUnit runner, which lives somewhere in the
        // test bin. If we declare a synthetic project whose path makes its bin contain that
        // entry-assembly path, the guard must skip it.

        var entryLocation = System.Reflection.Assembly.GetEntryAssembly()?.Location;
        Assert.NotNull(entryLocation);  // Test infra precondition

        // Build a project root that "owns" the entry assembly's bin/.
        var entryDir = Path.GetDirectoryName(entryLocation!)!;
        // entryDir is something like .../bin/Release/net10.0; walk up until we find "bin"
        var binDir = entryDir;
        while (binDir is not null && Path.GetFileName(binDir) != "bin")
            binDir = Path.GetDirectoryName(binDir);
        Assert.NotNull(binDir);
        var fakeProjectRoot = Path.GetDirectoryName(binDir!)!;
        var fakeProjectFile = Path.Combine(fakeProjectRoot, "FakeRunning.csproj");

        // We need the project to be enumerable from a solution. Synthesize a slnx with the
        // real path of the running project.
        var slnxPath = Path.Combine(_scratch, "Test.slnx");
        var slnxContent = new System.Xml.Linq.XDocument(
            new System.Xml.Linq.XElement("Solution",
                new System.Xml.Linq.XElement("Project", new System.Xml.Linq.XAttribute("Path", fakeProjectFile))));
        slnxContent.Save(slnxPath);
        var solution = Solution.Load(AbsolutePath.Create(slnxPath));

        var binBefore = Directory.Exists(binDir);

        var build = new TestBuild();
        build.RunCleanArtifacts(solution, AbsolutePath.Create(Path.Combine(_scratch, "artifacts")));

        // The running test's bin/ MUST still exist; the guard skipped it.
        Assert.Equal(binBefore, Directory.Exists(binDir));
    }

    // ---- Solution.Projects projection ----

    [Fact]
    public void Does_Not_Touch_Sibling_Projects_With_Identical_Names_Outside_Solution()
    {
        // If the user has src/Foo and a stray src/UnreferencedFoo (not in the solution),
        // the unreferenced one survives.
        var solution = WriteSolution("src/Foo/Foo.csproj");
        MkDir("src/Foo/bin");
        MkDir("src/UnreferencedFoo/bin");

        var build = new TestBuild();
        build.RunCleanArtifacts(solution, AbsolutePath.Create(Path.Combine(_scratch, "artifacts")));

        Assert.False(Directory.Exists(Path.Combine(_scratch, "src/Foo/bin")));
        Assert.True(Directory.Exists(Path.Combine(_scratch, "src/UnreferencedFoo/bin")));
    }
}
