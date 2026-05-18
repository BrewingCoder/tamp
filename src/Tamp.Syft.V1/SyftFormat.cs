namespace Tamp.Syft.V1;

/// <summary>
/// Output format for syft (<c>-o / --output</c>). Wave 1 chain default
/// is <see cref="CycloneDxJson"/> so the SBOM slots directly into
/// <c>Tamp.DependencyTrack.V1</c>'s upload path.
/// </summary>
public enum SyftFormat
{
    /// <summary>CycloneDX JSON — the Wave 1 default (Dependency-Track consumes JSON BOMs).</summary>
    CycloneDxJson,

    /// <summary>CycloneDX XML.</summary>
    CycloneDxXml,

    /// <summary>SPDX 2.x JSON.</summary>
    SpdxJson,

    /// <summary>SPDX 2.x tag-value (original SPDX format).</summary>
    SpdxTagValue,

    /// <summary>syft-native JSON (richer than CycloneDX; useful for syft-to-syft round-trips).</summary>
    SyftJson,

    /// <summary>syft's human-readable table (the tool default).</summary>
    SyftTable,

    /// <summary>syft's plain-text report.</summary>
    SyftText,

    /// <summary>GitHub dependency-submission JSON (for the Dependency Graph API).</summary>
    GithubJson,

    /// <summary>Package URLs (purls) only — no metadata.</summary>
    Purls,

    /// <summary>Pair with --template &lt;path&gt;.</summary>
    Template,
}
