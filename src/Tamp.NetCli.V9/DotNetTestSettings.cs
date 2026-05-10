namespace Tamp.NetCli.V9;

public sealed class DotNetTestSettings : DotNetSettingsBase
{
    public Configuration? Configuration { get; set; }
    public bool NoBuild { get; set; }
    public bool NoRestore { get; set; }
    public string? Filter { get; set; }
    public List<string> Loggers { get; } = [];
    public List<string> DataCollectors { get; } = [];
    public string? ResultsDirectory { get; set; }
    public string? Settings { get; set; }
    public string? Runtime { get; set; }
    public string? Framework { get; set; }
    public bool BlameHang { get; set; }
    public TimeSpan? BlameHangTimeout { get; set; }
    public Dictionary<string, string> Properties { get; } = new();

    public DotNetTestSettings SetProject(string? project) { Project = project; return this; }
    public DotNetTestSettings SetConfiguration(Configuration c) { Configuration = c; return this; }
    public DotNetTestSettings SetNoBuild(bool v) { NoBuild = v; return this; }
    public DotNetTestSettings SetNoRestore(bool v) { NoRestore = v; return this; }
    public DotNetTestSettings SetFilter(string? filter) { Filter = filter; return this; }
    public DotNetTestSettings AddLogger(string logger) { Loggers.Add(logger); return this; }
    public DotNetTestSettings AddDataCollector(string name) { DataCollectors.Add(name); return this; }
    public DotNetTestSettings SetResultsDirectory(string? path) { ResultsDirectory = path; return this; }
    public DotNetTestSettings SetSettings(string? path) { Settings = path; return this; }
    public DotNetTestSettings SetRuntime(string? runtime) { Runtime = runtime; return this; }
    public DotNetTestSettings SetFramework(string? tfm) { Framework = tfm; return this; }
    public DotNetTestSettings SetBlameHang(bool v) { BlameHang = v; return this; }
    public DotNetTestSettings SetBlameHangTimeout(TimeSpan? t) { BlameHangTimeout = t; return this; }
    public DotNetTestSettings SetProperty(string name, string value) { Properties[name] = value; return this; }
    public DotNetTestSettings SetVerbosity(DotNetVerbosity v) { Verbosity = v; return this; }
    public DotNetTestSettings SetWorkingDirectory(string? cwd) { WorkingDirectory = cwd; return this; }

    protected override IEnumerable<string> BuildVerbArguments()
    {
        yield return "test";
        if (!string.IsNullOrEmpty(Project)) yield return Project!;
        if (ConfigurationToken(Configuration) is { } cfg) { yield return "--configuration"; yield return cfg; }
        if (NoBuild) yield return "--no-build";
        if (NoRestore) yield return "--no-restore";
        if (!string.IsNullOrEmpty(Filter)) { yield return "--filter"; yield return Filter!; }
        foreach (var l in Loggers) { yield return "--logger"; yield return l; }
        foreach (var c in DataCollectors) { yield return "--collect"; yield return c; }
        if (!string.IsNullOrEmpty(ResultsDirectory)) { yield return "--results-directory"; yield return ResultsDirectory!; }
        if (!string.IsNullOrEmpty(Settings)) { yield return "--settings"; yield return Settings!; }
        if (!string.IsNullOrEmpty(Runtime)) { yield return "--runtime"; yield return Runtime!; }
        if (!string.IsNullOrEmpty(Framework)) { yield return "--framework"; yield return Framework!; }
        if (BlameHang) yield return "--blame-hang";
        if (BlameHangTimeout is { } t) { yield return "--blame-hang-timeout"; yield return $"{(int)t.TotalMilliseconds}ms"; }
        foreach (var (k, v) in Properties)
            yield return $"-p:{k}={v}";
    }
}
