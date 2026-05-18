namespace Tamp.OpenGrep.V1;

/// <summary>
/// Settings for <see cref="OpenGrep.Scan"/>. Wave 1 default: emit SARIF
/// because DefectDojo consumes SARIF as its primary scan_type.
/// </summary>
public sealed class OpenGrepScanSettings
{
    /// <summary>Target paths to scan (files or directories). At least one is required.</summary>
    public List<string> Targets { get; } = new();

    /// <summary>Rule configurations: file path, directory path, or registry locator (e.g. <c>auto</c>, <c>p/owasp-top-ten</c>).</summary>
    public List<string> Configs { get; } = new();

    /// <summary>Path to write the SARIF output to. When null, results stream to stdout.</summary>
    public string? OutputFile { get; set; }

    /// <summary>Emit SARIF format. On by default — the Wave 1 chain depends on it.</summary>
    public bool Sarif { get; set; } = true;

    /// <summary>
    /// Baseline mode: only report findings that are NEW compared to the
    /// commit at this git ref. Maps to <c>--baseline-commit</c>. Pass a
    /// git SHA, branch, or tag — opengrep diffs against it.
    /// </summary>
    public string? BaselineCommit { get; set; }

    /// <summary>Optional severity floor (suppress findings below this level).</summary>
    public OpenGrepSeverity? SeverityThreshold { get; set; }

    /// <summary>Exclude glob patterns (relative to repo root).</summary>
    public List<string> Excludes { get; } = new();

    /// <summary>Max bytes per target file. Default is the tool's default (1 MB); raise for large generated files.</summary>
    public long? MaxTargetBytes { get; set; }

    /// <summary>Suppress non-finding output (banners, progress).</summary>
    public bool Quiet { get; set; }

    /// <summary>
    /// Disable opengrep's outbound version-check call. On by default — the
    /// Wave 1 stance is air-gap friendliness; federal targets often
    /// disallow outbound network calls from build runners.
    /// </summary>
    public bool DisableVersionCheck { get; set; } = true;

    public string? WorkingDirectory { get; set; }
    public Dictionary<string, string> EnvironmentVariables { get; } = new();

    // -- Fluent setters --
    public OpenGrepScanSettings AddTarget(string path) { Targets.Add(path); return this; }
    public OpenGrepScanSettings AddTargets(IEnumerable<string> paths) { Targets.AddRange(paths); return this; }
    public OpenGrepScanSettings AddConfig(string config) { Configs.Add(config); return this; }
    public OpenGrepScanSettings AddConfigs(IEnumerable<string> configs) { Configs.AddRange(configs); return this; }
    public OpenGrepScanSettings SetOutputFile(string? path) { OutputFile = path; return this; }
    public OpenGrepScanSettings SetSarif(bool v) { Sarif = v; return this; }
    public OpenGrepScanSettings SetBaselineCommit(string? gitRef) { BaselineCommit = gitRef; return this; }
    public OpenGrepScanSettings SetSeverityThreshold(OpenGrepSeverity? severity) { SeverityThreshold = severity; return this; }
    public OpenGrepScanSettings AddExclude(string pattern) { Excludes.Add(pattern); return this; }
    public OpenGrepScanSettings SetMaxTargetBytes(long? bytes) { MaxTargetBytes = bytes; return this; }
    public OpenGrepScanSettings SetQuiet(bool v) { Quiet = v; return this; }
    public OpenGrepScanSettings SetDisableVersionCheck(bool v) { DisableVersionCheck = v; return this; }
    public OpenGrepScanSettings SetWorkingDirectory(string? cwd) { WorkingDirectory = cwd; return this; }

    public CommandPlan ToCommandPlan()
    {
        if (Targets.Count == 0)
            throw new InvalidOperationException("OpenGrepScanSettings.Targets is empty (call AddTarget at least once).");

        var args = new List<string> { "scan" };

        foreach (var config in Configs)
        {
            args.Add("--config");
            args.Add(config);
        }

        if (Sarif) args.Add("--sarif");

        if (!string.IsNullOrEmpty(OutputFile))
        {
            args.Add("--output");
            args.Add(OutputFile!);
        }

        if (!string.IsNullOrEmpty(BaselineCommit))
        {
            args.Add($"--baseline-commit={BaselineCommit}");
        }

        if (SeverityThreshold is { } sev)
        {
            args.Add($"--severity={SeverityToWire(sev)}");
        }

        foreach (var exclude in Excludes)
        {
            args.Add($"--exclude={exclude}");
        }

        if (MaxTargetBytes is { } bytes)
        {
            args.Add($"--max-target-bytes={bytes.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
        }

        if (DisableVersionCheck) args.Add("--disable-version-check");

        if (Quiet) args.Add("--quiet");

        args.AddRange(Targets);

        var env = new Dictionary<string, string>(EnvironmentVariables);

        return new CommandPlan
        {
            Executable = "opengrep",
            Arguments = args,
            Environment = env,
            WorkingDirectory = WorkingDirectory,
        };
    }

    private static string SeverityToWire(OpenGrepSeverity s) => s switch
    {
        OpenGrepSeverity.Info => "INFO",
        OpenGrepSeverity.Warning => "WARNING",
        OpenGrepSeverity.Error => "ERROR",
        _ => throw new ArgumentOutOfRangeException(nameof(s), s, "Unknown OpenGrep severity."),
    };
}
