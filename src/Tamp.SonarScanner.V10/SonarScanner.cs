namespace Tamp.SonarScanner.V10;

/// <summary>
/// Wrapper for SonarScanner for .NET (the <c>dotnet-sonarscanner</c>
/// global tool, current major v10). Two-phase invocation: <see cref="Begin"/>
/// before the build, the build runs (where the scanner instruments
/// MSBuild), then <see cref="End"/> uploads results to the Sonar server.
/// </summary>
/// <remarks>
/// Resolve the tool via <see cref="NuGetPackageAttribute"/> on the build
/// class:
/// <code>
/// [NuGetPackage("dotnet-sonarscanner", Version = "10.0.0")] readonly Tool Sonar;
/// </code>
/// Or hand-construct a <see cref="Tool"/> if the binary is on a non-
/// standard path or system PATH.
/// <para>
/// For non-.NET projects (Java, JS, Python, generic), use the Java-CLI
/// sibling package <c>Tamp.SonarScannerCli.V6</c> instead — it has a
/// different invocation pattern (single-phase) and configuration model
/// (reads <c>sonar-project.properties</c> by default).
/// </para>
/// </remarks>
public static class SonarScanner
{
    /// <summary>Build the <c>begin</c> phase command plan.</summary>
    public static CommandPlan Begin(Tool tool, Action<SonarBeginSettings> configure)
    {
        if (tool is null) throw new ArgumentNullException(nameof(tool));
        if (configure is null) throw new ArgumentNullException(nameof(configure));

        var s = new SonarBeginSettings();
        configure(s);

        if (string.IsNullOrEmpty(s.ProjectKey))
            throw new InvalidOperationException("SonarScanner.Begin requires ProjectKey.");

        var args = new List<string> { "begin", $"/k:{s.ProjectKey}" };
        if (!string.IsNullOrEmpty(s.ProjectName)) args.Add($"/n:{s.ProjectName}");
        if (!string.IsNullOrEmpty(s.ProjectVersion)) args.Add($"/v:{s.ProjectVersion}");
        if (!string.IsNullOrEmpty(s.Organization)) args.Add($"/o:{s.Organization}");
        if (!string.IsNullOrEmpty(s.AnalysisXml)) args.Add($"/s:{s.AnalysisXml}");
        if (!string.IsNullOrEmpty(s.HostUrl)) args.Add($"/d:sonar.host.url={s.HostUrl}");
        if (s.Token is { } t) args.Add($"/d:sonar.token={t.Reveal()}");
        if (s.Verbose) args.Add("/d:sonar.verbose=true");
        foreach (var (k, v) in s.AdditionalProperties)
            args.Add($"/d:{k}={v}");

        return new CommandPlan
        {
            Executable = tool.Executable.Value,
            Arguments = args,
            Environment = new Dictionary<string, string>(s.EnvironmentVariables),
            WorkingDirectory = s.WorkingDirectory ?? tool.WorkingDirectory,
            Secrets = s.Token is null ? Array.Empty<Secret>() : new[] { s.Token },
        };
    }

    /// <summary>Build the <c>end</c> phase command plan.</summary>
    public static CommandPlan End(Tool tool, Action<SonarEndSettings>? configure = null)
    {
        if (tool is null) throw new ArgumentNullException(nameof(tool));
        var s = new SonarEndSettings();
        configure?.Invoke(s);

        var args = new List<string> { "end" };
        if (s.Token is { } t) args.Add($"/d:sonar.token={t.Reveal()}");
        foreach (var (k, v) in s.AdditionalProperties)
            args.Add($"/d:{k}={v}");

        return new CommandPlan
        {
            Executable = tool.Executable.Value,
            Arguments = args,
            Environment = new Dictionary<string, string>(s.EnvironmentVariables),
            WorkingDirectory = s.WorkingDirectory ?? tool.WorkingDirectory,
            Secrets = s.Token is null ? Array.Empty<Secret>() : new[] { s.Token },
        };
    }
}
