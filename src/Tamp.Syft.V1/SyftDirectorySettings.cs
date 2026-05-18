namespace Tamp.Syft.V1;

/// <summary>
/// Settings for <see cref="Syft.ScanDirectory"/>. Walks a directory
/// tree and runs every catalog-relevant cataloger (npm lockfiles, pip,
/// Cargo.lock, go.mod / go.sum, Maven POMs, Gemfile.lock, NuGet
/// project.assets.json, etc.). The Wave 1 default emits CycloneDX JSON
/// so the output slots straight into Dependency-Track.
/// </summary>
public sealed class SyftDirectorySettings : SyftScanSettingsBase
{
    /// <summary>Path to scan. Positional source argument; required.</summary>
    public string? Path { get; set; }

    /// <summary>Anchor for the walk. Maps to <c>--base-path</c>. No symlinks followed above this directory; reported paths are relative to it.</summary>
    public string? BasePath { get; set; }

    /// <summary>Force the scan source via <c>--from dir</c>. Defaults true — directories are easy to confuse with file-like archives, so the explicit form is safer.</summary>
    public bool ForceDirScheme { get; set; } = true;

    // Fluent setters
    public SyftDirectorySettings SetPath(string? p) { Path = p; return this; }
    public SyftDirectorySettings SetBasePath(string? p) { BasePath = p; return this; }
    public SyftDirectorySettings SetForceDirScheme(bool v) { ForceDirScheme = v; return this; }
    public SyftDirectorySettings AddOutput(SyftFormat f, string? path = null) { Outputs.Add(new SyftOutput(f, path)); return this; }
    public SyftDirectorySettings SetOutputFile(string? p) { OutputFile = p; return this; }
    public SyftDirectorySettings SetFormat(SyftFormat f) { Format = f; return this; }
    public SyftDirectorySettings AddExcludePattern(string p) { ExcludePatterns.Add(p); return this; }
    public SyftDirectorySettings AddSelectCatalogers(string spec) { SelectCatalogers.Add(spec); return this; }
    public SyftDirectorySettings AddEnrich(string source) { Enrich.Add(source); return this; }
    public SyftDirectorySettings AddConfigFile(string p) { ConfigFiles.Add(p); return this; }
    public SyftDirectorySettings SetSourceName(string? n) { SourceName = n; return this; }
    public SyftDirectorySettings SetSourceVersion(string? v) { SourceVersion = v; return this; }
    public SyftDirectorySettings SetSourceSupplier(string? s) { SourceSupplier = s; return this; }
    public SyftDirectorySettings SetParallelism(int? n) { Parallelism = n; return this; }
    public SyftDirectorySettings SetQuiet(bool v) { Quiet = v; return this; }
    public SyftDirectorySettings SetWorkingDirectory(string? cwd) { WorkingDirectory = cwd; return this; }

    protected override string SourceArgument()
    {
        if (string.IsNullOrEmpty(Path))
            throw new InvalidOperationException("SyftDirectorySettings.Path is required.");
        return ForceDirScheme ? $"dir:{Path}" : Path!;
    }

    protected override IEnumerable<string> ExtraArguments()
    {
        if (!string.IsNullOrEmpty(BasePath))
        {
            yield return "--base-path";
            yield return BasePath!;
        }
    }
}
