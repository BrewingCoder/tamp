using Tamp;
using Tamp.DotNetCoverage.V18;
using Tamp.NetCli.V10;
using Tamp.SonarScanner.V10;

/// <summary>
/// Tamp's self-hosted build script — Tamp drives its own pipeline.
/// Run via <c>dotnet run --project build -- &lt;target&gt;</c> or, after
/// <c>dotnet tool install -g Tamp.Cli</c>, via <c>tamp &lt;target&gt;</c>.
/// </summary>
class Build : TampBuild
{
    public static int Main(string[] args) => Execute<Build>(args);

    [Parameter("Build configuration")]
    Configuration Configuration = IsLocalBuild ? Configuration.Debug : Configuration.Release;

    [Solution] readonly Solution Solution = null!;
    [GitRepository] readonly GitRepository Git = null!;

    [NuGetPackage("dotnet-coverage", Version = "18.6.2")]
    readonly Tool DotNetCoverageTool = null!;

    [NuGetPackage("dotnet-sonarscanner", Version = "10.4.1")]
    readonly Tool SonarTool = null!;

    // Resolved by SecretBinder from the SONAR_TOKEN env var (TAM-78,
    // shipped in Tamp.Core 1.0.1). CI masking fires automatically on
    // GitHub Actions / Azure DevOps when the value is bound.
    [Secret("SonarQube admin token", EnvironmentVariable = "SONAR_TOKEN")]
    readonly Secret SonarToken = null!;

    [Parameter("Sonar host URL", EnvironmentVariable = "SONAR_HOST_URL")]
    readonly string SonarHostUrl = "https://sonar.brewingcoder.com";

    [Parameter("Sonar project key")]
    readonly string SonarProjectKey = "tamp-build_tamp";

    AbsolutePath Artifacts => RootDirectory / "artifacts";
    AbsolutePath CoverageDir => Artifacts / "coverage";

    Target Info => _ => _
        .Description("Print build context (branch, commit, configuration) — useful at the top of CI logs.")
        .Executes(() =>
        {
            Console.WriteLine($"  Branch:        {Git.Branch ?? "<detached>"}");
            Console.WriteLine($"  Commit:        {Git.Commit[..7]}");
            Console.WriteLine($"  Configuration: {Configuration}");
            Console.WriteLine($"  Solution:      {Solution.Name} ({Solution.Projects.Count} project{(Solution.Projects.Count == 1 ? "" : "s")})");
            Console.WriteLine($"  Local build:   {IsLocalBuild}");
        });

    Target Clean => _ => _
        .Description("Delete bin/obj across the tree and the artifacts directory.")
        .Executes(() =>
        {
            foreach (var d in RootDirectory.GlobDirectories("**/bin", "**/obj"))
                d.Delete();
            Artifacts.Delete();
        });

    Target Restore => _ => _
        .Description("dotnet restore the solution.")
        .Executes(() => DotNet.Restore(s => s.SetProject(Solution.Path)));

    Target Compile => _ => _
        .DependsOn(nameof(Restore))
        .Description("dotnet build the solution.")
        .Executes(() => DotNet.Build(s => s
            .SetProject(Solution.Path)
            .SetConfiguration(Configuration)
            .SetNoRestore(true)));

    Target Test => _ => _
        .DependsOn(nameof(Compile))
        .Description("Run the test suite across all TFMs with two coverage collectors stacked.")
        // Two collectors:
        //  - "Code Coverage"      → dotnet-coverage / vstest data collector path.
        //                            Emits .coverage binary → cobertura via Merge.
        //                            Works on macOS arm64 (the standalone
        //                            dotnet-coverage collect verb does not —
        //                            Hardened Runtime strips CORECLR_PROFILER).
        //  - "XPlat Code Coverage" → Coverlet collector. Configured via
        //                            build/coverlet.runsettings to emit
        //                            OpenCover XML, which Sonar's .NET path
        //                            (sonar.cs.opencover.reportsPaths) wants.
        // Both produce report files under CoverageDir; SonarBegin reads the
        // .opencover.xml files; Coverage target merges the .coverage files.
        .Executes(() => DotNet.Test(s => s
            .SetProject(Solution.Path)
            .SetConfiguration(Configuration)
            .SetNoBuild(true)
            .AddLogger("trx;LogFileName=test-results.trx")
            .AddDataCollector("Code Coverage")
            .AddDataCollector("XPlat Code Coverage")
            .SetSettings((RootDirectory / "build" / "coverlet.runsettings").Value)
            .SetResultsDirectory(CoverageDir)));

    Target Coverage => _ => _
        .DependsOn(nameof(Test))
        .Description("Merge the per-test-run .coverage files and emit Cobertura XML for Sonar / coverage gates.")
        .Executes(() => DotNetCoverage.Merge(DotNetCoverageTool, m => m
            .AddInputs(CoverageDir.GlobFiles("**/*.coverage"))
            .SetOutput(CoverageDir / "coverage.cobertura.xml")
            .SetOutputFormat(CoverageFormat.Cobertura)));

    Target Pack => _ => _
        .DependsOn(nameof(Test))
        .Description("Pack all NuGet artifacts into ./artifacts (both Cli flavors).")
        .Executes(() => new[]
        {
            // Default flavor: Tamp.Core, Tamp.Cli (bare), Tamp.NetCli.V8/9/10, Tamp.DotNetCoverage.V18.
            // Third-party tool wrappers (Docker, Sonar, EF, GitVersion, etc.) ship from satellite repos.
            DotNet.Pack(s => s
                .SetProject(Solution.Path)
                .SetConfiguration(Configuration)
                .SetNoBuild(true)
                .SetOutput(Artifacts)),

            // Second pack for Tamp.Cli with the dotnet-verb flavor → produces dotnet-tamp.nupkg.
            DotNet.Pack(s => s
                .SetProject(RootDirectory / "src" / "Tamp.Cli" / "Tamp.Cli.csproj")
                .SetConfiguration(Configuration)
                .SetOutput(Artifacts)
                .SetProperty("CliFlavor", "DotnetVerb")),
        });

    Target Ci => _ => _
        .DependsOn(nameof(Info), nameof(Clean), nameof(Pack))
        .Description("Full CI pipeline: print info, clean, restore, build, test, pack.");

    // ----- Sonar -----
    //
    // SonarScanner for .NET is a two-phase invocation: Begin BEFORE the
    // build, End AFTER. The build (and tests, if you want coverage) run
    // between them so MSBuild can hand the analyzer its inputs. The flow
    // here mirrors what the wrapper docs recommend and what the integration
    // tests of Tamp.SonarScanner.V10 exercise.

    Target SonarBegin => _ => _
        .Description("Initialize the SonarScanner pre-build phase.")
        .Before(nameof(Compile))
        .Requires(() => SonarToken != null)
        .Executes(() => SonarScanner.Begin(SonarTool, s => s
            .SetProjectKey(SonarProjectKey)
            .SetHostUrl(SonarHostUrl)
            .SetToken(SonarToken)
            .SetProperty("sonar.cs.vstest.reportsPaths", $"{CoverageDir.Value}/**/*.trx")
            // Coverage via Coverlet → OpenCover XML (TAM-80). Glob picks up
            // one file per test project (lands at <results>/<guid>/coverage.opencover.xml).
            .SetProperty("sonar.cs.opencover.reportsPaths", $"{CoverageDir.Value}/**/coverage.opencover.xml")
            .SetProperty("sonar.exclusions", "**/bin/**,**/obj/**,artifacts/**,build/**,docs/**")
            // Coverage shouldn't count test code or build script:
            .SetProperty("sonar.coverage.exclusions", "tests/**,build/**,samples/**")
            // Tamp.NetCli.V8 / V9 are intentional sibling copies of V10 per
            // ADR 0002 — drop them from copy-paste detection so duplication
            // metrics reflect accidental dup, not the by-design pattern (TAM-82).
            .SetProperty("sonar.cpd.exclusions", "src/Tamp.NetCli.V8/**,src/Tamp.NetCli.V9/**")));

    Target SonarEnd => _ => _
        .Description("Finalize SonarScanner and submit results to the server.")
        .DependsOn(nameof(Test))
        .Requires(() => SonarToken != null)
        .Executes(() => SonarScanner.End(SonarTool, s => s.SetToken(SonarToken)));

    Target Sonar => _ => _
        .DependsOn(nameof(SonarBegin), nameof(SonarEnd))
        .Description("End-to-end Sonar scan: Begin (before Compile) → Compile → Test → End. Requires SONAR_TOKEN.");

    Target Default => _ => _
        .DependsOn(nameof(Compile))
        .Description("Local-developer default: restore + build the solution.");
}
