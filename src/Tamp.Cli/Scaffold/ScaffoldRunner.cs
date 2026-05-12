using System.Collections.Generic;
using System.IO;
using Tamp.Scaffold;

namespace Tamp.Cli.Scaffold;

/// <summary>
/// Outcome of a single <see cref="FileSpec"/> write attempt by <see cref="ScaffoldRunner"/>.
/// </summary>
public enum FileWriteOutcome
{
    /// <summary>File was written (didn't exist before).</summary>
    Written,
    /// <summary>File already existed and <see cref="WriteMode.SkipIfExists"/> told the runner to leave it.</summary>
    Skipped,
    /// <summary>Would-write under <c>--dry-run</c>; nothing touched on disk.</summary>
    Planned,
}

/// <summary>One row in the runner's report.</summary>
public sealed record FileWriteResult(AbsolutePath Path, FileWriteOutcome Outcome);

/// <summary>
/// Applies a sequence of <see cref="FileSpec"/>s to the filesystem, honoring
/// each spec's <see cref="WriteMode"/>. Returns a per-file outcome list.
/// </summary>
public sealed class ScaffoldRunner
{
    private readonly bool _dryRun;
    public ScaffoldRunner(bool dryRun = false) => _dryRun = dryRun;

    public IReadOnlyList<FileWriteResult> Run(IEnumerable<FileSpec> specs)
    {
        var results = new List<FileWriteResult>();
        foreach (var spec in specs)
        {
            if (spec.Mode == WriteMode.Merge)
                throw new System.NotSupportedException(
                    $"FileSpec.Mode = Merge is reserved for v0.2.0+; spec at '{spec.Path}' cannot be applied by v0.1.0.");

            var exists = File.Exists(spec.Path.Value);

            if (exists && spec.Mode == WriteMode.Create)
                throw new System.IO.IOException(
                    $"Refusing to overwrite existing file '{spec.Path.Value}'. Use --force (v0.2.0+) to override.");

            if (exists && spec.Mode == WriteMode.SkipIfExists)
            {
                results.Add(new FileWriteResult(spec.Path, FileWriteOutcome.Skipped));
                continue;
            }

            if (_dryRun)
            {
                results.Add(new FileWriteResult(spec.Path, FileWriteOutcome.Planned));
                continue;
            }

            var parent = Path.GetDirectoryName(spec.Path.Value);
            if (!string.IsNullOrEmpty(parent)) Directory.CreateDirectory(parent);
            File.WriteAllText(spec.Path.Value, spec.Content);

            if (spec.Executable && !System.OperatingSystem.IsWindows())
            {
                // 0755 — owner rwx, group/other rx. POSIX-only; no-op on Windows
                // because shell scripts there execute via the .cmd extension association.
                File.SetUnixFileMode(spec.Path.Value,
                    System.IO.UnixFileMode.UserRead | System.IO.UnixFileMode.UserWrite | System.IO.UnixFileMode.UserExecute
                  | System.IO.UnixFileMode.GroupRead | System.IO.UnixFileMode.GroupExecute
                  | System.IO.UnixFileMode.OtherRead | System.IO.UnixFileMode.OtherExecute);
            }

            results.Add(new FileWriteResult(spec.Path, FileWriteOutcome.Written));
        }
        return results;
    }
}
