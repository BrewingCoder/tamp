namespace Tamp.Trivy;

/// <summary>
/// Settings for <see cref="Trivy.ScanFilesystem"/>. Source-tree scan —
/// secrets, IaC misconfig, and lockfile vulnerabilities depending on
/// which <see cref="TrivyScanSettingsBase.Scanners"/> are enabled.
/// </summary>
public sealed class TrivyFilesystemSettings : TrivyScanSettingsBase
{
    /// <summary>Path to scan. Positional. Required.</summary>
    public string? Path { get; set; }

    /// <summary>Directories or glob patterns to skip. Maps to repeated <c>--skip-dirs</c>.</summary>
    public List<string> SkipDirs { get; } = new();

    /// <summary>Files or glob patterns to skip. Maps to repeated <c>--skip-files</c>.</summary>
    public List<string> SkipFiles { get; } = new();

    // Fluent setters
    public TrivyFilesystemSettings SetPath(string? p) { Path = p; return this; }
    public TrivyFilesystemSettings AddSkipDir(string p) { SkipDirs.Add(p); return this; }
    public TrivyFilesystemSettings AddSkipFile(string p) { SkipFiles.Add(p); return this; }
    public TrivyFilesystemSettings SetOutputFile(string? p) { OutputFile = p; return this; }
    public TrivyFilesystemSettings SetFormat(TrivyFormat f) { Format = f; return this; }
    public TrivyFilesystemSettings AddSeverity(TrivySeverity s) { SeverityFilter.Add(s); return this; }
    public TrivyFilesystemSettings AddScanner(TrivyScanner s) { Scanners.Add(s); return this; }
    public TrivyFilesystemSettings SetIgnoreUnfixed(bool v) { IgnoreUnfixed = v; return this; }
    public TrivyFilesystemSettings SetSkipDbUpdate(bool v) { SkipDbUpdate = v; return this; }
    public TrivyFilesystemSettings SetSkipJavaDbUpdate(bool v) { SkipJavaDbUpdate = v; return this; }
    public TrivyFilesystemSettings SetQuiet(bool v) { Quiet = v; return this; }
    public TrivyFilesystemSettings SetNoProgress(bool v) { NoProgress = v; return this; }
    public TrivyFilesystemSettings SetExitCode(int? c) { ExitCode = c; return this; }
    public TrivyFilesystemSettings SetIgnoreFile(string? p) { IgnoreFile = p; return this; }
    public TrivyFilesystemSettings SetWorkingDirectory(string? cwd) { WorkingDirectory = cwd; return this; }

    protected override IEnumerable<string> BuildSubcommandArguments()
    {
        if (string.IsNullOrEmpty(Path))
            throw new InvalidOperationException("TrivyFilesystemSettings.Path is required.");

        yield return "fs";

        foreach (var dir in SkipDirs)
        {
            yield return "--skip-dirs";
            yield return dir;
        }

        foreach (var file in SkipFiles)
        {
            yield return "--skip-files";
            yield return file;
        }

        yield return Path!;
    }
}
