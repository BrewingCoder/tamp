namespace Tamp;

/// <summary>
/// Marks a property or field on a <see cref="TampBuild"/> subclass as
/// receiving a build parameter. Parameter values resolve from, in order:
/// command-line argument, environment variable, the property's default.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class ParameterAttribute : Attribute
{
    public ParameterAttribute(string? description = null) => Description = description;

    /// <summary>Human-readable description for <c>--list</c> output.</summary>
    public string? Description { get; }

    /// <summary>
    /// Override the environment-variable name. Default mapping is to convert
    /// the property name to UPPER_SNAKE_CASE (so <c>Configuration</c> maps to
    /// <c>CONFIGURATION</c>). Set this to use a different name.
    /// </summary>
    public string? EnvironmentVariable { get; init; }

    /// <summary>
    /// Override the command-line flag name. Default mapping is to convert the
    /// property name to kebab-case (so <c>Configuration</c> maps to
    /// <c>--configuration</c>). Set this to use a different name.
    /// </summary>
    public string? Name { get; init; }
}

/// <summary>
/// Marks a property or field on a <see cref="TampBuild"/> subclass as
/// receiving a sensitive value. Resolution order: CI vendor's secret store,
/// local secret store, environment variable, interactive prompt.
/// </summary>
/// <remarks>
/// The decorated member's type must be <see cref="Secret"/>. Secrets are
/// resolved lazily — only when a target that requires the secret is about
/// to run — so unrelated targets do not fail because an unrelated secret
/// is missing.
/// </remarks>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class SecretAttribute : Attribute
{
    public SecretAttribute(string? description = null) => Description = description;

    public string? Description { get; }
    public string? EnvironmentVariable { get; init; }
    public string? Name { get; init; }

    /// <summary>
    /// When <c>true</c> (default), <see cref="SecretBinder.EnsureResolved"/>
    /// may prompt for the value at run time if it wasn't supplied via
    /// the earlier resolution legs and an interactive TTY is attached.
    /// Set to <c>false</c> on secrets that must come from CI / a secret
    /// store — keeps a CI run from hanging waiting for input.
    /// </summary>
    public bool AllowInteractivePrompt { get; init; } = true;

    /// <summary>
    /// When <c>true</c> (default), <see cref="SecretBinder.Bind"/>
    /// consults the host's OS keychain (macOS Keychain / libsecret /
    /// Windows Credential Manager) as a fallback when the environment
    /// variable isn't set. Lookup key is the secret's
    /// <see cref="EnvironmentVariable"/> (or <c>UPPER_SNAKE_CASE</c>
    /// of the member name) under the service / target name
    /// <c>tamp</c>. Set to <c>false</c> on secrets that should ONLY
    /// resolve from env (e.g. when the keychain would be stale or
    /// out-of-band).
    /// </summary>
    public bool UseKeychain { get; init; } = true;
}
