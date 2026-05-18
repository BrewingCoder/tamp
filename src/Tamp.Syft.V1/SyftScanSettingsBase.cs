namespace Tamp.Syft.V1;

/// <summary>
/// Common settings for every syft scan subcommand. Subclasses add the
/// source-specific knobs and supply the positional source argument.
/// </summary>
public abstract class SyftScanSettingsBase
{
    /// <summary>
    /// Where syft writes the report(s). Maps to repeated <c>-o</c>. Multiple
    /// entries produce one report each (e.g. cyclonedx-json to one file
    /// plus syft-json to another for forensic depth). When empty, the
    /// shortcut <see cref="OutputFile"/> + <see cref="Format"/> applies; if
    /// both are unset, syft uses its default (syft-table to stdout).
    /// </summary>
    public List<SyftOutput> Outputs { get; } = new();

    /// <summary>Single-output shortcut. Used only when <see cref="Outputs"/> is empty. Set together with <see cref="Format"/>.</summary>
    public string? OutputFile { get; set; }

    /// <summary>Format for the single-output shortcut. Defaults to <see cref="SyftFormat.CycloneDxJson"/> (Wave 1 chain consumer).</summary>
    public SyftFormat Format { get; set; } = SyftFormat.CycloneDxJson;

    /// <summary>Exclusion patterns (glob). Maps to repeated <c>--exclude</c>.</summary>
    public List<string> ExcludePatterns { get; } = new();

    /// <summary>Cataloger selection. Maps to repeated <c>--select-catalogers</c>. Strings can be additive (<c>+name</c>), subtractive (<c>-name</c>), or replacements.</summary>
    public List<string> SelectCatalogers { get; } = new();

    /// <summary>Override the default cataloger set entirely. Maps to repeated <c>--override-default-catalogers</c>.</summary>
    public List<string> OverrideDefaultCatalogers { get; } = new();

    /// <summary>Opt-in enrichment from online/local sources. Maps to repeated <c>--enrich</c>. Values: <c>all</c>, <c>golang</c>, <c>java</c>, <c>javascript</c>, <c>python</c>.</summary>
    public List<string> Enrich { get; } = new();

    /// <summary>syft config files. Maps to repeated <c>-c / --config</c>.</summary>
    public List<string> ConfigFiles { get; } = new();

    /// <summary>Override the metadata.component.name in the SBOM. Maps to <c>--source-name</c>.</summary>
    public string? SourceName { get; set; }

    /// <summary>Override the metadata.component.version. Maps to <c>--source-version</c>.</summary>
    public string? SourceVersion { get; set; }

    /// <summary>Set the supplier metadata. Maps to <c>--source-supplier</c>.</summary>
    public string? SourceSupplier { get; set; }

    /// <summary>Number of cataloger workers. Maps to <c>--parallelism</c>. Null = syft default.</summary>
    public int? Parallelism { get; set; }

    /// <summary>Suppress all logging. Maps to <c>-q / --quiet</c>.</summary>
    public bool Quiet { get; set; }

    public string? WorkingDirectory { get; set; }
    public Dictionary<string, string> EnvironmentVariables { get; } = new();

    /// <summary>Subclasses return the positional source argument (e.g. <c>"dir:./src"</c>, <c>"registry:nginx:latest"</c>).</summary>
    protected abstract string SourceArgument();

    /// <summary>Hook for source-specific flags inserted between common flags and the positional source argument.</summary>
    protected virtual IEnumerable<string> ExtraArguments() => Array.Empty<string>();

    public CommandPlan ToCommandPlan()
    {
        var source = SourceArgument();
        if (string.IsNullOrEmpty(source))
            throw new InvalidOperationException($"{GetType().Name} must supply a source (e.g. directory path, image ref, archive path).");

        var args = new List<string> { "scan" };

        // Output: prefer Outputs[] when populated; else fall back to OutputFile+Format shortcut;
        // else don't emit -o and let syft default to syft-table on stdout.
        if (Outputs.Count > 0)
        {
            foreach (var o in Outputs)
            {
                args.Add("-o");
                args.Add(o.Path is null ? FormatToWire(o.Format) : $"{FormatToWire(o.Format)}={o.Path}");
            }
        }
        else if (!string.IsNullOrEmpty(OutputFile))
        {
            args.Add("-o");
            args.Add($"{FormatToWire(Format)}={OutputFile}");
        }

        foreach (var pattern in ExcludePatterns)
        {
            args.Add("--exclude");
            args.Add(pattern);
        }

        foreach (var c in SelectCatalogers)
        {
            args.Add("--select-catalogers");
            args.Add(c);
        }

        foreach (var c in OverrideDefaultCatalogers)
        {
            args.Add("--override-default-catalogers");
            args.Add(c);
        }

        foreach (var e in Enrich)
        {
            args.Add("--enrich");
            args.Add(e);
        }

        foreach (var c in ConfigFiles)
        {
            args.Add("-c");
            args.Add(c);
        }

        if (!string.IsNullOrEmpty(SourceName))
        {
            args.Add("--source-name");
            args.Add(SourceName!);
        }
        if (!string.IsNullOrEmpty(SourceVersion))
        {
            args.Add("--source-version");
            args.Add(SourceVersion!);
        }
        if (!string.IsNullOrEmpty(SourceSupplier))
        {
            args.Add("--source-supplier");
            args.Add(SourceSupplier!);
        }
        if (Parallelism is { } p)
        {
            args.Add("--parallelism");
            args.Add(p.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
        if (Quiet) args.Add("--quiet");

        // Subcommand-specific flags
        foreach (var a in ExtraArguments()) args.Add(a);

        // Positional source last
        args.Add(source);

        return new CommandPlan
        {
            Executable = "syft",
            Arguments = args,
            Environment = new Dictionary<string, string>(EnvironmentVariables),
            WorkingDirectory = WorkingDirectory,
        };
    }

    internal static string FormatToWire(SyftFormat f) => f switch
    {
        SyftFormat.CycloneDxJson => "cyclonedx-json",
        SyftFormat.CycloneDxXml => "cyclonedx-xml",
        SyftFormat.SpdxJson => "spdx-json",
        SyftFormat.SpdxTagValue => "spdx-tag-value",
        SyftFormat.SyftJson => "syft-json",
        SyftFormat.SyftTable => "syft-table",
        SyftFormat.SyftText => "syft-text",
        SyftFormat.GithubJson => "github-json",
        SyftFormat.Purls => "purls",
        SyftFormat.Template => "template",
        _ => throw new ArgumentOutOfRangeException(nameof(f), f, "Unknown syft format."),
    };
}
