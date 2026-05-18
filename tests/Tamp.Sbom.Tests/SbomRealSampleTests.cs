using Xunit;

namespace Tamp.Sbom.Tests;

/// <summary>
/// Parse CycloneDX documents shaped like what real producers actually emit.
/// Catches regressions that the synthetic Bogus tests miss.
/// </summary>
public class SbomRealSampleTests
{
    // Shaped like a dotnet-CycloneDX output for a small .NET app with one transitive
    // dep that has a known CVE marked not_affected via inline VEX.
    private const string DotnetCycloneDxLikeSample = """
    {
      "bomFormat": "CycloneDX",
      "specVersion": "1.6",
      "serialNumber": "urn:uuid:3e671687-395b-41f5-a30f-a58921a69b79",
      "version": 1,
      "metadata": {
        "timestamp": "2026-05-17T12:00:00Z",
        "component": {
          "type": "application",
          "name": "TampSampleApp",
          "version": "1.0.0"
        }
      },
      "components": [
        {
          "bom-ref": "pkg:nuget/Microsoft.Extensions.Logging@9.0.0",
          "type": "library",
          "name": "Microsoft.Extensions.Logging",
          "version": "9.0.0",
          "purl": "pkg:nuget/Microsoft.Extensions.Logging@9.0.0",
          "hashes": [{ "alg": "SHA-256", "content": "abc123def4567890abc123def4567890abc123def4567890abc123def4567890" }],
          "licenses": [{ "license": { "id": "MIT" } }]
        },
        {
          "bom-ref": "pkg:nuget/Newtonsoft.Json@13.0.3",
          "type": "library",
          "name": "Newtonsoft.Json",
          "version": "13.0.3",
          "purl": "pkg:nuget/Newtonsoft.Json@13.0.3",
          "licenses": [{ "license": { "id": "MIT" } }]
        }
      ],
      "dependencies": [
        {
          "ref": "pkg:nuget/Microsoft.Extensions.Logging@9.0.0",
          "dependsOn": ["pkg:nuget/Newtonsoft.Json@13.0.3"]
        }
      ],
      "vulnerabilities": [
        {
          "bom-ref": "vuln-1",
          "id": "CVE-2024-21907",
          "source": { "name": "NVD", "url": "https://nvd.nist.gov" },
          "ratings": [
            { "source": { "name": "NVD" }, "score": 7.5, "severity": "high", "method": "CVSSv31" }
          ],
          "cwes": [502],
          "description": "Improper handling of exceptional conditions in Newtonsoft.Json.",
          "affects": [
            {
              "ref": "pkg:nuget/Newtonsoft.Json@13.0.3",
              "versions": [{ "version": "13.0.3", "status": "affected" }]
            }
          ],
          "analysis": {
            "state": "not_affected",
            "justification": "code_not_reachable",
            "response": ["will_not_fix"],
            "detail": "The vulnerable JsonConvert.DeserializeObject<T>() overload is not invoked anywhere in this build."
          }
        }
      ]
    }
    """;

    [Fact]
    public void Dotnet_CycloneDx_Like_Sample_Parses_Fully()
    {
        var bom = SbomReader.Parse(DotnetCycloneDxLikeSample);

        Assert.Equal("CycloneDX", bom.BomFormat);
        Assert.Equal("1.6", bom.SpecVersion);
        Assert.Equal("urn:uuid:3e671687-395b-41f5-a30f-a58921a69b79", bom.SerialNumber);

        Assert.NotNull(bom.Metadata);
        Assert.Equal("TampSampleApp", bom.Metadata!.Component!.Name);
        Assert.Equal("application", bom.Metadata.Component.Type);

        Assert.Equal(2, bom.Components!.Count);
        Assert.Equal("Microsoft.Extensions.Logging", bom.Components[0].Name);
        Assert.Equal("pkg:nuget/Microsoft.Extensions.Logging@9.0.0", bom.Components[0].BomRef);

        var dep = Assert.Single(bom.Dependencies!);
        Assert.Equal("pkg:nuget/Microsoft.Extensions.Logging@9.0.0", dep.Ref);
        Assert.Single(dep.DependsOn!);

        var vuln = Assert.Single(bom.Vulnerabilities!);
        Assert.Equal("CVE-2024-21907", vuln.Id);
        Assert.Equal(CycloneDxSeverity.High, vuln.Ratings![0].Severity);
        Assert.Equal(7.5, vuln.Ratings[0].Score);

        Assert.NotNull(vuln.Analysis);
        Assert.Equal(CycloneDxVexState.NotAffected, vuln.Analysis!.State);
        Assert.Equal(CycloneDxVexJustification.CodeNotReachable, vuln.Analysis.Justification);
        Assert.Equal(CycloneDxVexResponse.WillNotFix, vuln.Analysis.Response![0]);
        Assert.Contains("not invoked", vuln.Analysis.Detail);
    }

    [Fact]
    public void Dotnet_CycloneDx_Like_Sample_Roundtrips_Without_Loss()
    {
        var original = SbomReader.Parse(DotnetCycloneDxLikeSample);
        var json1 = SbomWriter.Serialize(original);
        var reparsed = SbomReader.Parse(json1);
        var json2 = SbomWriter.Serialize(reparsed);

        JsonAssert.Equivalent(json1, json2);
    }
}
