namespace Tamp.OsvScanner.V2;

/// <summary>
/// Settings for <see cref="OsvScanner.ScanSource"/>. Wave 1/2 default:
/// emit SARIF (the chain's standard finding format).
/// </summary>
public sealed class OsvScannerScanSettings
{
    /// <summary>
    /// Path to a CycloneDX (or SPDX) SBOM to scan. Maps to <c>--sbom</c>.
    /// In osv-scanner 2.x this flag is marked deprecated in --help but
    /// remains functional and is the most explicit way to point at a
    /// specific BOM file; the alternative is auto-detection via scan
    /// directories.
    /// </summary>
    public string? SbomFile { get; set; }

    /// <summary>Lockfile paths to scan explicitly. Maps to repeated <c>--lockfile</c>. Supports per-ecosystem manifests (package-lock.json, Cargo.lock, requirements.txt, go.sum, pom.xml, …).</summary>
    public List<string> LockfilePaths { get; } = new();

    /// <summary>Directories to scan. Positional arguments. Empty means osv-scanner falls back to whatever's explicit (sbom/lockfile flags).</summary>
    public List<string> ScanDirectories { get; } = new();

    /// <summary>Where to write findings. Maps to <c>--output-file</c>. When null, results stream to stdout.</summary>
    public string? OutputFile { get; set; }

    /// <summary>Output format. Defaults to <see cref="OsvScannerFormat.Sarif"/> for the Wave 1 chain.</summary>
    public OsvScannerFormat Format { get; set; } = OsvScannerFormat.Sarif;

    /// <summary>Recurse into subdirectories when a scan directory is given. Maps to <c>--recursive</c>.</summary>
    public bool Recursive { get; set; }

    /// <summary>Don't fail when no lockfile is found in a scan directory. Maps to <c>--allow-no-lockfiles</c>. Useful for SBOM-only invocations.</summary>
    public bool AllowNoLockfiles { get; set; } = true;

    /// <summary>Scan files normally excluded by .gitignore. Maps to <c>--no-ignore</c>.</summary>
    public bool NoIgnore { get; set; }

    /// <summary>Include the git repository root in the scan. Maps to <c>--include-git-root</c>.</summary>
    public bool IncludeGitRoot { get; set; }

    /// <summary>Disable transitive resolution of manifest files. Maps to <c>--no-resolve</c>. Lockfile contents are taken at face value.</summary>
    public bool NoResolve { get; set; }

    /// <summary>Source for package metadata. Defaults to <see cref="OsvScannerDataSource.DepsDev"/>.</summary>
    public OsvScannerDataSource DataSource { get; set; } = OsvScannerDataSource.DepsDev;

    /// <summary>Use cached vulnerability databases — no network. Maps to <c>--offline-vulnerabilities</c>.</summary>
    public bool OfflineVulnerabilities { get; set; }

    /// <summary>Disable ALL network features (broader than vulnerability-DB-only offline). Maps to <c>--offline</c>.</summary>
    public bool Offline { get; set; }

    /// <summary>Download offline DBs before the scan. Maps to <c>--download-offline-databases</c>. Useful for the first run in an air-gapped pipeline.</summary>
    public bool DownloadOfflineDatabases { get; set; }

    /// <summary>Path to an osv-scanner config file (rule ignores etc.). Maps to <c>--config</c>.</summary>
    public string? ConfigFile { get; set; }

    /// <summary>Exclusion patterns (exact, glob with <c>g:</c> prefix, or regex with <c>r:</c> prefix). Maps to repeated <c>--experimental-exclude</c>.</summary>
    public List<string> ExcludePatterns { get; } = new();

    /// <summary>Show all vulnerabilities including ones marked unimportant or uncalled. Maps to <c>--all-vulns</c>.</summary>
    public bool AllVulns { get; set; }

    public string? WorkingDirectory { get; set; }
    public Dictionary<string, string> EnvironmentVariables { get; } = new();

    // -- Fluent setters --
    public OsvScannerScanSettings SetSbomFile(string? path) { SbomFile = path; return this; }
    public OsvScannerScanSettings AddLockfile(string path) { LockfilePaths.Add(path); return this; }
    public OsvScannerScanSettings AddLockfiles(IEnumerable<string> paths) { LockfilePaths.AddRange(paths); return this; }
    public OsvScannerScanSettings AddScanDirectory(string path) { ScanDirectories.Add(path); return this; }
    public OsvScannerScanSettings AddScanDirectories(IEnumerable<string> paths) { ScanDirectories.AddRange(paths); return this; }
    public OsvScannerScanSettings SetOutputFile(string? path) { OutputFile = path; return this; }
    public OsvScannerScanSettings SetFormat(OsvScannerFormat f) { Format = f; return this; }
    public OsvScannerScanSettings SetRecursive(bool v) { Recursive = v; return this; }
    public OsvScannerScanSettings SetAllowNoLockfiles(bool v) { AllowNoLockfiles = v; return this; }
    public OsvScannerScanSettings SetNoIgnore(bool v) { NoIgnore = v; return this; }
    public OsvScannerScanSettings SetIncludeGitRoot(bool v) { IncludeGitRoot = v; return this; }
    public OsvScannerScanSettings SetNoResolve(bool v) { NoResolve = v; return this; }
    public OsvScannerScanSettings SetDataSource(OsvScannerDataSource v) { DataSource = v; return this; }
    public OsvScannerScanSettings SetOfflineVulnerabilities(bool v) { OfflineVulnerabilities = v; return this; }
    public OsvScannerScanSettings SetOffline(bool v) { Offline = v; return this; }
    public OsvScannerScanSettings SetDownloadOfflineDatabases(bool v) { DownloadOfflineDatabases = v; return this; }
    public OsvScannerScanSettings SetConfigFile(string? path) { ConfigFile = path; return this; }
    public OsvScannerScanSettings AddExcludePattern(string pattern) { ExcludePatterns.Add(pattern); return this; }
    public OsvScannerScanSettings SetAllVulns(bool v) { AllVulns = v; return this; }
    public OsvScannerScanSettings SetWorkingDirectory(string? cwd) { WorkingDirectory = cwd; return this; }

    public CommandPlan ToCommandPlan()
    {
        if (string.IsNullOrEmpty(SbomFile) && LockfilePaths.Count == 0 && ScanDirectories.Count == 0)
            throw new InvalidOperationException("OsvScannerScanSettings requires at least one input: set SbomFile, add a Lockfile, or add a ScanDirectory.");

        var args = new List<string> { "scan", "source" };

        if (!string.IsNullOrEmpty(SbomFile))
        {
            args.Add("--sbom");
            args.Add(SbomFile!);
        }

        foreach (var lockfile in LockfilePaths)
        {
            args.Add("--lockfile");
            args.Add(lockfile);
        }

        if (!string.IsNullOrEmpty(OutputFile))
        {
            args.Add("--output-file");
            args.Add(OutputFile!);
        }

        // Format defaults to Table in the tool; emit explicitly for any non-Table choice.
        if (Format != OsvScannerFormat.Table)
        {
            args.Add("--format");
            args.Add(FormatToWire(Format));
        }

        if (DataSource != OsvScannerDataSource.DepsDev)
        {
            args.Add("--data-source");
            args.Add(DataSourceToWire(DataSource));
        }

        if (Recursive) args.Add("--recursive");
        if (AllowNoLockfiles) args.Add("--allow-no-lockfiles");
        if (NoIgnore) args.Add("--no-ignore");
        if (IncludeGitRoot) args.Add("--include-git-root");
        if (NoResolve) args.Add("--no-resolve");
        if (Offline) args.Add("--offline");
        if (OfflineVulnerabilities) args.Add("--offline-vulnerabilities");
        if (DownloadOfflineDatabases) args.Add("--download-offline-databases");
        if (AllVulns) args.Add("--all-vulns");

        if (!string.IsNullOrEmpty(ConfigFile))
        {
            args.Add("--config");
            args.Add(ConfigFile!);
        }

        foreach (var pattern in ExcludePatterns)
        {
            args.Add("--experimental-exclude");
            args.Add(pattern);
        }

        // Positional scan directories come last.
        args.AddRange(ScanDirectories);

        return new CommandPlan
        {
            Executable = "osv-scanner",
            Arguments = args,
            Environment = new Dictionary<string, string>(EnvironmentVariables),
            WorkingDirectory = WorkingDirectory,
        };
    }

    private static string FormatToWire(OsvScannerFormat f) => f switch
    {
        OsvScannerFormat.Sarif => "sarif",
        OsvScannerFormat.Json => "json",
        OsvScannerFormat.Table => "table",
        OsvScannerFormat.Markdown => "markdown",
        OsvScannerFormat.Html => "html",
        OsvScannerFormat.CycloneDx14 => "cyclonedx-1-4",
        OsvScannerFormat.CycloneDx15 => "cyclonedx-1-5",
        OsvScannerFormat.Spdx23 => "spdx-2-3",
        OsvScannerFormat.GhAnnotations => "gh-annotations",
        _ => throw new ArgumentOutOfRangeException(nameof(f), f, "Unknown osv-scanner format."),
    };

    private static string DataSourceToWire(OsvScannerDataSource d) => d switch
    {
        OsvScannerDataSource.DepsDev => "deps.dev",
        OsvScannerDataSource.Native => "native",
        _ => throw new ArgumentOutOfRangeException(nameof(d), d, "Unknown osv-scanner data source."),
    };
}
