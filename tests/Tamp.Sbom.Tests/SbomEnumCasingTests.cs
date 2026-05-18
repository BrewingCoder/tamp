using Xunit;

namespace Tamp.Sbom.Tests;

/// <summary>
/// CycloneDX uses different casing conventions per enum: severity is lowercase,
/// VEX state/justification/response are snake_case. These tests pin those
/// conventions — a regression would silently break CycloneDX-spec compatibility.
/// </summary>
public class SbomEnumCasingTests
{
    [Theory]
    [InlineData(CycloneDxSeverity.Critical, "critical")]
    [InlineData(CycloneDxSeverity.High, "high")]
    [InlineData(CycloneDxSeverity.Medium, "medium")]
    [InlineData(CycloneDxSeverity.Low, "low")]
    [InlineData(CycloneDxSeverity.Info, "info")]
    [InlineData(CycloneDxSeverity.None, "none")]
    [InlineData(CycloneDxSeverity.Unknown, "unknown")]
    public void Severity_Serialises_As_Lowercase(CycloneDxSeverity severity, string expected)
    {
        var bom = new CycloneDxBom
        {
            Vulnerabilities = [new CycloneDxVulnerability { Id = "CVE-x", Ratings = [new CycloneDxVulnerabilityRating { Severity = severity }] }],
        };

        var json = SbomWriter.Serialize(bom);

        Assert.Contains($"\"severity\": \"{expected}\"", json);
    }

    [Theory]
    [InlineData(CycloneDxVexState.NotAffected, "not_affected")]
    [InlineData(CycloneDxVexState.Exploitable, "exploitable")]
    [InlineData(CycloneDxVexState.InTriage, "in_triage")]
    [InlineData(CycloneDxVexState.FalsePositive, "false_positive")]
    [InlineData(CycloneDxVexState.Resolved, "resolved")]
    [InlineData(CycloneDxVexState.ResolvedWithPedigree, "resolved_with_pedigree")]
    public void Vex_State_Serialises_As_Snake_Case(CycloneDxVexState state, string expected)
    {
        var bom = new CycloneDxBom
        {
            Vulnerabilities = [new CycloneDxVulnerability { Id = "CVE-x", Analysis = new CycloneDxVulnerabilityAnalysis { State = state } }],
        };

        var json = SbomWriter.Serialize(bom);

        Assert.Contains($"\"state\": \"{expected}\"", json);
    }

    [Theory]
    [InlineData(CycloneDxVexJustification.CodeNotPresent, "code_not_present")]
    [InlineData(CycloneDxVexJustification.CodeNotReachable, "code_not_reachable")]
    [InlineData(CycloneDxVexJustification.RequiresConfiguration, "requires_configuration")]
    [InlineData(CycloneDxVexJustification.ProtectedByMitigatingControl, "protected_by_mitigating_control")]
    public void Vex_Justification_Serialises_As_Snake_Case(CycloneDxVexJustification j, string expected)
    {
        var bom = new CycloneDxBom
        {
            Vulnerabilities = [new CycloneDxVulnerability
            {
                Id = "CVE-x",
                Analysis = new CycloneDxVulnerabilityAnalysis { State = CycloneDxVexState.NotAffected, Justification = j },
            }],
        };

        var json = SbomWriter.Serialize(bom);

        Assert.Contains($"\"justification\": \"{expected}\"", json);
    }

    [Theory]
    [InlineData(CycloneDxVexResponse.WillNotFix, "will_not_fix")]
    [InlineData(CycloneDxVexResponse.CanNotFix, "can_not_fix")]
    [InlineData(CycloneDxVexResponse.WorkaroundAvailable, "workaround_available")]
    public void Vex_Response_Serialises_As_Snake_Case(CycloneDxVexResponse r, string expected)
    {
        var bom = new CycloneDxBom
        {
            Vulnerabilities = [new CycloneDxVulnerability { Id = "CVE-x", Analysis = new CycloneDxVulnerabilityAnalysis { Response = [r] } }],
        };

        var json = SbomWriter.Serialize(bom);

        Assert.Contains($"\"{expected}\"", json);
    }

    [Fact]
    public void Vex_Enums_Reject_Integer_Values_On_Parse()
    {
        // Severity is an enum, but the JSON encodes integer 3 instead of "high"; the converter (allowIntegerValues:false) should reject.
        const string json = """
        {
          "bomFormat": "CycloneDX",
          "specVersion": "1.6",
          "vulnerabilities": [{ "id": "x", "ratings": [{ "severity": 3 }] }]
        }
        """;

        Assert.Throws<InvalidDataException>(() => SbomReader.Parse(json));
    }
}
