using Bogus;

namespace Tamp.Sbom.Tests;

internal static class SbomFakers
{
    public static Faker<CycloneDxProperty> Property() => new Faker<CycloneDxProperty>()
        .CustomInstantiator(f => new CycloneDxProperty { Name = f.Hacker.Noun(), Value = f.Lorem.Word() });

    public static Faker<CycloneDxHash> Hash() => new Faker<CycloneDxHash>()
        .CustomInstantiator(f => new CycloneDxHash { Alg = f.PickRandom("SHA-256", "SHA-384", "SHA-512", "SHA3-256"), Content = f.Random.Hash(64) });

    public static Faker<CycloneDxLicense> License() => new Faker<CycloneDxLicense>()
        .CustomInstantiator(f => new CycloneDxLicense
        {
            Id = f.PickRandom("MIT", "Apache-2.0", "BSD-3-Clause", "GPL-3.0-or-later"),
            Url = f.Random.Bool() ? f.Internet.Url() : null,
        });

    public static Faker<CycloneDxLicenseChoice> LicenseChoice()
    {
        var lic = License();
        return new Faker<CycloneDxLicenseChoice>()
            .CustomInstantiator(f => new CycloneDxLicenseChoice { License = lic.Generate() });
    }

    public static Faker<CycloneDxComponent> Component()
    {
        var hashes = Hash();
        var licenses = LicenseChoice();
        return new Faker<CycloneDxComponent>()
            .CustomInstantiator(f =>
            {
                var name = f.Hacker.Noun().ToLowerInvariant();
                var version = f.System.Semver();
                return new CycloneDxComponent
                {
                    BomRef = $"pkg:nuget/{name}@{version}",
                    Type = f.PickRandom("library", "application", "framework", "container"),
                    Name = name,
                    Version = version,
                    Group = f.Random.Bool() ? f.Hacker.Noun() : null,
                    Purl = $"pkg:nuget/{name}@{version}",
                    Hashes = hashes.Generate(f.Random.Int(1, 2)),
                    Licenses = licenses.Generate(1),
                };
            });
    }

    public static Faker<CycloneDxDependency> Dependency() => new Faker<CycloneDxDependency>()
        .CustomInstantiator(f => new CycloneDxDependency
        {
            Ref = $"pkg:nuget/{f.Hacker.Noun()}@{f.System.Semver()}",
            DependsOn = Enumerable.Range(0, f.Random.Int(0, 3)).Select(_ => $"pkg:nuget/{f.Hacker.Noun()}@{f.System.Semver()}").ToList(),
        });

    public static Faker<CycloneDxVulnerabilityRating> Rating() => new Faker<CycloneDxVulnerabilityRating>()
        .CustomInstantiator(f => new CycloneDxVulnerabilityRating
        {
            Source = new CycloneDxVulnerabilitySource { Name = f.PickRandom("NVD", "GHSA", "OSV"), Url = f.Internet.Url() },
            Score = f.Random.Double(0, 10),
            Severity = f.PickRandom<CycloneDxSeverity>(),
            Method = f.PickRandom("CVSSv31", "CVSSv4", "OWASP", "other"),
            Vector = f.Random.Bool() ? "CVSS:3.1/AV:N/AC:L/PR:N/UI:N/S:U/C:H/I:H/A:H" : null,
        });

    public static Faker<CycloneDxVulnerabilityAnalysis> Analysis() => new Faker<CycloneDxVulnerabilityAnalysis>()
        .CustomInstantiator(f =>
        {
            var state = f.PickRandom<CycloneDxVexState>();
            return new CycloneDxVulnerabilityAnalysis
            {
                State = state,
                Justification = state == CycloneDxVexState.NotAffected ? f.PickRandom<CycloneDxVexJustification>() : null,
                Response = f.Random.Bool() ? [f.PickRandom<CycloneDxVexResponse>()] : null,
                Detail = f.Lorem.Sentence(),
            };
        });

    public static Faker<CycloneDxVulnerability> Vulnerability()
    {
        var ratings = Rating();
        var analysis = Analysis();
        return new Faker<CycloneDxVulnerability>()
            .CustomInstantiator(f => new CycloneDxVulnerability
            {
                BomRef = $"vuln-{f.Random.Guid()}",
                Id = $"CVE-{f.Random.Int(2020, 2026)}-{f.Random.Int(1000, 99999)}",
                Source = new CycloneDxVulnerabilitySource { Name = "NVD", Url = "https://nvd.nist.gov" },
                Ratings = ratings.Generate(f.Random.Int(1, 2)),
                Cwes = [f.Random.Int(1, 1500)],
                Description = f.Lorem.Sentence(),
                Recommendation = f.Lorem.Sentence(),
                Analysis = analysis.Generate(),
                Published = f.Date.PastOffset(2),
                Updated = f.Date.RecentOffset(),
            });
    }

    public static Faker<CycloneDxBom> Bom()
    {
        var components = Component();
        var deps = Dependency();
        var vulns = Vulnerability();
        return new Faker<CycloneDxBom>()
            .CustomInstantiator(f => new CycloneDxBom
            {
                SerialNumber = $"urn:uuid:{f.Random.Guid()}",
                Version = 1,
                Metadata = new CycloneDxMetadata
                {
                    Timestamp = f.Date.RecentOffset(),
                    Component = new CycloneDxComponent { Type = "application", Name = "root-app", Version = f.System.Semver() },
                },
                Components = components.Generate(f.Random.Int(2, 6)),
                Dependencies = deps.Generate(f.Random.Int(1, 4)),
                Vulnerabilities = vulns.Generate(f.Random.Int(0, 3)),
            });
    }
}
