using Xunit;

namespace Tamp.Sarif.Tests;

/// <summary>
/// Parse SARIF documents shaped like what real scanners actually emit.
/// These are smaller-than-production but spec-conformant — the goal is
/// to catch type-graph regressions that synthetic Bogus tests miss.
/// </summary>
public class SarifRealSampleTests
{
    private const string OpenGrepLikeSample = """
    {
      "version": "2.1.0",
      "$schema": "https://json.schemastore.org/sarif-2.1.0.json",
      "runs": [
        {
          "tool": {
            "driver": {
              "name": "opengrep",
              "semanticVersion": "1.2.3",
              "informationUri": "https://opengrep.dev",
              "rules": [
                {
                  "id": "csharp.lang.security.audit.hardcoded-secret",
                  "name": "hardcoded-secret",
                  "shortDescription": { "text": "Hard-coded secret detected." },
                  "fullDescription": { "text": "A literal that looks like a secret was found in source." },
                  "helpUri": "https://opengrep.dev/r/csharp.lang.security.audit.hardcoded-secret",
                  "defaultConfiguration": { "level": "error" }
                }
              ]
            }
          },
          "results": [
            {
              "ruleId": "csharp.lang.security.audit.hardcoded-secret",
              "level": "error",
              "message": { "text": "Hard-coded API key on Foo.cs:42." },
              "locations": [
                {
                  "physicalLocation": {
                    "artifactLocation": { "uri": "src/Foo.cs", "uriBaseId": "%SRCROOT%" },
                    "region": { "startLine": 42, "startColumn": 9, "endLine": 42, "endColumn": 50 }
                  }
                }
              ]
            }
          ]
        }
      ]
    }
    """;

    [Fact]
    public void OpenGrep_Like_Sample_Parses_Fully()
    {
        var log = SarifReader.Parse(OpenGrepLikeSample);

        Assert.Equal("2.1.0", log.Version);
        var run = Assert.Single(log.Runs);
        Assert.Equal("opengrep", run.Tool.Driver.Name);
        Assert.Equal("1.2.3", run.Tool.Driver.SemanticVersion);

        var rule = Assert.Single(run.Tool.Driver.Rules!);
        Assert.Equal("csharp.lang.security.audit.hardcoded-secret", rule.Id);
        Assert.Equal(SarifLevel.Error, rule.DefaultConfiguration!.Level);

        var result = Assert.Single(run.Results!);
        Assert.Equal(SarifLevel.Error, result.Level);
        Assert.Equal("Hard-coded API key on Foo.cs:42.", result.Message.Text);

        var location = Assert.Single(result.Locations!);
        Assert.Equal("src/Foo.cs", location.PhysicalLocation!.ArtifactLocation!.Uri);
        Assert.Equal("%SRCROOT%", location.PhysicalLocation.ArtifactLocation.UriBaseId);
        Assert.Equal(42, location.PhysicalLocation.Region!.StartLine);
    }

    [Fact]
    public void OpenGrep_Like_Sample_Roundtrips_Without_Loss()
    {
        var original = SarifReader.Parse(OpenGrepLikeSample);
        var json1 = SarifWriter.Serialize(original);
        var reparsed = SarifReader.Parse(json1);
        var json2 = SarifWriter.Serialize(reparsed);

        JsonAssert.Equivalent(json1, json2);
    }
}
