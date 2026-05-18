using System.Text.Json;
using Xunit;

namespace Tamp.Sbom.Tests;

public class SbomRoundTripTests
{
    [Fact]
    public void Empty_Bom_Has_Default_Format_And_Version()
    {
        var bom = new CycloneDxBom();
        var json = SbomWriter.Serialize(bom);
        var parsed = SbomReader.Parse(json);

        Assert.Equal("CycloneDX", parsed.BomFormat);
        Assert.Equal("1.6", parsed.SpecVersion);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(17)]
    [InlineData(99)]
    [InlineData(2026)]
    public void Bogus_Generated_Boms_Roundtrip_Losslessly(int seed)
    {
        Bogus.Randomizer.Seed = new Random(seed);
        var original = SbomFakers.Bom().Generate();

        var json1 = SbomWriter.Serialize(original);
        var reparsed = SbomReader.Parse(json1);
        var json2 = SbomWriter.Serialize(reparsed);

        JsonAssert.Equivalent(json1, json2);
    }

    [Fact]
    public void Bom_Ref_Property_Maps_To_Hyphenated_Json_Key()
    {
        var bom = new CycloneDxBom
        {
            Components = [new CycloneDxComponent { BomRef = "pkg:nuget/Newtonsoft.Json@13.0.3", Name = "Newtonsoft.Json", Version = "13.0.3" }],
            Vulnerabilities = [new CycloneDxVulnerability { BomRef = "v1", Id = "CVE-2025-12345" }],
        };

        var json = SbomWriter.Serialize(bom);
        using var doc = JsonDocument.Parse(json);

        var component = doc.RootElement.GetProperty("components")[0];
        Assert.True(component.TryGetProperty("bom-ref", out var compRef));
        Assert.Equal("pkg:nuget/Newtonsoft.Json@13.0.3", compRef.GetString());
        Assert.False(component.TryGetProperty("bomRef", out _));

        var vuln = doc.RootElement.GetProperty("vulnerabilities")[0];
        Assert.True(vuln.TryGetProperty("bom-ref", out var vulnRef));
        Assert.Equal("v1", vulnRef.GetString());
    }

    [Fact]
    public void Null_Optional_Properties_Are_Omitted()
    {
        var bom = new CycloneDxBom
        {
            Components = [new CycloneDxComponent { Name = "no-extras", Version = "1.0.0", Type = "library" }],
        };

        var json = SbomWriter.Serialize(bom);
        var component = JsonDocument.Parse(json).RootElement.GetProperty("components")[0];

        Assert.False(component.TryGetProperty("group", out _));
        Assert.False(component.TryGetProperty("purl", out _));
        Assert.False(component.TryGetProperty("hashes", out _));
        Assert.False(component.TryGetProperty("licenses", out _));
    }

    [Fact]
    public void Empty_String_Throws_On_Parse()
    {
        Assert.Throws<InvalidDataException>(() => SbomReader.Parse(""));
    }

    [Fact]
    public void Null_Inputs_Throw_ArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => SbomReader.Parse(null!));
        Assert.Throws<ArgumentNullException>(() => SbomWriter.Serialize(null!));
    }
}
