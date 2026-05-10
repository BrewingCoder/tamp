namespace Tamp.Docker.V27;

public sealed class DockerBuildSettings : DockerSettingsBase
{
    /// <summary>The build context path (positional). Defaults to current directory.</summary>
    public string? Context { get; set; }
    public string? Dockerfile { get; set; }
    public List<string> Tags { get; } = [];
    public Dictionary<string, string> BuildArgs { get; } = new();
    public Dictionary<string, string> Labels { get; } = new();
    public string? Target { get; set; }
    public string? Platform { get; set; }
    public bool NoCache { get; set; }
    public bool Pull { get; set; }
    public bool Quiet { get; set; }
    public string? Network { get; set; }
    public string? OutputType { get; set; }
    public string? Progress { get; set; }

    public DockerBuildSettings SetContext(string? path) { Context = path; return this; }
    public DockerBuildSettings SetDockerfile(string? path) { Dockerfile = path; return this; }
    public DockerBuildSettings AddTag(string tag) { Tags.Add(tag); return this; }
    public DockerBuildSettings SetBuildArg(string name, string value) { BuildArgs[name] = value; return this; }
    public DockerBuildSettings SetLabel(string name, string value) { Labels[name] = value; return this; }
    public DockerBuildSettings SetTarget(string? target) { Target = target; return this; }
    public DockerBuildSettings SetPlatform(string? platform) { Platform = platform; return this; }
    public DockerBuildSettings SetNoCache(bool v) { NoCache = v; return this; }
    public DockerBuildSettings SetPull(bool v) { Pull = v; return this; }
    public DockerBuildSettings SetQuiet(bool v) { Quiet = v; return this; }
    public DockerBuildSettings SetNetwork(string? network) { Network = network; return this; }
    public DockerBuildSettings SetOutputType(string? type) { OutputType = type; return this; }
    public DockerBuildSettings SetProgress(string? progress) { Progress = progress; return this; }
    public DockerBuildSettings SetWorkingDirectory(string? cwd) { WorkingDirectory = cwd; return this; }

    protected override IEnumerable<string> BuildVerbArguments()
    {
        yield return "build";
        if (!string.IsNullOrEmpty(Dockerfile)) { yield return "--file"; yield return Dockerfile!; }
        foreach (var t in Tags) { yield return "--tag"; yield return t; }
        foreach (var (k, v) in BuildArgs) { yield return "--build-arg"; yield return $"{k}={v}"; }
        foreach (var (k, v) in Labels) { yield return "--label"; yield return $"{k}={v}"; }
        if (!string.IsNullOrEmpty(Target)) { yield return "--target"; yield return Target!; }
        if (!string.IsNullOrEmpty(Platform)) { yield return "--platform"; yield return Platform!; }
        if (NoCache) yield return "--no-cache";
        if (Pull) yield return "--pull";
        if (Quiet) yield return "--quiet";
        if (!string.IsNullOrEmpty(Network)) { yield return "--network"; yield return Network!; }
        if (!string.IsNullOrEmpty(OutputType)) { yield return "--output"; yield return OutputType!; }
        if (!string.IsNullOrEmpty(Progress)) { yield return "--progress"; yield return Progress!; }
        yield return Context ?? ".";
    }
}
