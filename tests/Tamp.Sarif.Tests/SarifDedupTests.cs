using Xunit;

namespace Tamp.Sarif.Tests;

public class SarifDedupTests
{
    private static SarifResult Result(string ruleId, string uri, int line, int col, SarifLevel level = SarifLevel.Warning)
        => new()
        {
            RuleId = ruleId,
            Level = level,
            Message = new SarifMessage { Text = $"{ruleId} at {uri}:{line}" },
            Locations = [new SarifLocation
            {
                PhysicalLocation = new SarifPhysicalLocation
                {
                    ArtifactLocation = new SarifArtifactLocation { Uri = uri },
                    Region = new SarifRegion { StartLine = line, StartColumn = col },
                },
            }],
        };

    private static SarifRun Run(string toolName, params SarifResult[] results)
        => new()
        {
            Tool = new SarifTool { Driver = new SarifToolComponent { Name = toolName } },
            Results = results,
        };

    [Fact]
    public void Empty_Log_Stays_Empty()
    {
        var deduped = SarifDedup.Distinct(new SarifLog());
        Assert.Empty(deduped.Runs);
    }

    [Fact]
    public void Single_Run_Without_Duplicates_Passes_Through_Unchanged()
    {
        var log = new SarifLog
        {
            Runs = [Run("opengrep",
                Result("S101", "src/A.cs", 10, 5),
                Result("S102", "src/B.cs", 20, 7))],
        };

        var deduped = SarifDedup.Distinct(log);

        var run = Assert.Single(deduped.Runs);
        Assert.Equal(2, run.Results!.Count);
    }

    [Fact]
    public void Identical_Results_In_Same_Run_Collapse_To_One()
    {
        var dup = Result("CA1822", "src/A.cs", 42, 9);
        var log = new SarifLog
        {
            Runs = [Run("roslyn", dup, dup, dup)],
        };

        var deduped = SarifDedup.Distinct(log);

        var run = Assert.Single(deduped.Runs);
        Assert.Single(run.Results!);
    }

    [Fact]
    public void Identical_Results_Across_Runs_Keep_First_Occurrence_Only()
    {
        var key = Result("CA1822", "src/A.cs", 42, 9);
        var log = new SarifLog
        {
            Runs =
            [
                Run("roslyn-net8", key),
                Run("roslyn-net9", key),
                Run("roslyn-net10", key),
            ],
        };

        var deduped = SarifDedup.Distinct(log);

        Assert.Equal(3, deduped.Runs.Count); // every run preserved
        Assert.Single(deduped.Runs[0].Results!); // first run keeps the result
        Assert.Empty(deduped.Runs[1].Results!);  // later runs lose the dup
        Assert.Empty(deduped.Runs[2].Results!);
        // Tool-of-origin attribution survives on the first run:
        Assert.Equal("roslyn-net8", deduped.Runs[0].Tool.Driver.Name);
    }

    [Fact]
    public void Different_Rules_At_Same_Location_Both_Kept()
    {
        var log = new SarifLog
        {
            Runs = [Run("roslyn",
                Result("CA1822", "src/A.cs", 42, 9),
                Result("S6966",  "src/A.cs", 42, 9))],
        };

        var deduped = SarifDedup.Distinct(log);

        Assert.Equal(2, deduped.Runs[0].Results!.Count);
    }

    [Fact]
    public void Same_Rule_Different_Lines_Both_Kept()
    {
        var log = new SarifLog
        {
            Runs = [Run("roslyn",
                Result("CA1822", "src/A.cs", 10, 5),
                Result("CA1822", "src/A.cs", 11, 5))],
        };

        var deduped = SarifDedup.Distinct(log);

        Assert.Equal(2, deduped.Runs[0].Results!.Count);
    }

    [Fact]
    public void Same_Rule_Same_Line_Different_Columns_Both_Kept()
    {
        var log = new SarifLog
        {
            Runs = [Run("roslyn",
                Result("CA1822", "src/A.cs", 10, 5),
                Result("CA1822", "src/A.cs", 10, 9))],
        };

        var deduped = SarifDedup.Distinct(log);

        Assert.Equal(2, deduped.Runs[0].Results!.Count);
    }

    [Fact]
    public void Result_With_No_Location_Is_Deduped_By_RuleId_Only()
    {
        var noLoc = new SarifResult
        {
            RuleId = "X100",
            Message = new SarifMessage { Text = "no location finding" },
        };

        var log = new SarifLog
        {
            Runs = [Run("toolA", noLoc), Run("toolB", noLoc)],
        };

        var deduped = SarifDedup.Distinct(log);

        Assert.Single(deduped.Runs[0].Results!);
        Assert.Empty(deduped.Runs[1].Results!);
    }

    [Fact]
    public void Result_With_No_RuleId_Treated_As_None_Sentinel_Key()
    {
        var nullRule = new SarifResult
        {
            RuleId = null,
            Message = new SarifMessage { Text = "rule-less" },
            Locations = [new SarifLocation
            {
                PhysicalLocation = new SarifPhysicalLocation
                {
                    ArtifactLocation = new SarifArtifactLocation { Uri = "src/A.cs" },
                    Region = new SarifRegion { StartLine = 1, StartColumn = 1 },
                },
            }],
        };

        var log = new SarifLog { Runs = [Run("t", nullRule, nullRule)] };
        var deduped = SarifDedup.Distinct(log);

        Assert.Single(deduped.Runs[0].Results!);
    }

    [Fact]
    public void Runs_With_Null_Results_Are_Preserved_Untouched()
    {
        // SARIF 2.1.0 lets a run omit results entirely (vs. an empty array).
        // Dedup must not synthesise an empty list — adopters rely on the
        // null vs. [] distinction.
        var log = new SarifLog
        {
            Runs = [new SarifRun { Tool = new SarifTool { Driver = new SarifToolComponent { Name = "ran-clean" } }, Results = null }],
        };

        var deduped = SarifDedup.Distinct(log);

        Assert.Null(deduped.Runs[0].Results);
    }

    [Fact]
    public void Null_Log_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => SarifDedup.Distinct(null!));
    }

    [Fact]
    public void Combine_Distinct_Equals_Combine_Then_Distinct()
    {
        var dup = Result("CA1822", "src/A.cs", 42, 9);
        var a = new SarifLog { Runs = [Run("roslyn-net8", dup)] };
        var b = new SarifLog { Runs = [Run("roslyn-net9", dup)] };

        var oneShot = SarifMerge.CombineDistinct([a, b]);
        var twoStep = SarifDedup.Distinct(SarifMerge.Combine([a, b]));

        JsonAssert.Equivalent(SarifWriter.Serialize(oneShot), SarifWriter.Serialize(twoStep));
    }

    [Fact]
    public void Triplicate_Multi_TFM_Pattern_Collapses_To_Single_Finding()
    {
        // Mirrors the real-world Tamp build pre-dedup: the same finding
        // emitted once per (project, TFM) pair. This is the headline test
        // for what TAM-249 fixes.
        var finding = Result("S6966", "src/Tamp.Cli/Scaffold/Commands/InitCommand.cs", 110, 13);

        var log = new SarifLog
        {
            Runs =
            [
                Run("csc-net8.0",  finding),
                Run("csc-net9.0",  finding),
                Run("csc-net10.0", finding),
            ],
        };

        var deduped = SarifDedup.Distinct(log);

        var total = deduped.Runs.Sum(r => r.Results?.Count ?? 0);
        Assert.Equal(1, total);
        // Attribution: first TFM-run owns the surviving copy.
        Assert.Equal("csc-net8.0", deduped.Runs[0].Tool.Driver.Name);
        Assert.Single(deduped.Runs[0].Results!);
    }
}
