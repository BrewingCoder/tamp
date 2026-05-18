using System.Text.Json.Serialization;

namespace Tamp.Sbom;

/// <summary>
/// CycloneDX 1.6 BOM root object. The whole document deserialises to this.
/// </summary>
public sealed record CycloneDxBom
{
    public string BomFormat { get; init; } = "CycloneDX";
    public string SpecVersion { get; init; } = "1.6";

    /// <summary>RFC 4122 urn:uuid: identifier for this specific BOM instance.</summary>
    public string? SerialNumber { get; init; }

    /// <summary>BOM-document version (not the SBOM spec version). Increment when republishing the same SBOM with corrections.</summary>
    public int? Version { get; init; }

    public CycloneDxMetadata? Metadata { get; init; }
    public IReadOnlyList<CycloneDxComponent>? Components { get; init; }
    public IReadOnlyList<CycloneDxDependency>? Dependencies { get; init; }
    public IReadOnlyList<CycloneDxVulnerability>? Vulnerabilities { get; init; }
    public IReadOnlyList<CycloneDxProperty>? Properties { get; init; }
}

/// <summary>CycloneDX 1.6 metadata block. When/who/what produced this BOM.</summary>
public sealed record CycloneDxMetadata
{
    public DateTimeOffset? Timestamp { get; init; }

    /// <summary>The root component (the thing being described).</summary>
    public CycloneDxComponent? Component { get; init; }

    public IReadOnlyList<CycloneDxProperty>? Properties { get; init; }
}

/// <summary>CycloneDX 1.6 component. Library, application, container, etc.</summary>
public sealed record CycloneDxComponent
{
    [JsonPropertyName("bom-ref")]
    public string? BomRef { get; init; }

    /// <summary>Component type. CycloneDX-defined values: application, framework, library, container, platform, operating-system, device, device-driver, firmware, file, machine-learning-model, data, cryptographic-asset.</summary>
    public string Type { get; init; } = "library";

    public string Name { get; init; } = "";
    public string? Version { get; init; }
    public string? Group { get; init; }
    public string? Description { get; init; }
    public string? Scope { get; init; }

    /// <summary>Package URL (purl) — the canonical cross-ecosystem identifier.</summary>
    public string? Purl { get; init; }

    public IReadOnlyList<CycloneDxHash>? Hashes { get; init; }
    public IReadOnlyList<CycloneDxLicenseChoice>? Licenses { get; init; }
    public IReadOnlyList<CycloneDxComponent>? Components { get; init; }
    public IReadOnlyList<CycloneDxProperty>? Properties { get; init; }
}

/// <summary>CycloneDX 1.6 hash (algorithm + hex content). Algorithm is a free-form string per spec ("SHA-256", "SHA3-384", "BLAKE2b-256", ...).</summary>
public sealed record CycloneDxHash
{
    public string Alg { get; init; } = "";
    public string Content { get; init; } = "";
}

/// <summary>CycloneDX 1.6 license choice — either a structured license or a free-form SPDX expression.</summary>
public sealed record CycloneDxLicenseChoice
{
    public CycloneDxLicense? License { get; init; }

    /// <summary>SPDX license expression (e.g., "MIT OR Apache-2.0"). Mutually exclusive with License.</summary>
    public string? Expression { get; init; }
}

/// <summary>CycloneDX 1.6 structured license.</summary>
public sealed record CycloneDxLicense
{
    public string? Id { get; init; }
    public string? Name { get; init; }
    public string? Url { get; init; }
}

/// <summary>CycloneDX 1.6 dependency edge. The ref is the dependent; dependsOn lists what it depends on (by bom-ref).</summary>
public sealed record CycloneDxDependency
{
    public string Ref { get; init; } = "";
    public IReadOnlyList<string>? DependsOn { get; init; }
}

/// <summary>CycloneDX 1.6 vulnerability record. Carries the CVE/advisory plus inline VEX analysis.</summary>
public sealed record CycloneDxVulnerability
{
    [JsonPropertyName("bom-ref")]
    public string? BomRef { get; init; }

    /// <summary>Vulnerability identifier (CVE-YYYY-NNNN, GHSA-..., etc.).</summary>
    public string Id { get; init; } = "";

    public CycloneDxVulnerabilitySource? Source { get; init; }
    public IReadOnlyList<CycloneDxVulnerabilityRating>? Ratings { get; init; }
    public IReadOnlyList<int>? Cwes { get; init; }
    public string? Description { get; init; }
    public string? Detail { get; init; }
    public string? Recommendation { get; init; }
    public IReadOnlyList<CycloneDxVulnerabilityAffects>? Affects { get; init; }

    /// <summary>VEX analysis: is this CVE actually exploitable in our build, and why/why-not.</summary>
    public CycloneDxVulnerabilityAnalysis? Analysis { get; init; }

    public DateTimeOffset? Created { get; init; }
    public DateTimeOffset? Published { get; init; }
    public DateTimeOffset? Updated { get; init; }
}

/// <summary>CycloneDX 1.6 vulnerability source (where the advisory came from).</summary>
public sealed record CycloneDxVulnerabilitySource
{
    public string? Name { get; init; }
    public string? Url { get; init; }
}

/// <summary>CycloneDX 1.6 vulnerability rating. CVSS or scanner-specific severity.</summary>
public sealed record CycloneDxVulnerabilityRating
{
    public CycloneDxVulnerabilitySource? Source { get; init; }
    public double? Score { get; init; }
    public CycloneDxSeverity Severity { get; init; } = CycloneDxSeverity.Unknown;

    /// <summary>Scoring method (e.g., "CVSSv31", "OWASP", "other").</summary>
    public string? Method { get; init; }

    /// <summary>Vector string (e.g., a CVSS vector).</summary>
    public string? Vector { get; init; }
}

/// <summary>CycloneDX 1.6 vulnerability affects entry. Which component (by bom-ref) is affected, and at which versions.</summary>
public sealed record CycloneDxVulnerabilityAffects
{
    public string Ref { get; init; } = "";
    public IReadOnlyList<CycloneDxVulnerabilityVersion>? Versions { get; init; }
}

/// <summary>CycloneDX 1.6 vulnerability version constraint.</summary>
public sealed record CycloneDxVulnerabilityVersion
{
    public string? Version { get; init; }
    public string? Range { get; init; }
    public string? Status { get; init; }
}

/// <summary>CycloneDX 1.6 VEX analysis block. The exploitability decision for this vulnerability in this BOM's context.</summary>
public sealed record CycloneDxVulnerabilityAnalysis
{
    public CycloneDxVexState State { get; init; } = CycloneDxVexState.InTriage;
    public CycloneDxVexJustification? Justification { get; init; }
    public IReadOnlyList<CycloneDxVexResponse>? Response { get; init; }
    public string? Detail { get; init; }
}

/// <summary>CycloneDX 1.6 generic name/value property bag.</summary>
public sealed record CycloneDxProperty
{
    public string Name { get; init; } = "";
    public string? Value { get; init; }
}
