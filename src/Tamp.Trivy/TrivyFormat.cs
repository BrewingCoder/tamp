namespace Tamp.Trivy;

/// <summary>
/// Trivy report format (<c>--format</c>). Wave 1 chain default is
/// <see cref="Sarif"/>; <see cref="CycloneDx"/> / <see cref="Spdx"/>
/// are useful when piping Trivy's container-image SBOM output back
/// through Dependency-Track.
/// </summary>
public enum TrivyFormat
{
    Table,
    Json,
    Template,
    Sarif,
    CycloneDx,
    Spdx,
    SpdxJson,
    Github,
    CosignVuln,
}
