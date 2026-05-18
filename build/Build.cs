using Tamp;
using Tamp.DotNetCoverage.V18;
using Tamp.NetCli.V10;
using Tamp.SonarScanner.V10;
using Tamp.CycloneDx.V6;
using Tamp.OpenGrep.V1;
using Tamp.Sbom;
using Tamp.Sarif;
using Tamp.DependencyTrack.V1;
using Tamp.DefectDojo.V2;

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
    AbsolutePath SecurityDir => Artifacts / "security";
    AbsolutePath SbomFile => SecurityDir / "tamp-bom.json";
    AbsolutePath SarifFile => SecurityDir / "tamp-opengrep.sarif";

    // ----- Wave 1 security chain env-var inputs (TAM-243 / TAM-245). All
    //       OPTIONAL: when unset, the SecurityPush target no-ops cleanly so
    //       the producer half still dogfoods on every CI run regardless of
    //       whether DT / DD instances exist yet.

    // Initialiser =null silences CS0649 (the framework assigns these by reflection).
    [Parameter("Dependency-Track base URL.", EnvironmentVariable = "TAMP_DT_URL")]
    readonly string? DtUrl = null;

    [Secret("Dependency-Track API key.", EnvironmentVariable = "TAMP_DT_API_KEY")]
    readonly Secret? DtApiKey = null;

    [Parameter("Dependency-Track project UUID for this build.", EnvironmentVariable = "TAMP_DT_PROJECT_UUID")]
    readonly string? DtProjectUuid = null;

    [Parameter("DefectDojo base URL.", EnvironmentVariable = "TAMP_DD_URL")]
    readonly string? DdUrl = null;

    [Secret("DefectDojo API v2 token.", EnvironmentVariable = "TAMP_DD_TOKEN")]
    readonly Secret? DdToken = null;

    [Parameter("DefectDojo engagement id for this build.", EnvironmentVariable = "TAMP_DD_ENGAGEMENT_ID")]
    readonly int? DdEngagementId = null;

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

    // ----- Wave 1 security chain (TAM-245 dogfood). -----
    //
    // Producer targets (Sbom, SecurityScan) are always runnable and emit
    // artifacts to ./artifacts/security/. SecurityPush is opt-in: it
    // no-ops unless TAMP_DT_URL / TAMP_DD_URL are set, so the producer
    // half exercises on every CI run even before DT/DD instances exist
    // in the lab.

    Target Sbom => _ => _
        .Description("Generate a CycloneDX SBOM for the Tamp solution via dotnet-CycloneDX 6.x. Output: artifacts/security/tamp-bom.json.")
        .DependsOn(nameof(Restore))
        .Executes(() =>
        {
            SecurityDir.CreateDirectory();
            return CycloneDx.Generate(s => s
                .SetPath(Solution.Path)
                .SetOutputDirectory(SecurityDir)
                .SetFilename(SbomFile.Value.Substring(SecurityDir.Value.Length + 1))
                .SetFormat(CycloneDxFormat.Json)
                .SetRecursive(true)
                .SetExcludeTestProjects(true)
                .SetMetadataComponentName("Tamp")
                .SetWorkingDirectory(RootDirectory));
        });

    Target SecurityScan => _ => _
        .Description("Run OpenGrep over the Tamp source tree with the registry auto rules. Output: artifacts/security/tamp-opengrep.sarif.")
        .Executes(() =>
        {
            SecurityDir.CreateDirectory();
            return OpenGrep.Scan(s => s
                .AddTarget("src")
                .AddTarget("tests")
                .AddTarget("build")
                .AddConfig("auto")
                .SetOutputFile(SarifFile)
                .AddExclude("**/bin/**")
                .AddExclude("**/obj/**")
                .AddExclude("artifacts/**")
                .SetQuiet(true)
                .SetWorkingDirectory(RootDirectory));
        });

    Target SecurityPush => _ => _
        .Description("Push the SBOM to Dependency-Track and the SARIF + FPF to DefectDojo. Opt-in via TAMP_DT_URL / TAMP_DD_URL env vars; no-op when unset so producer-only builds stay green.")
        .DependsOn(nameof(Sbom), nameof(SecurityScan))
        .Executes(async () =>
        {
            string? exportedFpf = null;

            if (!string.IsNullOrEmpty(DtUrl) && DtApiKey is not null && !string.IsNullOrEmpty(DtProjectUuid))
            {
                Console.WriteLine($"[security] Uploading SBOM to Dependency-Track at {DtUrl} (project {DtProjectUuid})…");
                var dtSettings = new DependencyTrackSettings
                {
                    BaseUrl = new Uri(DtUrl),
                    ApiKey = DtApiKey,
                };
                using var dt = new DependencyTrackClient(dtSettings);
                var bom = SbomReader.LoadFromFile(SbomFile);
                var upload = await dt.UploadBomAsync(Guid.Parse(DtProjectUuid), bom);
                Console.WriteLine($"[security] DT upload token: {upload.Token}; waiting for async analysis…");
                var settled = await dt.WaitForAnalysisCompleteAsync(upload.Token, dtSettings.DefaultAnalysisTimeout, Backoff.Constant(TimeSpan.FromSeconds(3)));
                if (!settled)
                {
                    Console.WriteLine("[security] DT analysis didn't settle within budget; skipping findings export.");
                }
                else
                {
                    exportedFpf = await dt.ExportFindingsAsync(Guid.Parse(DtProjectUuid));
                    Console.WriteLine($"[security] DT findings exported ({exportedFpf.Length} bytes of FPF JSON).");
                }
            }
            else
            {
                Console.WriteLine("[security] TAMP_DT_URL / TAMP_DT_API_KEY / TAMP_DT_PROJECT_UUID not all set — skipping Dependency-Track push.");
            }

            if (!string.IsNullOrEmpty(DdUrl) && DdToken is not null && DdEngagementId is { } engagementId)
            {
                Console.WriteLine($"[security] Pushing findings to DefectDojo at {DdUrl} (engagement {engagementId})…");
                var ddSettings = new DefectDojoSettings
                {
                    BaseUrl = new Uri(DdUrl),
                    Token = DdToken,
                };
                using var dd = new DefectDojoClient(ddSettings);

                if (File.Exists(SarifFile))
                {
                    var sarifLog = SarifReader.LoadFromFile(SarifFile);
                    var sarifResult = await dd.ReimportSarifAsync(engagementId, sarifLog);
                    Console.WriteLine($"[security] DD SARIF reimport → test {sarifResult.TestId}");
                }

                if (exportedFpf is not null)
                {
                    var fpfResult = await dd.ReimportScanAsync(DefectDojoScanType.DependencyTrackFpf, engagementId, exportedFpf);
                    Console.WriteLine($"[security] DD FPF reimport → test {fpfResult.TestId}");
                }
            }
            else
            {
                Console.WriteLine("[security] TAMP_DD_URL / TAMP_DD_TOKEN / TAMP_DD_ENGAGEMENT_ID not all set — skipping DefectDojo push.");
            }
        });

    Target Security => _ => _
        .Description("End-to-end Wave 1 chain: Sbom + SecurityScan + (gated) SecurityPush. Run locally as `tamp security`.")
        .DependsOn(nameof(Sbom), nameof(SecurityScan), nameof(SecurityPush));

    Target Default => _ => _
        .DependsOn(nameof(Compile))
        .Description("Local-developer default: restore + build the solution.");
}
