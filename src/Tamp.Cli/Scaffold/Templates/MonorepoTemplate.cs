using System.Collections.Generic;
using Tamp.Scaffold;

namespace Tamp.Cli.Scaffold.Templates;

/// <summary>
/// Monorepo-shaped scaffold: per-project Compile / Test fan-out using Tamp's
/// existing topological-order facilities, plus a default <c>Ci</c> aggregate
/// target that walks every solution member.
/// Filed under TAM-122.
/// </summary>
/// <remarks>
/// Adopter signal:
/// <code>tamp init --template monorepo</code>
/// Differs from <see cref="MinimalTemplate"/> in two ways:
/// <list type="bullet">
///   <item>The Test target enumerates every <c>*.Tests.csproj</c> in the solution
///         rather than running the whole solution at once — a single failing
///         project surfaces in the per-target output instead of being buried
///         in a 2000-line log.</item>
///   <item>A <c>Ci</c> aggregate runs Compile + Test + Pack as the entry point
///         CI runners invoke — Tamp picks the <c>Ci</c>-named target as default
///         when no <c>.Default()</c> is set.</item>
/// </list>
/// </remarks>
public sealed class MonorepoTemplate : IScaffoldTemplate
{
    public string Name => "monorepo";
    public string Description => "Monorepo scaffold: per-project Test fan-out + Ci aggregate, Pack output dir.";
    public string MinimumTampCoreVersion => "1.3.0";

    public IEnumerable<FileSpec> Render(ScaffoldContext ctx)
    {
        yield return new FileSpec(ctx.RepoRoot / "build" / "Build.cs", RenderBuildCs(ctx), WriteMode.Create);
        yield return new FileSpec(ctx.RepoRoot / "build" / "Build.csproj", MinimalTemplate.RenderBuildCsproj(ctx), WriteMode.Create);
        yield return new FileSpec(ctx.RepoRoot / ".config" / "dotnet-tools.json", MinimalTemplate.RenderToolsJson(ctx), WriteMode.SkipIfExists);
        yield return new FileSpec(ctx.RepoRoot / "tamp.sh", MinimalTemplate.RenderTampSh(), WriteMode.SkipIfExists) { Executable = true };
        yield return new FileSpec(ctx.RepoRoot / "tamp.cmd", MinimalTemplate.RenderTampCmd(), WriteMode.SkipIfExists);
    }

    internal static string RenderBuildCs(ScaffoldContext ctx)
        => ctx.SettingsStyle == SettingsStyle.Init
            ? RenderBuildCsInitStyle()
            : RenderBuildCsFluentStyle();

    private static string RenderBuildCsFluentStyle() => """
        using Tamp;
        using Tamp.NetCli.V10;

        class Build : TampBuild
        {
            public static int Main(string[] args) => Execute<Build>(args);

            [Parameter("Build configuration")]
            Configuration Configuration = IsLocalBuild ? Configuration.Debug : Configuration.Release;

            [Solution] readonly Solution Solution = null!;

            AbsolutePath Artifacts => RootDirectory / "artifacts";
            AbsolutePath NupkgOut => Artifacts / "nupkg";

            Target Clean => _ => _.Executes(() => CleanArtifacts());

            Target Restore => _ => _
                .Internal()
                .Executes(() => DotNet.Restore(s => s.SetProject(Solution.Path)));

            Target Compile => _ => _
                .DependsOn(Restore)
                .Executes(() => DotNet.Build(s => s
                    .SetProject(Solution.Path)
                    .SetConfiguration(Configuration)
                    .SetNoRestore(true)));

            // Fan-out: every *.Tests.csproj in the solution gets its own
            // dotnet test invocation. A single project's failure surfaces in
            // the per-target output instead of being buried in a giant log.
            Target Test => _ => _
                .DependsOn(Compile)
                .Executes(() => Solution.GlobFiles("**/*.Tests.csproj")
                    .Select(testProject => DotNet.Test(s => s
                        .SetProject(testProject)
                        .SetConfiguration(Configuration)
                        .SetNoBuild(true))));

            Target Pack => _ => _
                .DependsOn(Compile)
                .Executes(() => DotNet.Pack(s => s
                    .SetProject(Solution.Path)
                    .SetConfiguration(Configuration)
                    .SetOutputDirectory(NupkgOut)
                    .SetNoBuild(true)));

            // CI entry point — no .Default() set, so `tamp` with no args picks
            // the `Ci`-named target by convention. CI runners invoke `tamp Ci`
            // explicitly; this gives both the same behavior.
            Target Ci => _ => _
                .DependsOn(Compile, Test, Pack);
        }

        """;

    private static string RenderBuildCsInitStyle() => """
        using Tamp;
        using Tamp.NetCli.V10;

        class Build : TampBuild
        {
            public static int Main(string[] args) => Execute<Build>(args);

            [Parameter("Build configuration")]
            Configuration Configuration = IsLocalBuild ? Configuration.Debug : Configuration.Release;

            [Solution] readonly Solution Solution = null!;

            AbsolutePath Artifacts => RootDirectory / "artifacts";
            AbsolutePath NupkgOut => Artifacts / "nupkg";

            Target Clean => _ => _.Executes(() => CleanArtifacts());

            Target Restore => _ => _
                .Internal()
                .Executes(() => DotNet.Restore(new DotNetRestoreSettings
                {
                    Project = Solution.Path,
                }));

            Target Compile => _ => _
                .DependsOn(Restore)
                .Executes(() => DotNet.Build(new DotNetBuildSettings
                {
                    Project = Solution.Path,
                    Configuration = Configuration,
                    NoRestore = true,
                }));

            // Fan-out: every *.Tests.csproj in the solution gets its own
            // dotnet test invocation. A single project's failure surfaces in
            // the per-target output instead of being buried in a giant log.
            Target Test => _ => _
                .DependsOn(Compile)
                .Executes(() => Solution.GlobFiles("**/*.Tests.csproj")
                    .Select(testProject => DotNet.Test(new DotNetTestSettings
                    {
                        Project = testProject,
                        Configuration = Configuration,
                        NoBuild = true,
                    })));

            Target Pack => _ => _
                .DependsOn(Compile)
                .Executes(() => DotNet.Pack(new DotNetPackSettings
                {
                    Project = Solution.Path,
                    Configuration = Configuration,
                    OutputDirectory = NupkgOut,
                    NoBuild = true,
                }));

            // CI entry point — no .Default() set, so `tamp` with no args picks
            // the `Ci`-named target by convention. CI runners invoke `tamp Ci`
            // explicitly; this gives both the same behavior.
            Target Ci => _ => _
                .DependsOn(Compile, Test, Pack);
        }

        """;
}
