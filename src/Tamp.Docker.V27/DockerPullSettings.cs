namespace Tamp.Docker.V27;

public sealed class DockerPullSettings : DockerSettingsBase
{
    public string? Image { get; set; }
    public bool AllTags { get; set; }
    public bool Quiet { get; set; }
    public bool DisableContentTrust { get; set; }
    public string? Platform { get; set; }

    public DockerPullSettings SetImage(string image) { Image = image; return this; }
    public DockerPullSettings SetAllTags(bool v) { AllTags = v; return this; }
    public DockerPullSettings SetQuiet(bool v) { Quiet = v; return this; }
    public DockerPullSettings SetDisableContentTrust(bool v) { DisableContentTrust = v; return this; }
    public DockerPullSettings SetPlatform(string? platform) { Platform = platform; return this; }
    public DockerPullSettings SetWorkingDirectory(string? cwd) { WorkingDirectory = cwd; return this; }

    protected override IEnumerable<string> BuildVerbArguments()
    {
        yield return "pull";
        if (AllTags) yield return "--all-tags";
        if (Quiet) yield return "--quiet";
        if (DisableContentTrust) yield return "--disable-content-trust";
        if (!string.IsNullOrEmpty(Platform)) { yield return "--platform"; yield return Platform!; }
        if (string.IsNullOrEmpty(Image))
            throw new InvalidOperationException("Docker.Pull requires Image.");
        yield return Image!;
    }
}
