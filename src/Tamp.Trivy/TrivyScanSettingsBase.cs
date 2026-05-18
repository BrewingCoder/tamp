namespace Tamp.Trivy;

/// <summary>
/// Common settings for every Trivy scan subcommand. Subclasses add the
/// subcommand-specific knobs and the positional target.
/// </summary>
public abstract class TrivyScanSettingsBase
{
    /// <summary>Where Trivy writes the report. Maps to <c>--output</c> (-o). When null, results stream to stdout.</summary>
    public string? OutputFile { get; set; }

    /// <summary>Report format. Defaults to <see cref="TrivyFormat.Sarif"/> for the Wave 1 chain.</summary>
    public TrivyFormat Format { get; set; } = TrivyFormat.Sarif;

    /// <summary>Severities to include in the report. Comma-joined on the wire. Empty = Trivy default (all severities).</summary>
    public List<TrivySeverity> SeverityFilter { get; } = new();

    /// <summary>Which scanner families to enable. Comma-joined on the wire. Empty = the subcommand's own default (image/fs: vuln+secret; config: misconfig).</summary>
    public List<TrivyScanner> Scanners { get; } = new();

    /// <summary>Only show vulns that have a fix available. Maps to <c>--ignore-unfixed</c>.</summary>
    public bool IgnoreUnfixed { get; set; }

    /// <summary>Skip the vulnerability DB update at scan time. Maps to <c>--skip-db-update</c>. Pair with <see cref="SkipJavaDbUpdate"/> for full air-gap.</summary>
    public bool SkipDbUpdate { get; set; }

    /// <summary>Skip the Java index DB update. Maps to <c>--skip-java-db-update</c>.</summary>
    public bool SkipJavaDbUpdate { get; set; }

    /// <summary>Suppress progress bar and log output. Maps to <c>-q / --quiet</c>.</summary>
    public bool Quiet { get; set; }

    /// <summary>Suppress only the progress bar (keep regular log). Maps to <c>--no-progress</c>.</summary>
    public bool NoProgress { get; set; }

    /// <summary>Exit code when any issue is found. Maps to <c>--exit-code</c>. Null = Trivy default (0; success regardless of findings).</summary>
    public int? ExitCode { get; set; }

    /// <summary>Path to a .trivyignore file. Maps to <c>--ignorefile</c>.</summary>
    public string? IgnoreFile { get; set; }

    public string? WorkingDirectory { get; set; }
    public Dictionary<string, string> EnvironmentVariables { get; } = new();

    /// <summary>Subclasses return the subcommand + any subcommand-specific args, including positional target.</summary>
    protected abstract IEnumerable<string> BuildSubcommandArguments();

    public CommandPlan ToCommandPlan()
    {
        var args = new List<string>();

        // Subcommand block (e.g. "fs", "image", "config") + any subcommand-specific flags.
        // Subclasses are responsible for putting the subcommand first and the positional
        // target last; common flags slot in between via this base.
        var subcommandArgs = BuildSubcommandArguments().ToList();
        if (subcommandArgs.Count == 0)
            throw new InvalidOperationException("Trivy subcommand args must include at least a subcommand verb.");

        // First arg is the subcommand verb; everything after it is the subcommand-specific block.
        args.Add(subcommandArgs[0]);

        // Common report flags
        if (Format != TrivyFormat.Table)
        {
            args.Add("--format");
            args.Add(FormatToWire(Format));
        }
        if (!string.IsNullOrEmpty(OutputFile))
        {
            args.Add("--output");
            args.Add(OutputFile!);
        }
        if (SeverityFilter.Count > 0)
        {
            args.Add("--severity");
            args.Add(string.Join(",", SeverityFilter.Select(SeverityToWire)));
        }
        if (Scanners.Count > 0)
        {
            args.Add("--scanners");
            args.Add(string.Join(",", Scanners.Select(ScannerToWire)));
        }
        if (IgnoreUnfixed) args.Add("--ignore-unfixed");
        if (SkipDbUpdate) args.Add("--skip-db-update");
        if (SkipJavaDbUpdate) args.Add("--skip-java-db-update");
        if (Quiet) args.Add("--quiet");
        if (NoProgress) args.Add("--no-progress");
        if (ExitCode is { } code)
        {
            args.Add("--exit-code");
            args.Add(code.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
        if (!string.IsNullOrEmpty(IgnoreFile))
        {
            args.Add("--ignorefile");
            args.Add(IgnoreFile!);
        }

        // Subcommand-specific flags + positional target come after the common report flags.
        for (var i = 1; i < subcommandArgs.Count; i++)
            args.Add(subcommandArgs[i]);

        return new CommandPlan
        {
            Executable = "trivy",
            Arguments = args,
            Environment = new Dictionary<string, string>(EnvironmentVariables),
            WorkingDirectory = WorkingDirectory,
        };
    }

    internal static string FormatToWire(TrivyFormat f) => f switch
    {
        TrivyFormat.Table => "table",
        TrivyFormat.Json => "json",
        TrivyFormat.Template => "template",
        TrivyFormat.Sarif => "sarif",
        TrivyFormat.CycloneDx => "cyclonedx",
        TrivyFormat.Spdx => "spdx",
        TrivyFormat.SpdxJson => "spdx-json",
        TrivyFormat.Github => "github",
        TrivyFormat.CosignVuln => "cosign-vuln",
        _ => throw new ArgumentOutOfRangeException(nameof(f), f, "Unknown Trivy format."),
    };

    internal static string SeverityToWire(TrivySeverity s) => s switch
    {
        TrivySeverity.Unknown => "UNKNOWN",
        TrivySeverity.Low => "LOW",
        TrivySeverity.Medium => "MEDIUM",
        TrivySeverity.High => "HIGH",
        TrivySeverity.Critical => "CRITICAL",
        _ => throw new ArgumentOutOfRangeException(nameof(s), s, "Unknown Trivy severity."),
    };

    internal static string ScannerToWire(TrivyScanner s) => s switch
    {
        TrivyScanner.Vuln => "vuln",
        TrivyScanner.Misconfig => "misconfig",
        TrivyScanner.Secret => "secret",
        TrivyScanner.License => "license",
        _ => throw new ArgumentOutOfRangeException(nameof(s), s, "Unknown Trivy scanner."),
    };
}
