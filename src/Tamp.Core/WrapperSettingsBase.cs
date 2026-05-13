namespace Tamp;

/// <summary>
/// Optional base class for satellite wrapper settings classes that handle <see cref="Secret"/>
/// values. Layers inheritance-based blessing on top of the
/// <see href="https://github.com/tamp-build/tamp/wiki/Tamp-Analyzers#tamp004">TAMP004</see>
/// analyzer's class-name heuristic.
/// </summary>
/// <remarks>
/// <para>
/// As of Tamp.Core 1.6.0, <see cref="Secret.Reveal"/> is public and gated by the TAMP004
/// Roslyn analyzer. TAMP004 recognizes "approved contexts" by class-name suffix
/// (<c>*Settings</c> / <c>*SettingsBase</c>), namespace (<c>Tamp.Core</c>, <c>Tamp.Cli</c>,
/// <c>Tamp.NetCli.V*</c>), or test-code shape (class ending in <c>Tests</c>, namespace
/// containing <c>.Tests</c>).
/// </para>
/// <para>
/// <b>This base class is an additional opt-in path</b>: satellites can derive their settings
/// classes from <see cref="WrapperSettingsBase"/> and call the <see cref="Reveal"/> helper
/// instead of <c>secret.Reveal()</c> directly. The discoverability benefit is meaningful —
/// new contributors see "what's the canonical way to reveal a secret for a CommandPlan?"
/// in the type system rather than in analyzer documentation. Existing satellites do not
/// need to migrate; the class-name heuristic continues to cover them.
/// </para>
/// <example>
/// <code>
/// public sealed class MyVerbSettings : WrapperSettingsBase
/// {
///     public Secret? ApiKey { get; set; }
///
///     internal void AppendArguments(List&lt;string&gt; args)
///     {
///         if (ApiKey is not null)
///         {
///             args.Add("--api-key");
///             args.Add(Reveal(ApiKey));   // inherited helper; no analyzer warning
///         }
///     }
/// }
/// </code>
/// </example>
/// </remarks>
public abstract class WrapperSettingsBase
{
    /// <summary>
    /// Reveals the cleartext of a <see cref="Secret"/> for inclusion in a command-line argument
    /// or environment variable. Equivalent to <c>secret.Reveal()</c>; surfacing it as a
    /// <see langword="protected"/> static helper makes inheritance-based opt-in to the TAMP004
    /// approved-context heuristic explicit and discoverable.
    /// </summary>
    /// <param name="secret">The secret to reveal. Must not be <see langword="null"/>.</param>
    /// <returns>The cleartext value.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="secret"/> is <see langword="null"/>.</exception>
    protected static string Reveal(Secret secret)
    {
        if (secret is null) throw new ArgumentNullException(nameof(secret));
        return secret.Reveal();
    }
}
