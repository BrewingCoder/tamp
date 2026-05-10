namespace Tamp;

/// <summary>
/// A typed wrapper around a sensitive value (API key, password, token).
/// The type system makes accidental leaks harder: <see cref="ToString"/> never
/// returns the value, and the value is reachable only via <see cref="Reveal"/>
/// which is internal-only — visible to the runner's process-spawn path and to
/// tests, but not to wrappers, build scripts, or arbitrary library code.
/// </summary>
/// <remarks>
/// What this type prevents: accidental inclusion of the secret in log output,
/// dry-run output, error messages, and standard <see cref="object.ToString"/>
/// calls.
/// What it does NOT prevent: a child process exposing the secret in its
/// argument list to the OS process table while it runs (an OS-level concern),
/// or runtime crash dumps if the process is terminated abnormally. Tool
/// wrappers should prefer stdin / file-based secret passing where the wrapped
/// tool supports it (<c>docker login --password-stdin</c>, etc.).
/// </remarks>
public sealed class Secret
{
    private readonly string _value;

    public Secret(string name, string value)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        if (string.IsNullOrEmpty(name))
            throw new ArgumentException("Secret name must be non-empty.", nameof(name));
        _value = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>Identifier for this secret. Safe to log; this is not the value.</summary>
    public string Name { get; }

    /// <summary>
    /// Exposes the underlying value. Internal access only — the runner uses this
    /// when spawning a child process; tests use it via <see cref="System.Runtime.CompilerServices.InternalsVisibleToAttribute"/>.
    /// Wrappers and build scripts cannot reach this method.
    /// </summary>
    internal string Reveal() => _value;

    /// <summary>
    /// Always returns a redacted form. Critically, this is what gets called by
    /// <c>string.Format</c>, string interpolation, structured loggers, and most
    /// debugger displays — so secrets that "leak" to those surfaces will appear
    /// as <c>&lt;Secret:Name&gt;</c> rather than the value.
    /// </summary>
    public override string ToString() => $"<Secret:{Name}>";

    /// <inheritdoc/>
    public override bool Equals(object? obj) => ReferenceEquals(this, obj);

    /// <inheritdoc/>
    public override int GetHashCode() => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this);
}
