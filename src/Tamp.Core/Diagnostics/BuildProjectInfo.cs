using System;
using System.IO;
using System.Reflection;

namespace Tamp.Diagnostics;

/// <summary>
/// Resolved project identification for a single build invocation. Sourced
/// from <see cref="BuildProjectAttribute"/> when present, otherwise from
/// language-agnostic fallback heuristics. Works for any build regardless of
/// stack — there's no requirement that the build target a .NET solution.
/// </summary>
public sealed record BuildProjectInfo
{
    public required string Name { get; init; }
    public string? Area { get; init; }

    /// <summary>How the project name was resolved. Surfaced on the build span for diagnosability.</summary>
    public required ProjectNameSource NameSource { get; init; }

    /// <summary>
    /// Resolve project identification from a build-class type. Precedence:
    /// <list type="number">
    ///   <item><see cref="BuildProjectAttribute"/> — language-agnostic, recommended.</item>
    ///   <item>Resolved <c>[Solution]</c> filename sans extension — .NET-specific; only when the build declares one.</item>
    ///   <item>Repository root directory name — works for any language stack.</item>
    ///   <item><c>"unknown"</c> literal — last-resort sentinel.</item>
    /// </list>
    /// Tamp does NOT require a .slnx / .sln to function; pure-JS, Python,
    /// Rust, mixed-stack builds all bypass step 2 and land on the repo-dir
    /// fallback (or set the attribute explicitly).
    /// </summary>
    /// <param name="buildType">The build script's class (the <c>T</c> in <c>Execute&lt;T&gt;</c>).</param>
    /// <param name="solutionPath">Resolved <c>[Solution]</c> path. Null when no solution is declared or the build doesn't target .NET.</param>
    /// <param name="repoRoot">Repository root directory, if known.</param>
    public static BuildProjectInfo Resolve(Type buildType, string? solutionPath, string? repoRoot)
    {
        if (buildType is null) throw new ArgumentNullException(nameof(buildType));

        var attr = buildType.GetCustomAttribute<BuildProjectAttribute>(inherit: true);
        if (attr is not null)
        {
            return new BuildProjectInfo
            {
                Name = attr.Name,
                Area = attr.Area,
                NameSource = ProjectNameSource.Attribute,
            };
        }

        if (!string.IsNullOrEmpty(solutionPath))
        {
            var fileName = Path.GetFileNameWithoutExtension(solutionPath);
            if (!string.IsNullOrWhiteSpace(fileName))
            {
                return new BuildProjectInfo
                {
                    Name = fileName,
                    NameSource = ProjectNameSource.Solution,
                };
            }
        }

        if (!string.IsNullOrEmpty(repoRoot))
        {
            var dirName = new DirectoryInfo(repoRoot).Name;
            if (!string.IsNullOrWhiteSpace(dirName))
            {
                return new BuildProjectInfo
                {
                    Name = dirName,
                    NameSource = ProjectNameSource.RepoDirectory,
                };
            }
        }

        return new BuildProjectInfo
        {
            Name = "unknown",
            NameSource = ProjectNameSource.Default,
        };
    }
}

/// <summary>Which fallback step produced the project name. Pinned vocabulary — surfaced as a tag.</summary>
public enum ProjectNameSource
{
    /// <summary><see cref="BuildProjectAttribute"/> was present on the build class.</summary>
    Attribute,

    /// <summary>Resolved from the <c>[Solution]</c> filename (sans extension).</summary>
    Solution,

    /// <summary>Resolved from the repository root directory name.</summary>
    RepoDirectory,

    /// <summary>None of the above worked; the literal string <c>"unknown"</c> was used.</summary>
    Default,
}
