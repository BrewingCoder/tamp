namespace Tamp;

/// <summary>
/// Read-only view of a build's target dependency graph. Computes execution
/// order via topological sort, detects cycles, and validates that all
/// dependencies are known targets.
/// </summary>
public sealed class TargetGraph
{
    private readonly IReadOnlyDictionary<string, TargetSpec> _targets;

    public TargetGraph(IReadOnlyDictionary<string, TargetSpec> targets)
    {
        _targets = targets ?? throw new ArgumentNullException(nameof(targets));
        ValidateDependencies();
    }

    public IReadOnlyDictionary<string, TargetSpec> Targets => _targets;

    public bool Contains(string targetName) => _targets.ContainsKey(targetName);

    /// <summary>
    /// Compute the linear execution order to satisfy <paramref name="rootTargetName"/>:
    /// every dependency appears before its dependent, and the root is last.
    /// Throws if the requested target doesn't exist or if the graph contains
    /// a cycle reachable from it.
    /// </summary>
    public IReadOnlyList<TargetSpec> TopologicalOrderFor(string rootTargetName)
    {
        if (!_targets.TryGetValue(rootTargetName, out _))
            throw new InvalidOperationException(
                $"Target '{rootTargetName}' not found. Known: {string.Join(", ", _targets.Keys)}");

        var order = new List<TargetSpec>();
        var permanent = new HashSet<string>(StringComparer.Ordinal);
        var temporary = new HashSet<string>(StringComparer.Ordinal);
        Visit(rootTargetName, permanent, temporary, order, []);
        return order;
    }

    private void Visit(string name, HashSet<string> permanent, HashSet<string> temporary,
        List<TargetSpec> order, List<string> path)
    {
        if (permanent.Contains(name)) return;
        if (temporary.Contains(name))
        {
            var cycle = string.Join(" → ", path.Concat([name]));
            throw new InvalidOperationException($"Cycle detected in target graph: {cycle}");
        }

        temporary.Add(name);
        path.Add(name);

        var spec = _targets[name];
        foreach (var dep in spec.Dependencies)
        {
            if (!_targets.ContainsKey(dep))
                throw new InvalidOperationException(
                    $"Target '{name}' declares dependency on '{dep}' which is not defined.");
            Visit(dep, permanent, temporary, order, path);
        }

        path.RemoveAt(path.Count - 1);
        temporary.Remove(name);
        permanent.Add(name);
        order.Add(spec);
    }

    private void ValidateDependencies()
    {
        foreach (var (name, spec) in _targets)
        {
            foreach (var dep in spec.Dependencies)
            {
                if (!_targets.ContainsKey(dep))
                    throw new InvalidOperationException(
                        $"Target '{name}' declares dependency on '{dep}' which is not defined.");
            }
        }
    }
}
