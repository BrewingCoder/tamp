using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Tamp.Scaffold;

/// <summary>
/// Immutable snapshot of what the CLI knows about the target repository.
/// Probes assemble this before any template runs; templates read from it.
/// </summary>
public sealed record ScaffoldContext
{
    public required AbsolutePath RepoRoot { get; init; }

    /// <summary>Resolved .NET solution file path, or null if zero / multiple solutions are present.</summary>
    public AbsolutePath? Solution { get; init; }

    /// <summary>
    /// Tamp.* package version templates should pin in generated project files.
    /// The CLI fills this from its own assembly version at runtime; tests
    /// override it directly.
    /// </summary>
    public required string TampCoreVersion { get; init; }

    public bool DotnetToolsJsonExists { get; init; }
    public bool BuildCsAlreadyPresent { get; init; }

    /// <summary>
    /// Extension slot. Future probes (YarnWorkspaceProbe, DockerfileProbe, etc.)
    /// contribute named facts here instead of growing this record. Read-only.
    /// </summary>
    public IReadOnlyDictionary<string, string> ProbeData { get; init; }
        = ReadOnlyDictionary<string, string>.Empty;
}

/// <summary>
/// Mutable builder probes accumulate into; the CLI command freezes the
/// result into a <see cref="ScaffoldContext"/> before handing it to templates.
/// </summary>
public sealed class ScaffoldContextBuilder
{
    public required AbsolutePath RepoRoot { get; init; }
    public AbsolutePath? Solution { get; set; }
    public required string TampCoreVersion { get; init; }
    public bool DotnetToolsJsonExists { get; set; }
    public bool BuildCsAlreadyPresent { get; set; }

    private readonly Dictionary<string, string> _probeData = new(System.StringComparer.Ordinal);

    public void Set(string key, string value) => _probeData[key] = value;

    public ScaffoldContext Build() => new()
    {
        RepoRoot = RepoRoot,
        Solution = Solution,
        TampCoreVersion = TampCoreVersion,
        DotnetToolsJsonExists = DotnetToolsJsonExists,
        BuildCsAlreadyPresent = BuildCsAlreadyPresent,
        ProbeData = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(_probeData, System.StringComparer.Ordinal)),
    };
}
