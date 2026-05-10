namespace Tamp;

/// <summary>
/// Resolved tool reference: an executable path plus optional default
/// settings. Modules and build scripts get a <see cref="Tool"/> from a
/// <see cref="NuGetPackageAttribute"/>-decorated property and use
/// <see cref="Plan(string[])"/> or <see cref="Plan(IEnumerable{string})"/>
/// to produce a <see cref="CommandPlan"/> for the executor.
/// </summary>
/// <remarks>
/// Tool is a thin convenience layer over <see cref="CommandPlan"/>. It
/// exists so attribute-driven tool resolution (NuGet, npm, system PATH,
/// local-path lookups) can hand back a single object that downstream
/// build code uses uniformly.
/// </remarks>
public sealed class Tool
{
    public Tool(AbsolutePath executable, string? workingDirectory = null)
    {
        Executable = executable ?? throw new ArgumentNullException(nameof(executable));
        WorkingDirectory = workingDirectory;
    }

    /// <summary>Resolved absolute path to the tool's executable.</summary>
    public AbsolutePath Executable { get; }

    /// <summary>Working directory the runner uses when dispatching plans built from this tool. Null = current directory.</summary>
    public string? WorkingDirectory { get; }

    /// <summary>Build a <see cref="CommandPlan"/> with the given arguments.</summary>
    public CommandPlan Plan(params string[] arguments) => Plan((IEnumerable<string>)arguments);

    /// <inheritdoc cref="Plan(string[])"/>
    public CommandPlan Plan(IEnumerable<string> arguments)
        => new()
        {
            Executable = Executable.Value,
            Arguments = arguments.ToList(),
            WorkingDirectory = WorkingDirectory,
        };
}
