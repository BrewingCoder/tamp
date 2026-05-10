namespace Tamp;

/// <summary>
/// A target authoring delegate. The build class declares targets as
/// properties of this type:
/// <code>
/// Target Compile => _ => _.Phase(Phase.Build).Executes(() => DotNet.Build());
/// </code>
/// At schedule time, the executor invokes the delegate with a fresh
/// <see cref="ITargetDefinition"/> and reads the configured target back out.
/// </summary>
public delegate ITargetDefinition Target(ITargetDefinition definition);

/// <summary>
/// Fluent surface for declaring everything Tamp needs to know about a target:
/// phase, dependencies, resources, capability requirements, idempotency,
/// failure handling, telemetry, and the work to perform.
/// </summary>
public interface ITargetDefinition
{
    // Lifecycle / metadata
    ITargetDefinition Phase(Phase phase);
    ITargetDefinition Phase(Phase phase, PhaseDescriptor descriptor);
    ITargetDefinition Description(string description);
    ITargetDefinition Tag(params string[] tags);

    // Dependencies (by target name; nameof(OtherTarget) is the recommended idiom)
    ITargetDefinition DependsOn(params string[] targetNames);

    // Resources
    ITargetDefinition Consumes(Resource resource, ConsumeMode mode);

    // Capability preflight
    ITargetDefinition RequiresNetwork();
    ITargetDefinition RequiresDocker();
    ITargetDefinition RequiresAdmin();
    ITargetDefinition RequiresTool(string toolName, string? minVersion = null);

    // Time
    ITargetDefinition Timeout(TimeSpan timeout);
    ITargetDefinition ExpectedDuration(TimeSpan expected);

    // Memory
    ITargetDefinition MemoryBudget(int megabytes);
    ITargetDefinition MemoryHardLimit(int megabytes);

    // Parallelism
    ITargetDefinition MaxParallelism(int copies);
    ITargetDefinition MaxHostParallelism(int copies);

    // Idempotency / caching
    ITargetDefinition Idempotent();
    ITargetDefinition InputHash(Func<string> hashProducer);
    ITargetDefinition Produces(string globPattern);
    ITargetDefinition RunMode(RunMode mode);

    // Failure handling
    ITargetDefinition FailureMode(FailureMode mode);
    ITargetDefinition Retry(int count, Backoff backoff, params int[] retryableExitCodes);

    // The work
    ITargetDefinition Executes(Action action);
    ITargetDefinition Executes(Func<CommandPlan> planFactory);
    ITargetDefinition Executes(Func<IEnumerable<CommandPlan>> planFactory);
}

/// <summary>
/// Default <see cref="ITargetDefinition"/> implementation. Mutable while the
/// fluent chain is being built; freezes via <see cref="Build"/> into an
/// immutable <see cref="TargetSpec"/> the executor consumes.
/// </summary>
internal sealed class TargetDefinition : ITargetDefinition
{
    private readonly List<string> _dependencies = [];
    private readonly List<(Resource Resource, ConsumeMode Mode)> _resources = [];
    private readonly List<(string Tool, string? MinVersion)> _toolRequirements = [];
    private readonly List<string> _tags = [];
    private readonly List<string> _producedGlobs = [];
    private readonly List<Action> _actions = [];
    private readonly List<Func<IEnumerable<CommandPlan>>> _planFactories = [];

    private Phase _phase;
    private PhaseDescriptor? _phaseDescriptor;
    private string? _description;
    private bool _requiresNetwork;
    private bool _requiresDocker;
    private bool _requiresAdmin;
    private TimeSpan? _timeout;
    private TimeSpan? _expectedDuration;
    private int? _memoryBudgetMb;
    private int? _memoryHardLimitMb;
    private int? _maxParallelism;
    private int? _maxHostParallelism;
    private bool _idempotent;
    private Func<string>? _inputHashProducer;
    private RunMode _runMode = Tamp.RunMode.Always;
    private FailureMode _failureMode = Tamp.FailureMode.Fatal;
    private int? _retryCount;
    private Backoff? _retryBackoff;
    private int[]? _retryableExitCodes;

    public ITargetDefinition Phase(Phase phase) { _phase = phase; return this; }

    public ITargetDefinition Phase(Phase phase, PhaseDescriptor descriptor)
    {
        _phase = phase;
        _phaseDescriptor = descriptor;
        return this;
    }

    public ITargetDefinition Description(string description)
    {
        _description = description;
        return this;
    }

    public ITargetDefinition Tag(params string[] tags)
    {
        _tags.AddRange(tags);
        return this;
    }

    public ITargetDefinition DependsOn(params string[] targetNames)
    {
        _dependencies.AddRange(targetNames);
        return this;
    }

    public ITargetDefinition Consumes(Resource resource, ConsumeMode mode)
    {
        _resources.Add((resource, mode));
        return this;
    }

    public ITargetDefinition RequiresNetwork() { _requiresNetwork = true; return this; }
    public ITargetDefinition RequiresDocker() { _requiresDocker = true; return this; }
    public ITargetDefinition RequiresAdmin() { _requiresAdmin = true; return this; }

    public ITargetDefinition RequiresTool(string toolName, string? minVersion = null)
    {
        _toolRequirements.Add((toolName, minVersion));
        return this;
    }

    public ITargetDefinition Timeout(TimeSpan timeout) { _timeout = timeout; return this; }
    public ITargetDefinition ExpectedDuration(TimeSpan expected) { _expectedDuration = expected; return this; }

    public ITargetDefinition MemoryBudget(int megabytes) { _memoryBudgetMb = megabytes; return this; }
    public ITargetDefinition MemoryHardLimit(int megabytes) { _memoryHardLimitMb = megabytes; return this; }

    public ITargetDefinition MaxParallelism(int copies) { _maxParallelism = copies; return this; }
    public ITargetDefinition MaxHostParallelism(int copies) { _maxHostParallelism = copies; return this; }

    public ITargetDefinition Idempotent() { _idempotent = true; return this; }

    public ITargetDefinition InputHash(Func<string> hashProducer)
    {
        _inputHashProducer = hashProducer;
        return this;
    }

    public ITargetDefinition Produces(string globPattern)
    {
        _producedGlobs.Add(globPattern);
        return this;
    }

    public ITargetDefinition RunMode(RunMode mode) { _runMode = mode; return this; }
    public ITargetDefinition FailureMode(FailureMode mode) { _failureMode = mode; return this; }

    public ITargetDefinition Retry(int count, Backoff backoff, params int[] retryableExitCodes)
    {
        _retryCount = count;
        _retryBackoff = backoff;
        _retryableExitCodes = retryableExitCodes;
        _failureMode = Tamp.FailureMode.Retry;
        return this;
    }

    public ITargetDefinition Executes(Action action)
    {
        _actions.Add(action);
        return this;
    }

    public ITargetDefinition Executes(Func<CommandPlan> planFactory)
    {
        _planFactories.Add(() => [planFactory()]);
        return this;
    }

    public ITargetDefinition Executes(Func<IEnumerable<CommandPlan>> planFactory)
    {
        _planFactories.Add(planFactory);
        return this;
    }

    /// <summary>Freeze the definition into an immutable <see cref="TargetSpec"/>.</summary>
    internal TargetSpec Build(string name) => new()
    {
        Name = name,
        Phase = _phase,
        PhaseDescriptor = _phaseDescriptor,
        Description = _description,
        Tags = _tags.ToArray(),
        Dependencies = _dependencies.ToArray(),
        Resources = _resources.ToArray(),
        RequiresNetwork = _requiresNetwork,
        RequiresDocker = _requiresDocker,
        RequiresAdmin = _requiresAdmin,
        ToolRequirements = _toolRequirements.ToArray(),
        Timeout = _timeout,
        ExpectedDuration = _expectedDuration,
        MemoryBudgetMb = _memoryBudgetMb,
        MemoryHardLimitMb = _memoryHardLimitMb,
        MaxParallelism = _maxParallelism,
        MaxHostParallelism = _maxHostParallelism,
        Idempotent = _idempotent,
        InputHashProducer = _inputHashProducer,
        ProducedGlobs = _producedGlobs.ToArray(),
        RunMode = _runMode,
        FailureMode = _failureMode,
        RetryCount = _retryCount,
        RetryBackoff = _retryBackoff,
        RetryableExitCodes = _retryableExitCodes,
        Actions = _actions.ToArray(),
        PlanFactories = _planFactories.ToArray(),
    };
}

/// <summary>
/// Frozen, immutable description of a target after the fluent chain runs.
/// This is what the executor consumes; it does not see the mutable
/// <see cref="TargetDefinition"/> at any point.
/// </summary>
public sealed record TargetSpec
{
    public required string Name { get; init; }
    public Phase Phase { get; init; }
    public PhaseDescriptor? PhaseDescriptor { get; init; }
    public string? Description { get; init; }
    public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Dependencies { get; init; } = Array.Empty<string>();
    public IReadOnlyList<(Resource Resource, ConsumeMode Mode)> Resources { get; init; }
        = Array.Empty<(Resource, ConsumeMode)>();
    public bool RequiresNetwork { get; init; }
    public bool RequiresDocker { get; init; }
    public bool RequiresAdmin { get; init; }
    public IReadOnlyList<(string Tool, string? MinVersion)> ToolRequirements { get; init; }
        = Array.Empty<(string, string?)>();
    public TimeSpan? Timeout { get; init; }
    public TimeSpan? ExpectedDuration { get; init; }
    public int? MemoryBudgetMb { get; init; }
    public int? MemoryHardLimitMb { get; init; }
    public int? MaxParallelism { get; init; }
    public int? MaxHostParallelism { get; init; }
    public bool Idempotent { get; init; }
    public Func<string>? InputHashProducer { get; init; }
    public IReadOnlyList<string> ProducedGlobs { get; init; } = Array.Empty<string>();
    public RunMode RunMode { get; init; } = RunMode.Always;
    public FailureMode FailureMode { get; init; } = FailureMode.Fatal;
    public int? RetryCount { get; init; }
    public Backoff? RetryBackoff { get; init; }
    public IReadOnlyList<int>? RetryableExitCodes { get; init; }
    public IReadOnlyList<Action> Actions { get; init; } = Array.Empty<Action>();
    public IReadOnlyList<Func<IEnumerable<CommandPlan>>> PlanFactories { get; init; }
        = Array.Empty<Func<IEnumerable<CommandPlan>>>();
}
