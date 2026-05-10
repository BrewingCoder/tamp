namespace Tamp.DotNetCoverage.V18;

/// <summary>
/// Output formats supported by <c>dotnet-coverage</c>.
/// </summary>
public enum CoverageFormat
{
    /// <summary>Microsoft binary coverage format (.coverage). Default. Opens in Visual Studio.</summary>
    Coverage,

    /// <summary>Microsoft XML coverage format.</summary>
    Xml,

    /// <summary>Cobertura XML format. The Sonar integration path.</summary>
    Cobertura,

    /// <summary>LCOV format. Less commonly used; emitted by some Sonar configurations.</summary>
    Lcov,
}

internal static class CoverageFormatExtensions
{
    public static string ToFlagValue(this CoverageFormat format) => format switch
    {
        CoverageFormat.Coverage => "coverage",
        CoverageFormat.Xml => "xml",
        CoverageFormat.Cobertura => "cobertura",
        CoverageFormat.Lcov => "lcov",
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Unknown coverage format."),
    };
}
