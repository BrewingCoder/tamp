namespace Tamp.SonarScanner.V10;

/// <summary>
/// Settings for the <c>begin</c> phase of SonarScanner for .NET. Every
/// flag is passed on the command line — the .NET scanner is configured
/// 100% via parameters by convention; <see cref="AnalysisXml"/> can
/// override the default <c>SonarQube.Analysis.xml</c> location for
/// teams that prefer a config file.
/// </summary>
public sealed class SonarBeginSettings
{
    /// <summary>Required. Project key on the Sonar server.</summary>
    public string? ProjectKey { get; set; }

    /// <summary>Optional human-readable project name.</summary>
    public string? ProjectName { get; set; }

    /// <summary>Optional version string for this analysis run (often the git tag or commit short SHA).</summary>
    public string? ProjectVersion { get; set; }

    /// <summary>Sonar server URL (e.g., <c>https://sonarqube.example.com</c>). Defaults to SonarQube Cloud if unset.</summary>
    public string? HostUrl { get; set; }

    /// <summary>Authentication token. Pass as <see cref="Secret"/> so it's redacted in logs.</summary>
    public Secret? Token { get; set; }

    /// <summary>Sonar Cloud organisation, when targeting Sonar Cloud.</summary>
    public string? Organization { get; set; }

    /// <summary>Override the default <c>SonarQube.Analysis.xml</c> location with an absolute path.</summary>
    public string? AnalysisXml { get; set; }

    /// <summary>Enable verbose scanner logging (<c>/d:sonar.verbose=true</c>).</summary>
    public bool Verbose { get; set; }

    /// <summary>Free-form additional properties (<c>sonar.exclusions</c>, coverage report paths, etc.). Each emits as <c>/d:key=value</c>.</summary>
    public Dictionary<string, string> AdditionalProperties { get; } = new();

    /// <summary>Working directory for the spawned process.</summary>
    public string? WorkingDirectory { get; set; }

    /// <summary>Additional environment variables.</summary>
    public Dictionary<string, string> EnvironmentVariables { get; } = new();

    public SonarBeginSettings SetProjectKey(string key) { ProjectKey = key; return this; }
    public SonarBeginSettings SetProjectName(string? name) { ProjectName = name; return this; }
    public SonarBeginSettings SetProjectVersion(string? version) { ProjectVersion = version; return this; }
    public SonarBeginSettings SetHostUrl(string? url) { HostUrl = url; return this; }
    public SonarBeginSettings SetToken(Secret token) { Token = token; return this; }
    public SonarBeginSettings SetOrganization(string? org) { Organization = org; return this; }
    public SonarBeginSettings SetAnalysisXml(string? path) { AnalysisXml = path; return this; }
    public SonarBeginSettings SetVerbose(bool v) { Verbose = v; return this; }
    public SonarBeginSettings SetProperty(string name, string value) { AdditionalProperties[name] = value; return this; }
    public SonarBeginSettings SetWorkingDirectory(string? cwd) { WorkingDirectory = cwd; return this; }
}
