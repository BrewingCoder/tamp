using System.Globalization;

namespace Tamp.CycloneDx.V6;

/// <summary>
/// Settings for <see cref="CycloneDx.Generate"/>. Mirrors the
/// <c>dotnet-CycloneDX</c> 6.x CLI surface; defaults follow the Wave 1
/// chain's needs (JSON output).
/// </summary>
public sealed class CycloneDxGenerateSettings
{
    /// <summary>Path to the project / solution / directory to scan. Positional argument; required.</summary>
    public string? Path { get; set; }

    /// <summary>Directory the BOM is written into. Maps to <c>--output</c> (-o).</summary>
    public string? OutputDirectory { get; set; }

    /// <summary>Optional filename override. The tool defaults to <c>bom.xml</c> or <c>bom.json</c> based on format.</summary>
    public string? Filename { get; set; }

    /// <summary>Output format. Defaults to <see cref="CycloneDxFormat.Json"/> for the Wave 1 chain.</summary>
    public CycloneDxFormat Format { get; set; } = CycloneDxFormat.Json;

    /// <summary>Optional CycloneDX spec version to emit. Maps to <c>--spec-version</c>. When null, the tool default applies (1.7 in 6.x).</summary>
    public string? SpecVersion { get; set; }

    /// <summary>Exclude DevDependencies / packages flagged as dev-only.</summary>
    public bool ExcludeDevelopment { get; set; }

    /// <summary>Exclude test projects from the resolved graph.</summary>
    public bool ExcludeTestProjects { get; set; }

    /// <summary>For solutions: recursively walk project references.</summary>
    public bool Recursive { get; set; }

    /// <summary>For project files: include referenced projects as components in the BOM.</summary>
    public bool IncludeProjectReferences { get; set; }

    /// <summary>Disable the hash computation step (air-gap escape hatch).</summary>
    public bool DisableHashComputation { get; set; }

    /// <summary>Skip the implicit <c>dotnet restore</c> the tool performs before walking deps.</summary>
    public bool DisablePackageRestore { get; set; }

    /// <summary>Omit the BOM's serialNumber URN. The 6.x tool removed <c>--set-serial-number</c>; the only knob left is on/off.</summary>
    public bool NoSerialNumber { get; set; }

    /// <summary>Override the BOM metadata.component.name.</summary>
    public string? MetadataComponentName { get; set; }

    /// <summary>Override the BOM metadata.component.version.</summary>
    public string? MetadataComponentVersion { get; set; }

    /// <summary>Optional GitHub Personal Access Token for license-metadata lookups. Typed as <see cref="Secret"/> so the runner's redaction table covers it.</summary>
    public Secret? GitHubLicenseToken { get; set; }

    /// <summary>GitHub username to pair with <see cref="GitHubLicenseToken"/>. Both are required for license resolution per the 6.x tool.</summary>
    public string? GitHubLicenseUsername { get; set; }

    public string? WorkingDirectory { get; set; }
    public Dictionary<string, string> EnvironmentVariables { get; } = new();

    // -- Fluent setters --
    public CycloneDxGenerateSettings SetPath(string? p) { Path = p; return this; }
    public CycloneDxGenerateSettings SetOutputDirectory(string? p) { OutputDirectory = p; return this; }
    public CycloneDxGenerateSettings SetFilename(string? n) { Filename = n; return this; }
    public CycloneDxGenerateSettings SetFormat(CycloneDxFormat f) { Format = f; return this; }
    public CycloneDxGenerateSettings SetSpecVersion(string? v) { SpecVersion = v; return this; }
    public CycloneDxGenerateSettings SetExcludeDevelopment(bool v) { ExcludeDevelopment = v; return this; }
    public CycloneDxGenerateSettings SetExcludeTestProjects(bool v) { ExcludeTestProjects = v; return this; }
    public CycloneDxGenerateSettings SetRecursive(bool v) { Recursive = v; return this; }
    public CycloneDxGenerateSettings SetIncludeProjectReferences(bool v) { IncludeProjectReferences = v; return this; }
    public CycloneDxGenerateSettings SetDisableHashComputation(bool v) { DisableHashComputation = v; return this; }
    public CycloneDxGenerateSettings SetDisablePackageRestore(bool v) { DisablePackageRestore = v; return this; }
    public CycloneDxGenerateSettings SetNoSerialNumber(bool v) { NoSerialNumber = v; return this; }
    public CycloneDxGenerateSettings SetMetadataComponentName(string? name) { MetadataComponentName = name; return this; }
    public CycloneDxGenerateSettings SetMetadataComponentVersion(string? version) { MetadataComponentVersion = version; return this; }
    public CycloneDxGenerateSettings SetGitHubLicense(string username, Secret token) { GitHubLicenseUsername = username; GitHubLicenseToken = token; return this; }
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
            args.Add("--output");
            args.Add(OutputDirectory!);
        }

        if (!string.IsNullOrEmpty(Filename))
        {
            args.Add("--filename");
            args.Add(Filename!);
        }

        // --output-format defaults to Auto (XML) in 6.x; emit explicitly for any non-Auto choice.
        if (Format != CycloneDxFormat.Auto)
        {
            args.Add("--output-format");
            args.Add(FormatToWire(Format));
        }

        if (!string.IsNullOrEmpty(SpecVersion))
        {
            args.Add("--spec-version");
            args.Add(SpecVersion!);
        }

        if (ExcludeDevelopment) args.Add("--exclude-dev");
        if (ExcludeTestProjects) args.Add("--exclude-test-projects");
        if (Recursive) args.Add("--recursive");
        if (IncludeProjectReferences) args.Add("--include-project-references");
        if (DisableHashComputation) args.Add("--disable-hash-computation");
        if (DisablePackageRestore) args.Add("--disable-package-restore");
        if (NoSerialNumber) args.Add("--no-serial-number");

        if (!string.IsNullOrEmpty(MetadataComponentName))
        {
            args.Add("--set-name");
            args.Add(MetadataComponentName!);
        }

        if (!string.IsNullOrEmpty(MetadataComponentVersion))
        {
            args.Add("--set-version");
            args.Add(MetadataComponentVersion!);
        }

        if (RevealGitHubLicenseToken() is { Length: > 0 } token)
        {
            if (string.IsNullOrEmpty(GitHubLicenseUsername))
                throw new InvalidOperationException("CycloneDX 6.x requires both --github-username and --github-token together; SetGitHubLicense provides both.");
            args.Add("--github-username");
            args.Add(GitHubLicenseUsername!);
            args.Add("--github-token");
            args.Add(token);
            args.Add("--enable-github-licenses");
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

    private static string FormatToWire(CycloneDxFormat f) => f switch
    {
        CycloneDxFormat.Json => "Json",
        CycloneDxFormat.UnsafeJson => "UnsafeJson",
        CycloneDxFormat.Xml => "Xml",
        CycloneDxFormat.Auto => "Auto",
        _ => throw new ArgumentOutOfRangeException(nameof(f), f, "Unknown CycloneDX format."),
    };
}
