using System.Text.Json;
using Xunit;

namespace Tamp.Sarif.Tests;

public class SarifRoundTripTests
{
    [Fact]
    public void Empty_Log_Roundtrips_With_Defaults()
    {
        var log = new SarifLog();

        var json = SarifWriter.Serialize(log);
        var parsed = SarifReader.Parse(json);

        Assert.Equal("2.1.0", parsed.Version);
        Assert.Equal("https://json.schemastore.org/sarif-2.1.0.json", parsed.Schema);
        Assert.Empty(parsed.Runs);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(42)]
    [InlineData(1337)]
    public void Bogus_Generated_Logs_Roundtrip_Losslessly(int seed)
    {
        Bogus.Randomizer.Seed = new Random(seed);
        var original = SarifFakers.Log().Generate();

        var json1 = SarifWriter.Serialize(original);
        var reparsed = SarifReader.Parse(json1);
        var json2 = SarifWriter.Serialize(reparsed);

        JsonAssert.Equivalent(json1, json2);
    }

    [Fact]
    public void Schema_Property_Maps_To_Dollar_Schema_Key()
    {
        var log = new SarifLog { Schema = "https://example.test/sarif-schema.json" };
        var json = SarifWriter.Serialize(log);

        using var doc = JsonDocument.Parse(json);
        Assert.True(doc.RootElement.TryGetProperty("$schema", out var schemaProp));
        Assert.Equal("https://example.test/sarif-schema.json", schemaProp.GetString());
        Assert.False(doc.RootElement.TryGetProperty("schema", out _));
    }

    [Fact]
    public void Level_Serialises_As_Lowercase_String()
    {
        var log = new SarifLog
        {
            Runs = [new SarifRun { Results = [new SarifResult { RuleId = "x", Level = SarifLevel.Error, Message = new SarifMessage { Text = "boom" } }] }],
        };

        var json = SarifWriter.Serialize(log);

        Assert.Contains("\"level\": \"error\"", json);
        Assert.DoesNotContain("\"level\": \"Error\"", json);
        Assert.DoesNotContain("\"level\": 3", json);
    }

    [Fact]
    public void Level_Rejects_Integer_Values_On_Parse()
    {
        const string json = """
        {
          "version": "2.1.0",
          "runs": [{ "tool": { "driver": { "name": "x" } }, "results": [{ "ruleId": "r", "level": 3, "message": { "text": "m" } }] }]
        }
        """;

        Assert.Throws<InvalidDataException>(() => SarifReader.Parse(json));
    }

    [Fact]
    public void Null_Optional_Properties_Are_Omitted_From_Output()
    {
        var log = new SarifLog
        {
            Runs = [new SarifRun
            {
                Tool = new SarifTool { Driver = new SarifToolComponent { Name = "x" } },
                Results = [new SarifResult { Message = new SarifMessage { Text = "m" } }],
            }],
        };

        var json = SarifWriter.Serialize(log);

        Assert.DoesNotContain("\"ruleId\":", json);
        Assert.DoesNotContain("\"locations\":", json);
        Assert.DoesNotContain("\"markdown\":", json);
    }

    [Fact]
    public void Empty_String_Throws_On_Parse()
    {
        Assert.Throws<InvalidDataException>(() => SarifReader.Parse(""));
    }

    [Fact]
    public void Null_Input_Throws_ArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => SarifReader.Parse(null!));
        Assert.Throws<ArgumentNullException>(() => SarifWriter.Serialize(null!));
    }
}
