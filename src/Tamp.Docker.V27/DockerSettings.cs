namespace Tamp.Docker.V27;

/// <summary>
/// Common base for the per-verb Docker settings classes. Holds the
/// shared knobs (working directory, environment) and the path to a
/// CommandPlan via <see cref="ToCommandPlan"/>.
/// </summary>
public abstract class DockerSettingsBase
{
    public string? WorkingDirectory { get; set; }

    public Dictionary<string, string> EnvironmentVariables { get; } = new();

    /// <summary>
    /// Subclasses build the per-verb argument list, starting with the verb
    /// itself.
    /// </summary>
    protected abstract IEnumerable<string> BuildVerbArguments();

    /// <summary>
    /// Subclasses override when they need to feed the child process via
    /// stdin (e.g., <c>docker login --password-stdin</c>). Default: no stdin.
    /// </summary>
    protected virtual string? BuildStandardInput() => null;

    /// <summary>
    /// Subclasses override when their plan should declare typed Secrets so
    /// the runner's redaction table covers their values in any logged
    /// output. Default: no secrets.
    /// </summary>
    protected virtual IReadOnlyList<Secret> BuildSecrets() => Array.Empty<Secret>();

    public CommandPlan ToCommandPlan()
    {
        var args = BuildVerbArguments().ToList();
        return new CommandPlan
        {
            Executable = "docker",
            Arguments = args,
            Environment = new Dictionary<string, string>(EnvironmentVariables),
            WorkingDirectory = WorkingDirectory,
            StandardInput = BuildStandardInput(),
            Secrets = BuildSecrets(),
        };
    }
}
