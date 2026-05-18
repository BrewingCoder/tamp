namespace Tamp.Sbom;

/// <summary>
/// Implemented by anything that produces an SBOM — Tamp.CycloneDx
/// (dotnet-CycloneDX for managed projects), Tamp.Syft (for containers and
/// non-.NET artifacts), and so on.
/// </summary>
public interface ISbomSource
{
    /// <summary>
    /// Generate the SBOM and return the typed result. Implementations
    /// SHOULD populate metadata.timestamp and metadata.component so the BOM
    /// is self-describing.
    /// </summary>
    Task<CycloneDxBom> GenerateAsync(CancellationToken cancellationToken = default);
}
