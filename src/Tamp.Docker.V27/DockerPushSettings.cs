namespace Tamp.Docker.V27;

public sealed class DockerPushSettings : DockerSettingsBase
{
    public string? Image { get; set; }
    public bool AllTags { get; set; }
    public bool Quiet { get; set; }
    public bool Disable_Content_Trust { get; set; }
    public string? Platform { get; set; }

    public DockerPushSettings SetImage(string image) { Image = image; return this; }
    public DockerPushSettings SetAllTags(bool v) { AllTags = v; return this; }
    public DockerPushSettings SetQuiet(bool v) { Quiet = v; return this; }
    public DockerPushSettings SetDisableContentTrust(bool v) { Disable_Content_Trust = v; return this; }
    public DockerPushSettings SetPlatform(string? platform) { Platform = platform; return this; }
    public DockerPushSettings SetWorkingDirectory(string? cwd) { WorkingDirectory = cwd; return this; }

    protected override IEnumerable<string> BuildVerbArguments()
    {
        yield return "push";
        if (AllTags) yield return "--all-tags";
        if (Quiet) yield return "--quiet";
        if (Disable_Content_Trust) yield return "--disable-content-trust";
        if (!string.IsNullOrEmpty(Platform)) { yield return "--platform"; yield return Platform!; }
        if (string.IsNullOrEmpty(Image))
            throw new InvalidOperationException("Docker.Push requires Image.");
        yield return Image!;
    }
}
