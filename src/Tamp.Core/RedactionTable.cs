namespace Tamp;

/// <summary>
/// Maintains the mapping from secret values to placeholder tokens used by
/// the runner to scrub log output, error messages, and dry-run renderings.
/// </summary>
/// <remarks>
/// Two-stage redaction:
///   1. <see cref="Register"/> a <see cref="Secret"/> before its value can
///      reach any logged surface; the table records value → placeholder.
///   2. <see cref="Redact"/> any candidate string before it is written to
///      a logged surface; matches against the registered values get
///      replaced with their placeholder.
///
/// Empty values are not registered (every string would otherwise contain
/// the empty value as a substring). Placeholders are deterministic per
/// secret name so repeated calls produce stable redacted output.
/// </remarks>
public sealed class RedactionTable
{
    private readonly object _lock = new();
    private readonly List<(string Value, string Placeholder)> _entries = new();

    /// <summary>Register a secret so its value gets redacted in subsequent <see cref="Redact"/> calls.</summary>
    public void Register(Secret secret)
    {
        if (secret is null) throw new ArgumentNullException(nameof(secret));
        var value = secret.Reveal();
        if (string.IsNullOrEmpty(value)) return;  // Empty values would match everything.
        var placeholder = $"<Secret:{secret.Name}>";
        lock (_lock)
        {
            // De-duplicate: same value re-registered keeps the first
            // placeholder we saw to avoid order-dependent output.
            if (_entries.Any(e => string.Equals(e.Value, value, StringComparison.Ordinal))) return;
            _entries.Add((value, placeholder));
            // Sort by descending length so that longer matches win when one
            // value is a substring of another. Without this, a short secret
            // value would steal pieces of a longer one's value.
            _entries.Sort((a, b) => b.Value.Length.CompareTo(a.Value.Length));
        }
    }

    /// <summary>
    /// Convenience: register every secret in <paramref name="plan"/>'s
    /// <see cref="CommandPlan.Secrets"/> list.
    /// </summary>
    public void RegisterAll(CommandPlan plan)
    {
        if (plan is null) throw new ArgumentNullException(nameof(plan));
        foreach (var s in plan.Secrets) Register(s);
    }

    /// <summary>Replace every registered secret value in <paramref name="input"/> with its placeholder.</summary>
    public string Redact(string? input)
    {
        if (string.IsNullOrEmpty(input)) return input ?? string.Empty;

        // Snapshot the entries under lock; redaction itself is allocation-
        // friendly and runs against the snapshot.
        (string Value, string Placeholder)[] snapshot;
        lock (_lock) { snapshot = _entries.ToArray(); }
        if (snapshot.Length == 0) return input;

        string current = input;
        foreach (var (value, placeholder) in snapshot)
        {
            if (current.Contains(value, StringComparison.Ordinal))
                current = current.Replace(value, placeholder);
        }
        return current;
    }

    /// <summary>Number of distinct values currently registered.</summary>
    public int Count
    {
        get { lock (_lock) { return _entries.Count; } }
    }
}
