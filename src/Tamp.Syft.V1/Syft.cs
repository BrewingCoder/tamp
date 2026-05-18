namespace Tamp.Syft.V1;

/// <summary>
/// Tamp wrapper for Anchore's <c>syft</c> CLI (1.x). Three subcommand
/// methods cover the canonical SBOM sources:
/// <list type="bullet">
///   <item><see cref="ScanDirectory"/> — <c>syft scan dir:&lt;path&gt;</c>; walks a source tree, runs every applicable cataloger.</item>
///   <item><see cref="ScanImage"/> — <c>syft scan &lt;scheme&gt;:&lt;ref&gt;</c>; container image from registry / docker / podman / OCI archive / OCI dir.</item>
///   <item><see cref="ScanArchive"/> — <c>syft scan file:&lt;path&gt;</c>; single archive (jar / war / zip / tar / etc).</item>
/// </list>
/// </summary>
/// <remarks>
/// Adopter installs syft (homebrew, apt, or release binary). Default
/// output is CycloneDX JSON — the Wave 1 chain consumer.
/// Complementary to <c>Tamp.CycloneDx.V6</c> which handles managed
/// .NET projects only.
/// </remarks>
public static class Syft
{
    /// <summary>Scan a directory tree (multi-ecosystem cataloger sweep).</summary>
    public static CommandPlan ScanDirectory(Action<SyftDirectorySettings> configure)
    {
        if (configure is null) throw new ArgumentNullException(nameof(configure));
        var settings = new SyftDirectorySettings();
        configure(settings);
        return settings.ToCommandPlan();
    }

    /// <summary>Scan a container image (registry / daemon / archive).</summary>
    public static CommandPlan ScanImage(Action<SyftImageSettings> configure)
    {
        if (configure is null) throw new ArgumentNullException(nameof(configure));
        var settings = new SyftImageSettings();
        configure(settings);
        return settings.ToCommandPlan();
    }

    /// <summary>Scan a single archive (jar/war/zip/tar/…)</summary>
    public static CommandPlan ScanArchive(Action<SyftArchiveSettings> configure)
    {
        if (configure is null) throw new ArgumentNullException(nameof(configure));
        var settings = new SyftArchiveSettings();
        configure(settings);
        return settings.ToCommandPlan();
    }
}
