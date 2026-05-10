namespace Tamp.SonarScanner.V10;

/// <summary>
/// Settings for the <c>end</c> phase of SonarScanner for .NET. Almost
/// always a one-liner: pass the same token used in <c>begin</c>. The
/// scanner reads its remaining state from the <c>.sonarqube/</c>
/// directory <c>begin</c> wrote to disk.
/// </summary>
public sealed class SonarEndSettings
{
    /// <summary>Authentication token (same one used in begin).</summary>
    public Secret? Token { get; set; }

    /// <summary>Free-form additional properties (rare for end-phase).</summary>
    public Dictionary<string, string> AdditionalProperties { get; } = new();

    /// <summary>Working directory for the spawned process. Should match the begin phase's working directory.</summary>
    public string? WorkingDirectory { get; set; }

    /// <summary>Additional environment variables.</summary>
    public Dictionary<string, string> EnvironmentVariables { get; } = new();

    public SonarEndSettings SetToken(Secret token) { Token = token; return this; }
    public SonarEndSettings SetProperty(string name, string value) { AdditionalProperties[name] = value; return this; }
    public SonarEndSettings SetWorkingDirectory(string? cwd) { WorkingDirectory = cwd; return this; }
}
