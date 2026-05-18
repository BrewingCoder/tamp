using System.Text.Json.Serialization;

namespace Tamp.Sarif;

/// <summary>
/// SARIF 2.1.0 root object (§3.13). Every SARIF file deserialises to this.
/// </summary>
public sealed record SarifLog
{
    public string Version { get; init; } = "2.1.0";

    [JsonPropertyName("$schema")]
    public string? Schema { get; init; } = "https://json.schemastore.org/sarif-2.1.0.json";

    public IReadOnlyList<SarifRun> Runs { get; init; } = [];
}

/// <summary>SARIF 2.1.0 run (§3.14). One per tool invocation.</summary>
public sealed record SarifRun
{
    public SarifTool Tool { get; init; } = new();
    public IReadOnlyList<SarifResult>? Results { get; init; }
}

/// <summary>SARIF 2.1.0 tool (§3.18). Wraps the driver component.</summary>
public sealed record SarifTool
{
    public SarifToolComponent Driver { get; init; } = new();
}

/// <summary>SARIF 2.1.0 toolComponent (§3.19). Describes the analyser.</summary>
public sealed record SarifToolComponent
{
    public string Name { get; init; } = "";
    public string? Version { get; init; }
    public string? SemanticVersion { get; init; }
    public string? InformationUri { get; init; }
    public IReadOnlyList<SarifRule>? Rules { get; init; }
}

/// <summary>SARIF 2.1.0 reportingDescriptor (§3.49). Rule metadata.</summary>
public sealed record SarifRule
{
    public string Id { get; init; } = "";
    public string? Name { get; init; }
    public SarifMultiformatMessage? ShortDescription { get; init; }
    public SarifMultiformatMessage? FullDescription { get; init; }
    public string? HelpUri { get; init; }
    public SarifReportingConfiguration? DefaultConfiguration { get; init; }
}

/// <summary>SARIF 2.1.0 reportingConfiguration (§3.50). Per-rule defaults.</summary>
public sealed record SarifReportingConfiguration
{
    public SarifLevel Level { get; init; } = SarifLevel.Warning;
}

/// <summary>SARIF 2.1.0 result (§3.27). A single finding.</summary>
public sealed record SarifResult
{
    public string? RuleId { get; init; }
    public SarifLevel Level { get; init; } = SarifLevel.Warning;
    public SarifMessage Message { get; init; } = new();
    public IReadOnlyList<SarifLocation>? Locations { get; init; }
}

/// <summary>SARIF 2.1.0 location (§3.28). Wraps a physical/logical location.</summary>
public sealed record SarifLocation
{
    public SarifPhysicalLocation? PhysicalLocation { get; init; }
}

/// <summary>SARIF 2.1.0 physicalLocation (§3.29). File + region.</summary>
public sealed record SarifPhysicalLocation
{
    public SarifArtifactLocation? ArtifactLocation { get; init; }
    public SarifRegion? Region { get; init; }
}

/// <summary>SARIF 2.1.0 artifactLocation (§3.4). Points at a file (URI-form).</summary>
public sealed record SarifArtifactLocation
{
    public string Uri { get; init; } = "";
    public string? UriBaseId { get; init; }
}

/// <summary>SARIF 2.1.0 region (§3.30). Line/column span within an artifact.</summary>
public sealed record SarifRegion
{
    public int? StartLine { get; init; }
    public int? StartColumn { get; init; }
    public int? EndLine { get; init; }
    public int? EndColumn { get; init; }
}

/// <summary>SARIF 2.1.0 message (§3.11). Either plain text or markdown.</summary>
public sealed record SarifMessage
{
    public string? Text { get; init; }
    public string? Markdown { get; init; }
}

/// <summary>SARIF 2.1.0 multiformatMessageString (§3.12). Text required, markdown optional.</summary>
public sealed record SarifMultiformatMessage
{
    public string Text { get; init; } = "";
    public string? Markdown { get; init; }
}
