namespace Tamp.Syft.V1;

/// <summary>
/// Source-scheme prefix for image scans (<c>scheme:image-ref</c>). Pick
/// the scheme that matches where the image actually lives so syft
/// doesn't waste time asking each provider in turn.
/// </summary>
public enum SyftImageScheme
{
    /// <summary>Pull from a remote OCI registry.</summary>
    Registry,

    /// <summary>Local Docker daemon.</summary>
    Docker,

    /// <summary>Local Podman.</summary>
    Podman,

    /// <summary>OCI tarball produced by <c>docker save</c> / <c>skopeo copy</c>.</summary>
    OciArchive,

    /// <summary>OCI directory layout.</summary>
    OciDir,

    /// <summary>Docker archive (alternative tarball format).</summary>
    DockerArchive,

    /// <summary>SIF (Singularity / Apptainer image format).</summary>
    SingularityImage,
}
