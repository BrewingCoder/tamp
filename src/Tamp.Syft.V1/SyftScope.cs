namespace Tamp.Syft.V1;

/// <summary>
/// Layer-selection strategy for container-image scans (<c>--scope</c>).
/// Irrelevant for directory and archive scans.
/// </summary>
public enum SyftScope
{
    /// <summary>The squashed filesystem of all layers (tool default; what most users want).</summary>
    Squashed,

    /// <summary>Every layer separately — produces a much larger SBOM but exposes per-layer provenance.</summary>
    AllLayers,

    /// <summary>Newer "deep squash" mode that resolves all symlinks and recovers stripped metadata where possible.</summary>
    DeepSquashed,
}
