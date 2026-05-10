namespace Tamp.DotNetCoverage.V17;

/// <summary>
/// Settings for <c>dotnet-coverage merge</c>. Merges multiple coverage
/// reports into one, optionally converting format. The Sonar integration
/// path uses this to convert collected <c>.coverage</c> binaries into
/// the <c>cobertura</c> XML SonarScanner can ingest.
/// </summary>
public sealed class DotNetCoverageMergeSettings
{
    /// <summary>Input coverage report paths. Add multiple via <see cref="AddInput"/>.</summary>
    public List<string> Inputs { get; } = [];

    /// <summary>Output coverage file path.</summary>
    public string? Output { get; set; }

    /// <summary>Output format. Defaults to binary <c>coverage</c> if unset.</summary>
    public CoverageFormat? OutputFormat { get; set; }

    /// <summary>Delete every input file after merge. Off by default.</summary>
    public bool RemoveInputFiles { get; set; }

    /// <summary>Log file path.</summary>
    public string? LogFile { get; set; }

    public CoverageLogLevel? LogLevel { get; set; }
    public bool DisableConsoleOutput { get; set; }

    /// <summary>Suppress the "Code Coverage" banner. Defaults true.</summary>
    public bool NoLogo { get; set; } = true;

    /// <summary>Working directory for the spawned process.</summary>
    public string? WorkingDirectory { get; set; }

    /// <summary>Additional environment variables.</summary>
    public Dictionary<string, string> EnvironmentVariables { get; } = new();

    public DotNetCoverageMergeSettings AddInput(string path) { Inputs.Add(path); return this; }
    public DotNetCoverageMergeSettings AddInputs(IEnumerable<AbsolutePath> paths)
    {
        foreach (var p in paths) Inputs.Add(p.Value);
        return this;
    }
    public DotNetCoverageMergeSettings AddInputs(IEnumerable<string> paths)
    {
        Inputs.AddRange(paths);
        return this;
    }
    public DotNetCoverageMergeSettings SetOutput(string path) { Output = path; return this; }
    public DotNetCoverageMergeSettings SetOutputFormat(CoverageFormat format) { OutputFormat = format; return this; }
    public DotNetCoverageMergeSettings SetRemoveInputFiles(bool v) { RemoveInputFiles = v; return this; }
    public DotNetCoverageMergeSettings SetLogFile(string? path) { LogFile = path; return this; }
    public DotNetCoverageMergeSettings SetLogLevel(CoverageLogLevel level) { LogLevel = level; return this; }
    public DotNetCoverageMergeSettings SetDisableConsoleOutput(bool v) { DisableConsoleOutput = v; return this; }
    public DotNetCoverageMergeSettings SetNoLogo(bool v) { NoLogo = v; return this; }
    public DotNetCoverageMergeSettings SetWorkingDirectory(string? cwd) { WorkingDirectory = cwd; return this; }
}
