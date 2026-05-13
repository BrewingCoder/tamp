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
    /// Returns the underlying secret value. Made <see langword="public"/> as of Tamp.Core 1.6.0
    /// (TAM-196) — the previous <see langword="internal"/>-with-IVT gate had become friction
    /// without protection. The real masking lives in <see cref="ToString"/>, the
    /// <see cref="CommandPlan.Secrets"/> collection (process-trace masking), and the runner's
    /// env-var masking — none of which depend on this method's visibility.
    /// </summary>
    /// <remarks>
    /// Call sites that are NOT building command-line arguments, env vars, or otherwise plumbing
    /// the value to a child process should be considered suspect. The <c>TAMP004</c> Roslyn
    /// analyzer (ships bundled in <c>Tamp.Core</c> as of 1.6.0) flags <c>Reveal()</c> calls
    /// outside of approved contexts (classes ending in <c>Settings</c> / <c>SettingsBase</c>,
    /// and Tamp framework internals).
    /// </remarks>
    public string Reveal() => _value;

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
