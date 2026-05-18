using Xunit;

namespace Tamp.Sarif.Tests;

public class SarifMergeTests
{
    [Fact]
    public void Combine_Empty_Returns_Defaults()
    {
        var merged = SarifMerge.Combine([]);

        Assert.Equal("2.1.0", merged.Version);
        Assert.Equal("https://json.schemastore.org/sarif-2.1.0.json", merged.Schema);
        Assert.Empty(merged.Runs);
    }

    [Fact]
    public void Combine_Preserves_All_Runs_From_All_Inputs()
    {
        Bogus.Randomizer.Seed = new Random(99);
        var logs = Enumerable.Range(0, 3).Select(_ => SarifFakers.Log().Generate()).ToList();
        var totalRuns = logs.Sum(l => l.Runs.Count);

        var merged = SarifMerge.Combine(logs);

        Assert.Equal(totalRuns, merged.Runs.Count);
    }

    [Fact]
    public void Combine_Preserves_Each_Input_Run_Identity()
    {
        var logA = new SarifLog { Runs = [new SarifRun { Tool = new SarifTool { Driver = new SarifToolComponent { Name = "opengrep" } } }] };
        var logB = new SarifLog { Runs = [new SarifRun { Tool = new SarifTool { Driver = new SarifToolComponent { Name = "trivy" } } }] };

        var merged = SarifMerge.Combine([logA, logB]);

        Assert.Collection(merged.Runs,
            r => Assert.Equal("opengrep", r.Tool.Driver.Name),
            r => Assert.Equal("trivy", r.Tool.Driver.Name));
    }

    [Fact]
    public void Combine_Uses_First_Non_Empty_Version_And_Schema()
    {
        var a = new SarifLog { Version = "", Schema = null };
        var b = new SarifLog { Version = "2.1.0", Schema = "https://b.example/sarif.json" };
        var c = new SarifLog { Version = "9.9.9", Schema = "https://c.example/sarif.json" };

        var merged = SarifMerge.Combine([a, b, c]);

        Assert.Equal("2.1.0", merged.Version);
        Assert.Equal("https://b.example/sarif.json", merged.Schema);
    }

    [Fact]
    public void Combine_Skips_Null_Logs_In_Sequence()
    {
        var real = new SarifLog { Runs = [new SarifRun { Tool = new SarifTool { Driver = new SarifToolComponent { Name = "x" } } }] };

        var merged = SarifMerge.Combine([null!, real, null!]);

        Assert.Single(merged.Runs);
        Assert.Equal("x", merged.Runs[0].Tool.Driver.Name);
    }

    [Fact]
    public void Combine_Throws_On_Null_Input_Sequence()
    {
        Assert.Throws<ArgumentNullException>(() => SarifMerge.Combine(null!));
    }
}
