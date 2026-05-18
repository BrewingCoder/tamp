namespace Tamp.Trivy;

/// <summary>
/// Settings for <see cref="Trivy.ScanConfig"/>. IaC misconfig scan
/// (Terraform / Kubernetes / Dockerfile / CloudFormation / Helm / Ansible).
/// </summary>
public sealed class TrivyConfigSettings : TrivyScanSettingsBase
{
    /// <summary>Path to scan (directory or single file). Positional. Required.</summary>
    public string? Target { get; set; }

    /// <summary>Paths to Rego check files or directories. Maps to repeated <c>--config-check</c>.</summary>
    public List<string> ConfigCheckPaths { get; } = new();

    /// <summary>Directories or glob patterns to skip during the walk. Maps to repeated <c>--skip-dirs</c>.</summary>
    public List<string> SkipDirs { get; } = new();

    /// <summary>Files or glob patterns to skip during the walk. Maps to repeated <c>--skip-files</c>.</summary>
    public List<string> SkipFiles { get; } = new();

    /// <summary>Include passing checks in the report. Maps to <c>--include-non-failures</c>.</summary>
    public bool IncludeNonFailures { get; set; }

    // Fluent setters
    public TrivyConfigSettings SetTarget(string? p) { Target = p; return this; }
    public TrivyConfigSettings AddConfigCheckPath(string p) { ConfigCheckPaths.Add(p); return this; }
    public TrivyConfigSettings AddSkipDir(string p) { SkipDirs.Add(p); return this; }
    public TrivyConfigSettings AddSkipFile(string p) { SkipFiles.Add(p); return this; }
    public TrivyConfigSettings SetIncludeNonFailures(bool v) { IncludeNonFailures = v; return this; }
    public TrivyConfigSettings SetOutputFile(string? p) { OutputFile = p; return this; }
    public TrivyConfigSettings SetFormat(TrivyFormat f) { Format = f; return this; }
    public TrivyConfigSettings AddSeverity(TrivySeverity s) { SeverityFilter.Add(s); return this; }
    public TrivyConfigSettings AddScanner(TrivyScanner s) { Scanners.Add(s); return this; }
    public TrivyConfigSettings SetSkipDbUpdate(bool v) { SkipDbUpdate = v; return this; }
    public TrivyConfigSettings SetQuiet(bool v) { Quiet = v; return this; }
    public TrivyConfigSettings SetNoProgress(bool v) { NoProgress = v; return this; }
    public TrivyConfigSettings SetExitCode(int? c) { ExitCode = c; return this; }
    public TrivyConfigSettings SetIgnoreFile(string? p) { IgnoreFile = p; return this; }
    public TrivyConfigSettings SetWorkingDirectory(string? cwd) { WorkingDirectory = cwd; return this; }

    protected override IEnumerable<string> BuildSubcommandArguments()
    {
        if (string.IsNullOrEmpty(Target))
            throw new InvalidOperationException("TrivyConfigSettings.Target is required.");

        yield return "config";

        foreach (var check in ConfigCheckPaths)
        {
            yield return "--config-check";
            yield return check;
        }

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

        if (IncludeNonFailures) yield return "--include-non-failures";

        yield return Target!;
    }
}
