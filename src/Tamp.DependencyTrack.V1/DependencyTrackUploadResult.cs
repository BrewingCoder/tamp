namespace Tamp.DependencyTrack.V1;

/// <summary>
/// Result of a BOM upload. The token is what
/// <see cref="DependencyTrackClient.IsAnalysisCompleteAsync"/> and
/// <see cref="DependencyTrackClient.WaitForAnalysisCompleteAsync"/>
/// poll against — analysis happens asynchronously inside DT.
/// </summary>
public sealed record DependencyTrackUploadResult
{
    /// <summary>BOM-processing token returned by DT's <c>PUT /api/v1/bom</c>.</summary>
    public string Token { get; init; } = "";
}
