namespace Tamp.DefectDojo.V2;

/// <summary>
/// Scan-format keys recognised by DefectDojo's import/reimport endpoints.
/// The wire value (the literal <c>scan_type</c> string DD expects) is held
/// in <see cref="ToWireValue"/> so the enum stays C#-idiomatic while we
/// hand the long human-readable string to DD.
/// </summary>
public enum DefectDojoScanType
{
    /// <summary>Generic SARIF 2.1.0 — what Tamp.OpenGrep, Tamp.Trivy, etc. emit.</summary>
    Sarif,

    /// <summary>The raw Dependency-Track Finding Packaging Format JSON — passthrough from Tamp.DependencyTrack.ExportFindingsAsync.</summary>
    DependencyTrackFpf,
}

internal static class DefectDojoScanTypeExtensions
{
    public static string ToWireValue(this DefectDojoScanType scanType) => scanType switch
    {
        DefectDojoScanType.Sarif => "SARIF",
        DefectDojoScanType.DependencyTrackFpf => "Dependency Track Finding Packaging Format (FPF) Export",
        _ => throw new ArgumentOutOfRangeException(nameof(scanType), scanType, $"Unknown DefectDojo scan type: {scanType}."),
    };

    public static string ToUploadFilename(this DefectDojoScanType scanType) => scanType switch
    {
        DefectDojoScanType.Sarif => "scan.sarif",
        DefectDojoScanType.DependencyTrackFpf => "findings.fpf.json",
        _ => "scan.json",
    };
}
