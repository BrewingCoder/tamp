namespace Tamp.DefectDojo.V2;

/// <summary>
/// Optional flags passed alongside an import or reimport. Defaults match
/// the canonical Wave 1 pattern: active findings, unverified (let humans
/// triage), close-old-findings on reimport so retired CVEs go inactive
/// automatically.
/// </summary>
public sealed record DefectDojoImportOptions
{
    /// <summary>Mark findings active on import.</summary>
    public bool Active { get; init; } = true;

    /// <summary>Mark findings verified on import. Default false — Wave 1 leaves verification to human triage.</summary>
    public bool Verified { get; init; }

    /// <summary>On reimport, retire findings that aren't present in the new scan. Default true (the right answer for our retention model).</summary>
    public bool CloseOldFindings { get; init; } = true;

    /// <summary>Optional ISO-8601 date label for the scan. Defaults to today (UTC) DD-side when omitted.</summary>
    public DateOnly? ScanDate { get; init; }

    /// <summary>Optional build identifier — links the scan back to the CI run.</summary>
    public string? BuildId { get; init; }
}
