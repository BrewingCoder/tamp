namespace Tamp.Docker.V27;

/// <summary>
/// <c>docker login</c>. Always uses <c>--password-stdin</c> when a Secret
/// password is supplied, so the password value never appears in the OS
/// process table — the wrapper feeds it through stdin and the redaction
/// table catches it on any logged output.
/// </summary>
public sealed class DockerLoginSettings : DockerSettingsBase
{
    public string? Server { get; set; }
    public string? Username { get; set; }
    public Secret? Password { get; set; }

    public DockerLoginSettings SetServer(string? server) { Server = server; return this; }
    public DockerLoginSettings SetUsername(string? username) { Username = username; return this; }
    public DockerLoginSettings SetPassword(Secret password) { Password = password; return this; }
    public DockerLoginSettings SetWorkingDirectory(string? cwd) { WorkingDirectory = cwd; return this; }

    protected override IEnumerable<string> BuildVerbArguments()
    {
        yield return "login";
        if (!string.IsNullOrEmpty(Username)) { yield return "--username"; yield return Username!; }
        if (Password is not null) yield return "--password-stdin";
        if (!string.IsNullOrEmpty(Server)) yield return Server!;
    }

    protected override string? BuildStandardInput()
        => Password is { } s ? s.Reveal() : null;

    protected override IReadOnlyList<Secret> BuildSecrets()
        => Password is { } s ? new[] { s } : Array.Empty<Secret>();
}
