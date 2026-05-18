namespace Tamp.Syft.V1;

/// <summary>
/// Settings for <see cref="Syft.ScanImage"/>. The <see cref="Scheme"/>
/// + <see cref="ImageRef"/> pair becomes <c>scheme:ref</c> on the wire
/// (e.g. <c>registry:nginx:1.27</c>, <c>oci-archive:/tmp/img.tar</c>).
/// </summary>
public sealed class SyftImageSettings : SyftScanSettingsBase
{
    /// <summary>Image ref or archive path, paired with <see cref="Scheme"/>.</summary>
    public string? ImageRef { get; set; }

    /// <summary>Where the image actually lives. Defaults to <see cref="SyftImageScheme.Registry"/>.</summary>
    public SyftImageScheme Scheme { get; set; } = SyftImageScheme.Registry;

    /// <summary>Layer-selection strategy. Defaults to <see cref="SyftScope.Squashed"/> (matches syft default).</summary>
    public SyftScope Scope { get; set; } = SyftScope.Squashed;

    /// <summary>Platform override for multi-arch images. Maps to <c>--platform</c>.</summary>
    public string? Platform { get; set; }

    /// <summary>Constrain source providers syft consults. Maps to repeated <c>--from</c>.</summary>
    public List<string> From { get; } = new();

    // Fluent setters
    public SyftImageSettings SetImageRef(string? r) { ImageRef = r; return this; }
    public SyftImageSettings SetScheme(SyftImageScheme s) { Scheme = s; return this; }
    public SyftImageSettings SetScope(SyftScope s) { Scope = s; return this; }
    public SyftImageSettings SetPlatform(string? p) { Platform = p; return this; }
    public SyftImageSettings AddFrom(string source) { From.Add(source); return this; }
    public SyftImageSettings AddOutput(SyftFormat f, string? path = null) { Outputs.Add(new SyftOutput(f, path)); return this; }
    public SyftImageSettings SetOutputFile(string? p) { OutputFile = p; return this; }
    public SyftImageSettings SetFormat(SyftFormat f) { Format = f; return this; }
    public SyftImageSettings AddExcludePattern(string p) { ExcludePatterns.Add(p); return this; }
    public SyftImageSettings AddEnrich(string source) { Enrich.Add(source); return this; }
    public SyftImageSettings SetSourceName(string? n) { SourceName = n; return this; }
    public SyftImageSettings SetSourceVersion(string? v) { SourceVersion = v; return this; }
    public SyftImageSettings SetQuiet(bool v) { Quiet = v; return this; }
    public SyftImageSettings SetWorkingDirectory(string? cwd) { WorkingDirectory = cwd; return this; }

    protected override string SourceArgument()
    {
        if (string.IsNullOrEmpty(ImageRef))
            throw new InvalidOperationException("SyftImageSettings.ImageRef is required.");
        return $"{SchemeToWire(Scheme)}:{ImageRef}";
    }

    protected override IEnumerable<string> ExtraArguments()
    {
        if (Scope != SyftScope.Squashed)
        {
            yield return "--scope";
            yield return ScopeToWire(Scope);
        }
        if (!string.IsNullOrEmpty(Platform))
        {
            yield return "--platform";
            yield return Platform!;
        }
        foreach (var f in From)
        {
            yield return "--from";
            yield return f;
        }
    }

    internal static string SchemeToWire(SyftImageScheme s) => s switch
    {
        SyftImageScheme.Registry => "registry",
        SyftImageScheme.Docker => "docker",
        SyftImageScheme.Podman => "podman",
        SyftImageScheme.OciArchive => "oci-archive",
        SyftImageScheme.OciDir => "oci-dir",
        SyftImageScheme.DockerArchive => "docker-archive",
        SyftImageScheme.SingularityImage => "singularity",
        _ => throw new ArgumentOutOfRangeException(nameof(s), s, "Unknown syft image scheme."),
    };

    internal static string ScopeToWire(SyftScope s) => s switch
    {
        SyftScope.Squashed => "squashed",
        SyftScope.AllLayers => "all-layers",
        SyftScope.DeepSquashed => "deep-squashed",
        _ => throw new ArgumentOutOfRangeException(nameof(s), s, "Unknown syft scope."),
    };
}
