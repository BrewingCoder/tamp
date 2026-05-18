using System.Text.Json;
using System.Text.Json.Serialization;

namespace Tamp.Sbom;

/// <summary>CycloneDX 1.6 vulnerability severity (§ratings.severity).</summary>
[JsonConverter(typeof(CycloneDxSeverityConverter))]
public enum CycloneDxSeverity
{
    Unknown,
    None,
    Info,
    Low,
    Medium,
    High,
    Critical,
}

internal sealed class CycloneDxSeverityConverter()
    : JsonStringEnumConverter<CycloneDxSeverity>(JsonNamingPolicy.CamelCase, allowIntegerValues: false);

/// <summary>
/// CycloneDX 1.6 VEX state (§vulnerabilities.analysis.state). The "is this
/// CVE actually exploitable in our build?" decision — the killer feature
/// that motivated CycloneDX over SPDX for Tamp's reporting standardisation.
/// </summary>
[JsonConverter(typeof(CycloneDxVexStateConverter))]
public enum CycloneDxVexState
{
    Resolved,
    ResolvedWithPedigree,
    Exploitable,
    InTriage,
    FalsePositive,
    NotAffected,
}

internal sealed class CycloneDxVexStateConverter()
    : JsonStringEnumConverter<CycloneDxVexState>(JsonNamingPolicy.SnakeCaseLower, allowIntegerValues: false);

/// <summary>CycloneDX 1.6 VEX justification for why a vulnerability is not exploitable.</summary>
[JsonConverter(typeof(CycloneDxVexJustificationConverter))]
public enum CycloneDxVexJustification
{
    CodeNotPresent,
    CodeNotReachable,
    RequiresConfiguration,
    RequiresDependency,
    RequiresEnvironment,
    ProtectedByCompiler,
    ProtectedAtRuntime,
    ProtectedAtPerimeter,
    ProtectedByMitigatingControl,
}

internal sealed class CycloneDxVexJustificationConverter()
    : JsonStringEnumConverter<CycloneDxVexJustification>(JsonNamingPolicy.SnakeCaseLower, allowIntegerValues: false);

/// <summary>CycloneDX 1.6 VEX response (what the project plans to do about it).</summary>
[JsonConverter(typeof(CycloneDxVexResponseConverter))]
public enum CycloneDxVexResponse
{
    CanNotFix,
    WillNotFix,
    Update,
    Rollback,
    WorkaroundAvailable,
}

internal sealed class CycloneDxVexResponseConverter()
    : JsonStringEnumConverter<CycloneDxVexResponse>(JsonNamingPolicy.SnakeCaseLower, allowIntegerValues: false);
