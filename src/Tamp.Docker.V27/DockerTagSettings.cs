namespace Tamp.Docker.V27;

public sealed class DockerTagSettings : DockerSettingsBase
{
    public string? SourceImage { get; set; }
    public string? TargetImage { get; set; }

    public DockerTagSettings SetSource(string source) { SourceImage = source; return this; }
    public DockerTagSettings SetTarget(string target) { TargetImage = target; return this; }
    public DockerTagSettings SetWorkingDirectory(string? cwd) { WorkingDirectory = cwd; return this; }

    protected override IEnumerable<string> BuildVerbArguments()
    {
        yield return "tag";
        if (string.IsNullOrEmpty(SourceImage))
            throw new InvalidOperationException("Docker.Tag requires SourceImage.");
        if (string.IsNullOrEmpty(TargetImage))
            throw new InvalidOperationException("Docker.Tag requires TargetImage.");
        yield return SourceImage!;
        yield return TargetImage!;
    }
}
