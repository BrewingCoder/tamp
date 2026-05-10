using Tamp;
using Tamp.NetCli.V10;

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

    AbsolutePath Artifacts => RootDirectory / "artifacts";

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
        .TopLevel()
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
        .TopLevel()
        .DependsOn(nameof(Restore))
        .Description("dotnet build the solution.")
        .Executes(() => DotNet.Build(s => s
            .SetProject(Solution.Path)
            .SetConfiguration(Configuration)
            .SetNoRestore(true)));

    Target Test => _ => _
        .TopLevel()
        .DependsOn(nameof(Compile))
        .Description("Run the test suite across all TFMs.")
        .Executes(() => DotNet.Test(s => s
            .SetProject(Solution.Path)
            .SetConfiguration(Configuration)
            .SetNoBuild(true)
            .AddLogger("trx;LogFileName=test-results.trx")
            .SetResultsDirectory(Artifacts / "test-results")));

    Target Pack => _ => _
        .TopLevel()
        .DependsOn(nameof(Test))
        .Description("Pack all NuGet artifacts into ./artifacts (both Cli flavors).")
        .Executes(() => new[]
        {
            // Default flavor: Tamp.Core, Tamp.Cli (bare), Tamp.NetCli.V8/9/10, Tamp.Docker.V27.
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
        .TopLevel()
        .DependsOn(nameof(Info), nameof(Clean), nameof(Pack))
        .Description("Full CI pipeline: print info, clean, restore, build, test, pack.");

    Target Default => _ => _
        .DependsOn(nameof(Compile))
        .Description("Local-developer default: restore + build the solution.");
}
