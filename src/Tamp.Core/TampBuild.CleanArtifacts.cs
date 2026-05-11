using System.Reflection;

namespace Tamp;

public abstract partial class TampBuild
{
    /// <summary>
    /// Safely clean derived build artifacts: every solution-project's <c>bin/</c> and <c>obj/</c>
    /// plus the conventional <c>artifacts/</c> root. NEVER globs the repo tree — touches only
    /// paths derived from the typed <see cref="Solution"/> contract.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Replaces the <c>RootDirectory.GlobDirectories("**/bin", "**/obj")</c> pattern, which
    /// matches <c>node_modules/*/bin/</c>, tracked source script dirs (Ruby <c>bin/console</c>,
    /// Node <c>bin/clean-dist.sh</c>), and pre-fetched test fixtures (Playwright's
    /// <c>tests/&lt;proj&gt;/bin/Debug/.../.playwright/</c>) on any real monorepo. HoldFast trial
    /// friction #12: 531 tracked files lost in 14 seconds via that exact pattern.
    /// </para>
    /// <para>
    /// Self-deletion guard: skips the project whose entry assembly is currently executing.
    /// Detection via <see cref="Assembly.GetEntryAssembly"/>'s Location.
    /// </para>
    /// <para>
    /// Canonical usage:
    /// </para>
    /// <code>
    /// class Build : TampBuild
    /// {
    ///     [Solution] readonly Solution Solution = null!;
    ///     Target Clean => _ =&gt; _.Executes(() =&gt; CleanArtifacts());
    /// }
    /// </code>
    /// </remarks>
    /// <param name="solution">
    /// The solution whose projects to clean. When null, reflects over <c>this</c> for a
    /// <see cref="SolutionAttribute"/>-decorated <see cref="Solution"/> field/property and uses
    /// its value. Throws if neither is provided.
    /// </param>
    /// <param name="artifactsDir">
    /// Override the artifacts directory. Default: <c>RootDirectory / "artifacts"</c>.
    /// Pass <c>null</c> to use the default; pass an explicit <see cref="AbsolutePath"/> to override.
    /// </param>
    protected void CleanArtifacts(Solution? solution = null, AbsolutePath? artifactsDir = null)
    {
        solution ??= ResolveInjectedSolution()
            ?? throw new InvalidOperationException(
                "CleanArtifacts() needs a Solution — declare `[Solution] readonly Solution Solution = null!;` in your build class or pass one explicitly.");

        var entryLocation = Assembly.GetEntryAssembly()?.Location;

        foreach (var project in solution.Projects)
        {
            var projDir = project.Path.Parent;
            if (projDir is null) continue;

            var bin = projDir / "bin";
            var obj = projDir / "obj";

            // Self-deletion guard: don't delete the bin/obj of the project we're currently running from.
            if (entryLocation is not null && entryLocation.StartsWith(bin.Value, StringComparison.Ordinal))
                continue;

            if (bin.DirectoryExists()) bin.Delete();
            if (obj.DirectoryExists()) obj.Delete();
        }

        var artifacts = artifactsDir ?? RootDirectory / "artifacts";
        if (artifacts.DirectoryExists()) artifacts.Delete();
    }

    /// <summary>
    /// Reflects over the build instance for any field or property typed as <see cref="Solution"/>
    /// (typically decorated with <see cref="SolutionAttribute"/>). Returns the first non-null value
    /// found, or null when no Solution-typed member exists. Internal so tests can exercise it.
    /// </summary>
    internal Solution? ResolveInjectedSolution()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var type = GetType();

        foreach (var field in type.GetFields(flags))
        {
            if (field.FieldType == typeof(Solution) && field.GetValue(this) is Solution s)
                return s;
        }
        foreach (var property in type.GetProperties(flags))
        {
            if (property.PropertyType == typeof(Solution) && property.CanRead && property.GetValue(this) is Solution s)
                return s;
        }
        return null;
    }
}
