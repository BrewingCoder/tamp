using Bogus;

namespace Tamp.Sarif.Tests;

/// <summary>
/// Bogus-driven generators for SARIF schema records. Returns plausibly-shaped
/// SARIF graphs for round-trip property tests.
/// </summary>
internal static class SarifFakers
{
    public static Faker<SarifMessage> Message() => new Faker<SarifMessage>()
        .CustomInstantiator(f => new SarifMessage { Text = f.Lorem.Sentence(), Markdown = f.Random.Bool() ? f.Lorem.Sentence() : null });

    public static Faker<SarifMultiformatMessage> MultiformatMessage() => new Faker<SarifMultiformatMessage>()
        .CustomInstantiator(f => new SarifMultiformatMessage { Text = f.Lorem.Sentence(), Markdown = f.Random.Bool() ? f.Lorem.Sentence() : null });

    public static Faker<SarifRegion> Region() => new Faker<SarifRegion>()
        .CustomInstantiator(f => new SarifRegion
        {
            StartLine = f.Random.Int(1, 5000),
            StartColumn = f.Random.Int(1, 200),
            EndLine = f.Random.Int(1, 5000),
            EndColumn = f.Random.Int(1, 200),
        });

    public static Faker<SarifArtifactLocation> ArtifactLocation() => new Faker<SarifArtifactLocation>()
        .CustomInstantiator(f => new SarifArtifactLocation
        {
            Uri = $"src/{f.System.FileName("cs")}",
            UriBaseId = f.Random.Bool() ? "%SRCROOT%" : null,
        });

    public static Faker<SarifPhysicalLocation> PhysicalLocation()
    {
        var artifactFaker = ArtifactLocation();
        var regionFaker = Region();
        return new Faker<SarifPhysicalLocation>()
            .CustomInstantiator(_ => new SarifPhysicalLocation
            {
                ArtifactLocation = artifactFaker.Generate(),
                Region = regionFaker.Generate(),
            });
    }

    public static Faker<SarifLocation> Location()
    {
        var physical = PhysicalLocation();
        return new Faker<SarifLocation>()
            .CustomInstantiator(_ => new SarifLocation { PhysicalLocation = physical.Generate() });
    }

    public static Faker<SarifRule> Rule()
    {
        var shortDesc = MultiformatMessage();
        var fullDesc = MultiformatMessage();
        return new Faker<SarifRule>()
            .CustomInstantiator(f => new SarifRule
            {
                Id = $"{f.Hacker.Adjective()}-{f.Random.Int(1, 999)}",
                Name = f.Hacker.Noun(),
                ShortDescription = shortDesc.Generate(),
                FullDescription = fullDesc.Generate(),
                HelpUri = f.Internet.Url(),
                DefaultConfiguration = new SarifReportingConfiguration { Level = f.PickRandom<SarifLevel>() },
            });
    }

    public static Faker<SarifResult> Result()
    {
        var msg = Message();
        var loc = Location();
        return new Faker<SarifResult>()
            .CustomInstantiator(f => new SarifResult
            {
                RuleId = $"{f.Hacker.Adjective()}-{f.Random.Int(1, 999)}",
                Level = f.PickRandom<SarifLevel>(),
                Message = msg.Generate(),
                Locations = loc.Generate(f.Random.Int(1, 3)),
            });
    }

    public static Faker<SarifToolComponent> ToolComponent()
    {
        var rule = Rule();
        return new Faker<SarifToolComponent>()
            .CustomInstantiator(f => new SarifToolComponent
            {
                Name = f.Company.CompanyName(),
                Version = f.System.Semver(),
                SemanticVersion = f.System.Semver(),
                InformationUri = f.Internet.Url(),
                Rules = rule.Generate(f.Random.Int(1, 5)),
            });
    }

    public static Faker<SarifRun> Run()
    {
        var driver = ToolComponent();
        var result = Result();
        return new Faker<SarifRun>()
            .CustomInstantiator(f => new SarifRun
            {
                Tool = new SarifTool { Driver = driver.Generate() },
                Results = result.Generate(f.Random.Int(0, 8)),
            });
    }

    public static Faker<SarifLog> Log()
    {
        var run = Run();
        return new Faker<SarifLog>()
            .CustomInstantiator(f => new SarifLog { Runs = run.Generate(f.Random.Int(1, 3)) });
    }
}
