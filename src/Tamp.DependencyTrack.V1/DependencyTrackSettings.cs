namespace Tamp.DependencyTrack.V1;

/// <summary>
/// Connection settings for a <see cref="DependencyTrackClient"/>.
/// Values typically come from <c>TAMP_DT_URL</c> + <c>TAMP_DT_API_KEY</c>
/// per <c>docs/security-env-vars.md</c>; the build script resolves the
/// env vars and constructs this record.
/// </summary>
public sealed record DependencyTrackSettings
{
    /// <summary>Base URL of the Dependency-Track instance (no trailing slash).</summary>
    public required Uri BaseUrl { get; init; }

    /// <summary>API key for a DT team with at least <c>BOM_UPLOAD</c>, <c>VIEW_PORTFOLIO</c>, <c>VIEW_VULNERABILITY</c>.</summary>
    public required Secret ApiKey { get; init; }

    /// <summary>How long <see cref="DependencyTrackClient.WaitForAnalysisCompleteAsync"/> should wait by default. 5 minutes is enough for most BOMs; raise for very large dependency graphs.</summary>
    public TimeSpan DefaultAnalysisTimeout { get; init; } = TimeSpan.FromMinutes(5);

    /// <summary>Reveals the API key for inclusion in the <c>X-Api-Key</c> request header. Kept on the settings record so call sites in <see cref="DependencyTrackClient"/> don't have to invoke <see cref="Secret.Reveal"/> directly (TAMP004 boundary).</summary>
    internal string RevealApiKey() => ApiKey.Reveal();
}
