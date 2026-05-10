namespace Tamp;

/// <summary>
/// What a tool wrapper produces, what the runner dispatches.
/// A plan describes everything the runner needs to execute (or print, in dry-run)
/// without further consultation with the wrapper.
/// </summary>
public sealed record CommandPlan
{
    public required string Executable { get; init; }
    public required IReadOnlyList<string> Arguments { get; init; }
    public IReadOnlyDictionary<string, string> Environment { get; init; }
        = EmptyEnvironment;
    public string? WorkingDirectory { get; init; }
    public IReadOnlyList<Secret> Secrets { get; init; } = Array.Empty<Secret>();

    private static readonly IReadOnlyDictionary<string, string> EmptyEnvironment
        = new Dictionary<string, string>(0);
}
