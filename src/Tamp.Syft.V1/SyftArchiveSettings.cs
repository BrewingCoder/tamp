namespace Tamp.Syft.V1;

/// <summary>
/// Settings for <see cref="Syft.ScanArchive"/>. Single-file scan —
/// most useful for jar/war/zip/tar archives where syft can extract +
/// catalog the contents without a full directory walk.
/// </summary>
public sealed class SyftArchiveSettings : SyftScanSettingsBase
{
    /// <summary>Archive path. Positional, prefixed with <c>file:</c>. Required.</summary>
    public string? Path { get; set; }

    // Fluent setters
    public SyftArchiveSettings SetPath(string? p) { Path = p; return this; }
    public SyftArchiveSettings AddOutput(SyftFormat f, string? path = null) { Outputs.Add(new SyftOutput(f, path)); return this; }
    public SyftArchiveSettings SetOutputFile(string? p) { OutputFile = p; return this; }
    public SyftArchiveSettings SetFormat(SyftFormat f) { Format = f; return this; }
    public SyftArchiveSettings SetSourceName(string? n) { SourceName = n; return this; }
    public SyftArchiveSettings SetSourceVersion(string? v) { SourceVersion = v; return this; }
    public SyftArchiveSettings SetQuiet(bool v) { Quiet = v; return this; }
    public SyftArchiveSettings SetWorkingDirectory(string? cwd) { WorkingDirectory = cwd; return this; }

    protected override string SourceArgument()
    {
        if (string.IsNullOrEmpty(Path))
            throw new InvalidOperationException("SyftArchiveSettings.Path is required.");
        return $"file:{Path}";
    }
}
