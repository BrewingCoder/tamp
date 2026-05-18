namespace Tamp.CycloneDx.V5;

/// <summary>
/// Settings for <see cref="CycloneDx.Generate"/>. Mirrors the
/// <c>dotnet CycloneDX</c> CLI 5.x option surface; defaults follow the
/// Wave 1 chain's needs (JSON output, transitive resolution on).
/// </summary>
public sealed class CycloneDxGenerateSettings
{
    /// <summary>Path to the project file, solution file, or directory the tool should scan. Positional argument; required.</summary>
    public string? Path { get; set; }

    /// <summary>Directory the BOM is written into.</summary>
    public string? OutputDirectory { get; set; }

    /// <summary>Filename override (without extension). When null, the tool uses its default (<c>bom</c>).</summary>
    public string? Filename { get; set; }

    /// <summary>Output format. Defaults to <see cref="CycloneDxFormat.Json"/> because Dependency-Track consumes JSON BOMs.</summary>
    public CycloneDxFormat Format { get; set; } = CycloneDxFormat.Json;

    /// <summary>Exclude DevDependencies / packages flagged as dev-only.</summary>
    public bool ExcludeDevelopment { get; set; }

    /// <summary>Exclude test projects from the resolved graph.</summary>
    public bool ExcludeTestProjects { get; set; }

    /// <summary>Resolve transitive package references rather than only direct deps. On by default — federal SBOM expectations include the full graph.</summary>
    public bool IncludeTransitive { get; set; } = true;

    /// <summary>Disable the network lookup that enriches license metadata. Useful for air-gapped builds.</summary>
    public bool DisableHashComputation { get; set; }

    /// <summary>Optional GitHub Personal Access Token for license-metadata lookups. Typed as <see cref="Secret"/> so the runner's redaction table covers it.</summary>
    public Secret? GitHubLicenseToken { get; set; }

    /// <summary>Set the BOM's <c>serialNumber</c> URN explicitly. When null, the tool generates a fresh urn:uuid:.</summary>
    public string? SerialNumber { get; set; }

    public string? WorkingDirectory { get; set; }

    public Dictionary<string, string> EnvironmentVariables { get; } = new();

    // -- Fluent setters --
    public CycloneDxGenerateSettings SetPath(string? p) { Path = p; return this; }
    public CycloneDxGenerateSettings SetOutputDirectory(string? p) { OutputDirectory = p; return this; }
    public CycloneDxGenerateSettings SetFilename(string? n) { Filename = n; return this; }
    public CycloneDxGenerateSettings SetFormat(CycloneDxFormat f) { Format = f; return this; }
    public CycloneDxGenerateSettings SetExcludeDevelopment(bool v) { ExcludeDevelopment = v; return this; }
    public CycloneDxGenerateSettings SetExcludeTestProjects(bool v) { ExcludeTestProjects = v; return this; }
    public CycloneDxGenerateSettings SetIncludeTransitive(bool v) { IncludeTransitive = v; return this; }
    public CycloneDxGenerateSettings SetDisableHashComputation(bool v) { DisableHashComputation = v; return this; }
    public CycloneDxGenerateSettings SetGitHubLicenseToken(Secret? token) { GitHubLicenseToken = token; return this; }
    public CycloneDxGenerateSettings SetSerialNumber(string? urn) { SerialNumber = urn; return this; }
    public CycloneDxGenerateSettings SetWorkingDirectory(string? cwd) { WorkingDirectory = cwd; return this; }

    /// <summary>Reveals the optional GitHub License token. Lives on the Settings record per TAMP004.</summary>
    internal string? RevealGitHubLicenseToken() => GitHubLicenseToken?.Reveal();

    /// <summary>Builds the <c>dotnet CycloneDX …</c> argument list.</summary>
    public CommandPlan ToCommandPlan()
    {
        if (string.IsNullOrEmpty(Path))
            throw new InvalidOperationException("CycloneDxGenerateSettings.Path is required (point at the project, solution, or directory to scan).");

        var args = new List<string> { "CycloneDX", Path };

        if (!string.IsNullOrEmpty(OutputDirectory))
        {
            args.Add("--out");
            args.Add(OutputDirectory!);
        }

        if (!string.IsNullOrEmpty(Filename))
        {
            args.Add("--filename");
            args.Add(Filename!);
        }

        switch (Format)
        {
            case CycloneDxFormat.Json: args.Add("--json"); break;
            case CycloneDxFormat.Both: args.Add("--json"); args.Add("--include-xml"); break;
            case CycloneDxFormat.Xml: /* tool default is XML — no flag needed */ break;
        }

        if (ExcludeDevelopment) args.Add("--exclude-dev");
        if (ExcludeTestProjects) args.Add("--exclude-test-projects");
        if (!IncludeTransitive) args.Add("--exclude-transitive");
        if (DisableHashComputation) args.Add("--disable-hash-computation");

        if (!string.IsNullOrEmpty(SerialNumber))
        {
            args.Add("--set-serial-number");
            args.Add(SerialNumber!);
        }

        if (RevealGitHubLicenseToken() is { Length: > 0 } token)
        {
            args.Add("--github-token");
            args.Add(token);
        }

        var env = new Dictionary<string, string>(EnvironmentVariables)
        {
            ["DOTNET_NOLOGO"] = "1",
            ["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1",
        };

        var secrets = GitHubLicenseToken is null ? Array.Empty<Secret>() : new[] { GitHubLicenseToken };

        return new CommandPlan
        {
            Executable = "dotnet",
            Arguments = args,
            Environment = env,
            WorkingDirectory = WorkingDirectory,
            Secrets = secrets,
        };
    }
}
