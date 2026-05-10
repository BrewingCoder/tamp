namespace Tamp.SonarScannerCli.V6;

/// <summary>
/// Wrapper for the SonarScanner CLI — the Java <c>sonar-scanner</c>
/// binary, current major v6. Single-phase invocation. Reads
/// <c>sonar-project.properties</c> from the working directory by
/// default; command-line <c>-D</c> flags override anything in the
/// file.
/// </summary>
/// <remarks>
/// The Java CLI is distributed as a zip from Sonar; install it however
/// suits the runner (manual extract, package manager, container image,
/// pre-staged on the runner) and resolve the <see cref="Tool"/> via
/// <c>[NuGetPackage(UseSystemPath = true, ExecutableName = "sonar-scanner")]</c>
/// or by hand-constructing one from a configured path.
/// <para>
/// For .NET / MSBuild projects, use the sibling package
/// <c>Tamp.SonarScanner.V10</c> instead — different tool with
/// two-phase begin/end invocation.
/// </para>
/// </remarks>
public static class SonarScannerCli
{
    /// <summary>
    /// Build a <c>sonar-scanner</c> command plan. Pass an empty
    /// configurer (or <c>null</c>) to run with whatever's in the
    /// working directory's <c>sonar-project.properties</c>; supply
    /// settings to override individual properties on the command
    /// line.
    /// </summary>
    public static CommandPlan Scan(Tool tool, Action<SonarScanSettings>? configure = null)
    {
        if (tool is null) throw new ArgumentNullException(nameof(tool));
        var s = new SonarScanSettings();
        configure?.Invoke(s);

        var args = new List<string>();
        if (!string.IsNullOrEmpty(s.ProjectKey)) args.Add($"-Dsonar.projectKey={s.ProjectKey}");
        if (!string.IsNullOrEmpty(s.HostUrl)) args.Add($"-Dsonar.host.url={s.HostUrl}");
        if (s.Token is { } t) args.Add($"-Dsonar.token={t.Reveal()}");
        if (!string.IsNullOrEmpty(s.Organization)) args.Add($"-Dsonar.organization={s.Organization}");
        if (!string.IsNullOrEmpty(s.ProjectBaseDir)) args.Add($"-Dsonar.projectBaseDir={s.ProjectBaseDir}");
        if (!string.IsNullOrEmpty(s.Sources)) args.Add($"-Dsonar.sources={s.Sources}");
        if (!string.IsNullOrEmpty(s.PropertiesFile)) args.Add($"-Dproject.settings={s.PropertiesFile}");
        foreach (var (k, v) in s.AdditionalProperties)
            args.Add($"-D{k}={v}");

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
