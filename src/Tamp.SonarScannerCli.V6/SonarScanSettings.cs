namespace Tamp.SonarScannerCli.V6;

/// <summary>
/// Settings for a <c>sonar-scanner</c> run. The Java CLI is single-phase
/// and hybrid-configured: by default it reads <c>sonar-project.properties</c>
/// from <see cref="WorkingDirectory"/>, with command-line <c>-D</c> flags
/// as overrides.
/// </summary>
/// <remarks>
/// Three usage patterns:
/// <list type="bullet">
///   <item>**Properties-file driven.** Leave most settings null; the
///         scanner reads <c>sonar-project.properties</c> from the
///         working directory.</item>
///   <item>**Hybrid.** Set token / host URL via the wrapper (because
///         those are CI-supplied, not committed) and let the rest live
///         in the properties file.</item>
///   <item>**Fully programmatic.** Set every property via the wrapper;
///         the properties file is unnecessary.</item>
/// </list>
/// </remarks>
public sealed class SonarScanSettings
{
    /// <summary>Project key on the Sonar server. Often pulled from the properties file instead.</summary>
    public string? ProjectKey { get; set; }

    /// <summary>Sonar server URL (e.g., <c>https://sonarqube.example.com</c>).</summary>
    public string? HostUrl { get; set; }

    /// <summary>Authentication token. Pass as <see cref="Secret"/> so it's redacted in logs.</summary>
    public Secret? Token { get; set; }

    /// <summary>Sonar Cloud organisation, when targeting Sonar Cloud.</summary>
    public string? Organization { get; set; }

    /// <summary>Override the analysis base directory (rare; defaults to working directory).</summary>
    public string? ProjectBaseDir { get; set; }

    /// <summary>Comma-separated source paths (e.g., <c>src,lib</c>). Often pulled from the properties file.</summary>
    public string? Sources { get; set; }

    /// <summary>
    /// Path to a custom properties file in lieu of the default
    /// <c>sonar-project.properties</c> in the working directory. Emits
    /// as <c>-Dproject.settings=&lt;path&gt;</c>.
    /// </summary>
    public string? PropertiesFile { get; set; }

    /// <summary>Free-form additional <c>-D</c> properties (exclusions, coverage paths, etc.).</summary>
    public Dictionary<string, string> AdditionalProperties { get; } = new();

    /// <summary>Working directory for the spawned process. Where the scanner looks for sonar-project.properties.</summary>
    public string? WorkingDirectory { get; set; }

    /// <summary>Additional environment variables. SONAR_TOKEN / SONAR_HOST_URL are honoured by the scanner natively.</summary>
    public Dictionary<string, string> EnvironmentVariables { get; } = new();

    public SonarScanSettings SetProjectKey(string? key) { ProjectKey = key; return this; }
    public SonarScanSettings SetHostUrl(string? url) { HostUrl = url; return this; }
    public SonarScanSettings SetToken(Secret token) { Token = token; return this; }
    public SonarScanSettings SetOrganization(string? org) { Organization = org; return this; }
    public SonarScanSettings SetProjectBaseDir(string? dir) { ProjectBaseDir = dir; return this; }
    public SonarScanSettings SetSources(string? sources) { Sources = sources; return this; }
    public SonarScanSettings SetPropertiesFile(string? path) { PropertiesFile = path; return this; }
    public SonarScanSettings SetProperty(string name, string value) { AdditionalProperties[name] = value; return this; }
    public SonarScanSettings SetWorkingDirectory(string? cwd) { WorkingDirectory = cwd; return this; }
}
