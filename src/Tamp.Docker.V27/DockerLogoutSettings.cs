namespace Tamp.Docker.V27;

public sealed class DockerLogoutSettings : DockerSettingsBase
{
    public string? Server { get; set; }

    public DockerLogoutSettings SetServer(string? server) { Server = server; return this; }
    public DockerLogoutSettings SetWorkingDirectory(string? cwd) { WorkingDirectory = cwd; return this; }

    protected override IEnumerable<string> BuildVerbArguments()
    {
        yield return "logout";
        if (!string.IsNullOrEmpty(Server)) yield return Server!;
    }
}
