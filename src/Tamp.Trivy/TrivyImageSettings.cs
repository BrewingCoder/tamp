namespace Tamp.Trivy;

/// <summary>
/// Settings for <see cref="Trivy.ScanImage"/>. Target an image by ref
/// (registry / local) via <see cref="ImageRef"/> OR a local tar via
/// <see cref="InputTarFile"/> — one of the two is required.
/// </summary>
public sealed class TrivyImageSettings : TrivyScanSettingsBase
{
    /// <summary>Image reference, e.g. <c>python:3.12-alpine</c> or <c>registry.example.com/team/image:tag</c>. Positional.</summary>
    public string? ImageRef { get; set; }

    /// <summary>Local tarball produced by <c>docker save</c> etc. Maps to <c>--input</c>. Mutually exclusive with <see cref="ImageRef"/>.</summary>
    public string? InputTarFile { get; set; }

    /// <summary>Override the image platform (multi-arch images). Maps to <c>--platform</c>.</summary>
    public string? Platform { get; set; }

    /// <summary>Override the image-source priority (docker / containerd / podman / remote). Maps to <c>--image-src</c>.</summary>
    public List<string> ImageSources { get; } = new();

    // Fluent setters
    public TrivyImageSettings SetImageRef(string? r) { ImageRef = r; return this; }
    public TrivyImageSettings SetInputTarFile(string? p) { InputTarFile = p; return this; }
    public TrivyImageSettings SetPlatform(string? v) { Platform = v; return this; }
    public TrivyImageSettings AddImageSource(string source) { ImageSources.Add(source); return this; }
    public TrivyImageSettings SetOutputFile(string? p) { OutputFile = p; return this; }
    public TrivyImageSettings SetFormat(TrivyFormat f) { Format = f; return this; }
    public TrivyImageSettings AddSeverity(TrivySeverity s) { SeverityFilter.Add(s); return this; }
    public TrivyImageSettings AddScanner(TrivyScanner s) { Scanners.Add(s); return this; }
    public TrivyImageSettings SetIgnoreUnfixed(bool v) { IgnoreUnfixed = v; return this; }
    public TrivyImageSettings SetSkipDbUpdate(bool v) { SkipDbUpdate = v; return this; }
    public TrivyImageSettings SetSkipJavaDbUpdate(bool v) { SkipJavaDbUpdate = v; return this; }
    public TrivyImageSettings SetQuiet(bool v) { Quiet = v; return this; }
    public TrivyImageSettings SetNoProgress(bool v) { NoProgress = v; return this; }
    public TrivyImageSettings SetExitCode(int? c) { ExitCode = c; return this; }
    public TrivyImageSettings SetIgnoreFile(string? p) { IgnoreFile = p; return this; }
    public TrivyImageSettings SetWorkingDirectory(string? cwd) { WorkingDirectory = cwd; return this; }

    protected override IEnumerable<string> BuildSubcommandArguments()
    {
        if (string.IsNullOrEmpty(ImageRef) && string.IsNullOrEmpty(InputTarFile))
            throw new InvalidOperationException("TrivyImageSettings requires either ImageRef or InputTarFile.");
        if (!string.IsNullOrEmpty(ImageRef) && !string.IsNullOrEmpty(InputTarFile))
            throw new InvalidOperationException("TrivyImageSettings.ImageRef and .InputTarFile are mutually exclusive.");

        // Subcommand verb first; image-specific flags + positional come last.
        yield return "image";

        if (!string.IsNullOrEmpty(InputTarFile))
        {
            yield return "--input";
            yield return InputTarFile!;
        }

        if (!string.IsNullOrEmpty(Platform))
        {
            yield return "--platform";
            yield return Platform!;
        }

        if (ImageSources.Count > 0)
        {
            yield return "--image-src";
            yield return string.Join(",", ImageSources);
        }

        if (!string.IsNullOrEmpty(ImageRef))
            yield return ImageRef!;
    }
}
