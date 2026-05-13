using System.Collections.Generic;
using Tamp.Scaffold;

namespace Tamp.Cli.Scaffold.Templates;

/// <summary>
/// Library-shaped scaffold: minimal targets (Clean / Restore / Compile / Test) plus
/// a typed <c>Pack</c> target that emits NuGet packages, and an optional <c>Publish</c>
/// target that pushes to a registry. Filed under TAM-122.
/// </summary>
/// <remarks>
/// Adopter signal:
/// <code>tamp init --template library</code>
/// Differs from <see cref="MinimalTemplate"/> by adding a typed Pack target sized for
/// the typical "we're shipping nupkgs" adopter — most common shape across the satellite
/// ecosystem and Tamp's own dogfood build.
/// </remarks>
public sealed class LibraryTemplate : IScaffoldTemplate
{
    public string Name => "library";
    public string Description => "Library scaffold: Clean / Restore / Compile / Test + typed Pack target with nupkg output dir.";
    public string MinimumTampCoreVersion => "1.3.0";

    public IEnumerable<FileSpec> Render(ScaffoldContext ctx)
    {
        yield return new FileSpec(ctx.RepoRoot / "build" / "Build.cs", RenderBuildCs(ctx), WriteMode.Create);
        yield return new FileSpec(ctx.RepoRoot / "build" / "Build.csproj", MinimalTemplate.RenderBuildCsproj(ctx), WriteMode.Create);
        yield return new FileSpec(ctx.RepoRoot / ".config" / "dotnet-tools.json", MinimalTemplate.RenderToolsJson(ctx), WriteMode.SkipIfExists);
        yield return new FileSpec(ctx.RepoRoot / "tamp.sh", MinimalTemplate.RenderTampSh(), WriteMode.SkipIfExists) { Executable = true };
        yield return new FileSpec(ctx.RepoRoot / "tamp.cmd", MinimalTemplate.RenderTampCmd(), WriteMode.SkipIfExists);
    }

    internal static string RenderBuildCs(ScaffoldContext ctx) => """
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

            Target Test => _ => _
                .DependsOn(Compile)
                .Executes(() => DotNet.Test(s => s
                    .SetProject(Solution.Path)
                    .SetConfiguration(Configuration)
                    .SetNoBuild(true)));

            Target Pack => _ => _
                .DependsOn(Compile)
                .Executes(() => DotNet.Pack(s => s
                    .SetProject(Solution.Path)
                    .SetConfiguration(Configuration)
                    .SetOutputDirectory(NupkgOut)
                    .SetNoBuild(true)));

            Target Default => _ => _
                .Default()
                .DependsOn(Pack);
        }

        """;
}
