namespace Tamp.Sbom;

/// <summary>
/// Implemented by anything that consumes an SBOM — most notably
/// Tamp.DependencyTrack, which uploads the BOM and then polls for the
/// async analysis to complete.
/// </summary>
public interface ISbomSink
{
    /// <summary>
    /// Submit the BOM to the sink. Returns when the sink has acknowledged
    /// receipt; sinks with downstream async processing (like
    /// Dependency-Track's vulnerability analysis) expose their own polling
    /// helpers separately rather than blocking inside this call.
    /// </summary>
    Task SubmitAsync(CycloneDxBom bom, CancellationToken cancellationToken = default);
}
