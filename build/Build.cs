using Tamp;
using Tamp.DotNetCoverage.V18;
using Tamp.NetCli.V10;
using Tamp.SonarScanner.V10;
using Tamp.CycloneDx.V6;
using Tamp.OpenGrep.V1;
using Tamp.OsvScanner.V2;
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
    // CycloneDX canonical filename pattern (*.cdx.json). osv-scanner 2.x
    // refuses to extract from non-canonical names like "tamp-bom.json".
    AbsolutePath SbomFile => SecurityDir / "tamp.cdx.json";
    AbsolutePath SarifOpenGrepFile => SecurityDir / "tamp-opengrep.sarif";
    AbsolutePath SarifRoslynDir => SecurityDir / "roslyn";
    AbsolutePath SarifRoslynFile => SecurityDir / "tamp-roslyn.sarif";
    AbsolutePath SarifSastFile => SecurityDir / "tamp-sast.sarif";
    AbsolutePath SarifCveFile => SecurityDir / "tamp-cve.sarif";

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
        .Description("Generate a CycloneDX SBOM for the Tamp solution via dotnet-CycloneDX 6.x. Output: artifacts/security/tamp.cdx.json. Pinned to spec version 1.6 (osv-scanner 2.3.8 doesn't yet accept 1.7) and the canonical *.cdx.json filename osv-scanner's extractor requires.")
        .DependsOn(nameof(Restore))
        .Executes(() =>
        {
            SecurityDir.CreateDirectory();
            return CycloneDx.Generate(s => s
                .SetPath(Solution.Path)
                .SetOutputDirectory(SecurityDir)
                .SetFilename("tamp.cdx.json")
                .SetFormat(CycloneDxFormat.Json)
                .SetSpecVersion("1.6")
                .SetRecursive(true)
                .SetExcludeTestProjects(true)
                .SetMetadataComponentName("Tamp")
                .SetWorkingDirectory(RootDirectory));
        });

    Target SecurityScanOpenGrep => _ => _
        .Description("Pattern-based SAST: OpenGrep over the Tamp source tree. Output: artifacts/security/tamp-opengrep.sarif.")
        .Executes(() =>
        {
            SecurityDir.CreateDirectory();
            return OpenGrep.Scan(s => s
                .AddTarget("src")
                .AddTarget("tests")
                .AddTarget("build")
                .AddConfig("auto")
                .SetOutputFile(SarifOpenGrepFile)
                .AddExclude("**/bin/**")
                .AddExclude("**/obj/**")
                .AddExclude("artifacts/**")
                .SetQuiet(true)
                .SetWorkingDirectory(RootDirectory));
        });

    Target SecurityScanRoslyn => _ => _
        .Description("Semantic SAST: SonarAnalyzer.CSharp + Roslynator + NetAnalyzers via /p:ErrorLog. Produces one SARIF per (project, TFM) pair under artifacts/security/roslyn/, then merges to tamp-roslyn.sarif. TreatWarningsAsErrors is overridden to false so findings flow to SARIF without breaking the build. Multi-TFM projects emit one SARIF per TFM; identical findings across TFMs dedup at the DefectDojo end.")
        .DependsOn(nameof(Restore))
        .Executes(() =>
        {
            SecurityDir.CreateDirectory();
            SarifRoslynDir.CreateDirectory();
            // Clear per-(project, TFM) SARIFs — ErrorLog opens for write but
            // stale files from a prior wider build (e.g. before a project was
            // removed) would otherwise linger and skew the merge.
            foreach (var f in SarifRoslynDir.GlobFiles("*.sarif")) f.Delete();

            return DotNet.Build(s => s
                .SetProject(Solution.Path)
                .SetConfiguration(Configuration)
                .SetProperty("IncludeSecurityAnalyzers", "true")
                .SetProperty("TreatWarningsAsErrors", "false")
                .SetNoIncremental(true));
        });

    Target SecurityScan => _ => _
        .Description("Combine SAST sources: OpenGrep SARIF + per-project Roslyn SARIFs → artifacts/security/tamp-sast.sarif. Uses SarifMerge.CombineDistinct to collapse the per-TFM duplication a multi-target Roslyn build emits — same source-line finding appears once per TFM otherwise. Runs are preserved so the tool-of-origin per finding stays identifiable downstream.")
        .DependsOn(nameof(SecurityScanOpenGrep), nameof(SecurityScanRoslyn))
        .Executes(() =>
        {
            var logs = new List<SarifLog>();

            if (File.Exists(SarifOpenGrepFile))
            {
                logs.Add(SarifReader.LoadFromFile(SarifOpenGrepFile));
            }

            var roslynSarifs = SarifRoslynDir.GlobFiles("*.sarif").ToList();
            foreach (var sarif in roslynSarifs)
            {
                logs.Add(SarifReader.LoadFromFile(sarif));
            }

            var merged = SarifMerge.CombineDistinct(logs);
            // Also merge the per-project Roslyn SARIFs alone so adopters who
            // only want the Roslyn slice can grab one file.
            if (roslynSarifs.Count > 0)
            {
                var roslynOnly = SarifMerge.CombineDistinct(roslynSarifs.Select(f => SarifReader.LoadFromFile(f)));
                SarifWriter.WriteToFile(roslynOnly, SarifRoslynFile);
            }

            SarifWriter.WriteToFile(merged, SarifSastFile);

            var totalResults = merged.Runs.Sum(r => r.Results?.Count ?? 0);
            Console.WriteLine($"[security] Merged {logs.Count} SARIF source(s) → {SarifSastFile.Value} ({merged.Runs.Count} runs, {totalResults} distinct findings)");
        });

    Target SecurityScanCveSbom => _ => _
        .Description("Cross-ecosystem SCA: osv-scanner reads the CycloneDX BOM and queries OSV.dev (npm/PyPI/Cargo/Go/Maven/NuGet/Packagist/Pub). Output: artifacts/security/tamp-cve.sarif. Kept separate from tamp-sast.sarif so DefectDojo can route SAST and SCA findings to different triage queues.")
        .DependsOn(nameof(Sbom))
        .Executes(() =>
        {
            SecurityDir.CreateDirectory();
            return OsvScanner.ScanSource(s => s
                .SetSbomFile(SbomFile)
                .SetOutputFile(SarifCveFile)
                .SetFormat(OsvScannerFormat.Sarif)
                .SetAllowNoLockfiles(true)
                .SetWorkingDirectory(RootDirectory));
        });

    Target SecurityPush => _ => _
        .Description("Push the SBOM to Dependency-Track and the merged SAST SARIF + CVE SARIF + DT FPF to DefectDojo. Opt-in via TAMP_DT_URL / TAMP_DD_URL env vars; no-op when unset so producer-only builds stay green.")
        .DependsOn(nameof(Sbom), nameof(SecurityScan), nameof(SecurityScanCveSbom))
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

                // DD's reimport-scan needs an existing test to reimport into.
                // For first-push idempotency, pass product_name + engagement_name
                // + test_title with auto_create_context=true and DD finds-or-creates.
                var sastOptions = new DefectDojoImportOptions
                {
                    ProductName = "Tamp",
                    EngagementName = "Tamp CI",
                    TestTitle = "Tamp SAST (OpenGrep + Roslyn)",
                };
                var cveOptions = sastOptions with { TestTitle = "Tamp SCA (osv-scanner)" };
                var fpfOptions = sastOptions with { TestTitle = "Tamp SCA (Dependency-Track FPF)" };

                if (File.Exists(SarifSastFile))
                {
                    var sarifLog = SarifReader.LoadFromFile(SarifSastFile);
                    var sarifResult = await dd.ReimportSarifAsync(engagementId, sarifLog, sastOptions);
                    Console.WriteLine($"[security] DD SAST SARIF reimport → test {sarifResult.TestId}");
                }

                if (File.Exists(SarifCveFile))
                {
                    var cveLog = SarifReader.LoadFromFile(SarifCveFile);
                    var cveResult = await dd.ReimportSarifAsync(engagementId, cveLog, cveOptions);
                    Console.WriteLine($"[security] DD CVE SARIF reimport → test {cveResult.TestId}");
                }

                if (exportedFpf is not null)
                {
                    var fpfResult = await dd.ReimportScanAsync(DefectDojoScanType.DependencyTrackFpf, engagementId, exportedFpf, fpfOptions);
                    Console.WriteLine($"[security] DD FPF reimport → test {fpfResult.TestId}");
                }
            }
            else
            {
                Console.WriteLine("[security] TAMP_DD_URL / TAMP_DD_TOKEN / TAMP_DD_ENGAGEMENT_ID not all set — skipping DefectDojo push.");
            }
        });

    Target Security => _ => _
        .Description("End-to-end Wave 1+2 chain: Sbom + SecurityScan (SAST: OpenGrep + Roslyn merge) + SecurityScanCveSbom (SCA: osv-scanner) + (gated) SecurityPush. Run locally as `tamp Security`.")
        .DependsOn(nameof(Sbom), nameof(SecurityScan), nameof(SecurityScanCveSbom), nameof(SecurityPush));

    Target Default => _ => _
        .DependsOn(nameof(Compile))
        .Description("Local-developer default: restore + build the solution.");
}
