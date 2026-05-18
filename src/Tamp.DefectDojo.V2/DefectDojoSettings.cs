namespace Tamp.DefectDojo.V2;

/// <summary>
/// Connection settings for a <see cref="DefectDojoClient"/>. Values
/// typically come from <c>TAMP_DD_URL</c> + <c>TAMP_DD_TOKEN</c> +
/// <c>TAMP_DD_ENGAGEMENT_ID</c> per <c>docs/security-env-vars.md</c>.
/// </summary>
public sealed record DefectDojoSettings
{
    /// <summary>Base URL of the DefectDojo instance (no trailing slash).</summary>
    public required Uri BaseUrl { get; init; }

    /// <summary>User API v2 Key.</summary>
    public required Secret Token { get; init; }

    /// <summary>Reveals the token for inclusion in the <c>Authorization: Token …</c> header. Lives on the Settings record so call sites in <see cref="DefectDojoClient"/> don't have to invoke <see cref="Secret.Reveal"/> directly (TAMP004 boundary).</summary>
    internal string RevealToken() => Token.Reveal();
}
