namespace Tamp.DefectDojo.V2;

/// <summary>
/// What DefectDojo returns from a successful import or reimport. The
/// <see cref="TestId"/> is the row key for the resulting Test record;
/// adopters can deep-link to it for human triage:
/// <c>{BaseUrl}/test/{TestId}</c>.
/// </summary>
public sealed record DefectDojoScanResult
{
    public int TestId { get; init; }

    /// <summary>Number of findings the import created (DD's <c>statistics.before.total</c> minus prior).</summary>
    public int? FindingsCreated { get; init; }

    /// <summary>Number of findings the import closed (reimport only).</summary>
    public int? FindingsClosed { get; init; }
}
