using System.Reflection;

namespace Tamp;

/// <summary>
/// Base class for a Tamp build script. Authors derive from this, declare
/// <see cref="Target"/>-typed properties for each target, and call
/// <see cref="Execute{T}"/> from <c>Main</c>.
/// </summary>
/// <remarks>
/// At execution time, the framework reflects over the build class, finds
/// every <see cref="Target"/>-typed property, invokes each property's
/// delegate against a fresh <see cref="ITargetDefinition"/>, and assembles
/// the resulting <see cref="TargetSpec"/>s into the build's target graph.
/// </remarks>
public abstract partial class TampBuild
{
    /// <summary>
    /// Whether the current build invocation is local (developer machine)
    /// rather than CI. Build scripts use this for picking sensible defaults
    /// — e.g., Debug locally, Release in CI.
    /// </summary>
    protected static bool IsLocalBuild => HostProfileBuilder.Build().Ci is null;

    /// <summary>
    /// Whether the current build invocation is in a known CI environment.
    /// </summary>
    protected static bool IsServerBuild => !IsLocalBuild;

    private static AbsolutePath? _rootDirectoryCache;

    /// <summary>
    /// The root of the consumer's repository. Computed by walking up from
    /// the build assembly's location and stopping at the first directory
    /// containing any of: <c>.git</c>, a <c>.slnx</c> or <c>.sln</c> file,
    /// or a <c>.tamp</c> subdirectory. Cached after first access.
    /// </summary>
    public static AbsolutePath RootDirectory
    {
        get
        {
            if (_rootDirectoryCache is not null) return _rootDirectoryCache;
            var found = LocateRootDirectory(AppContext.BaseDirectory);
            if (found is null)
                throw new InvalidOperationException(
                    $"Could not locate repository root — no .git, .slnx/.sln, or .tamp directory found above '{AppContext.BaseDirectory}'.");
            return _rootDirectoryCache = AbsolutePath.Create(found);
        }
    }

    /// <summary>
    /// Tamp's per-build scratch directory. Lives at
    /// <c>RootDirectory / ".tamp" / "temp"</c>; created on first access.
    /// </summary>
    public static AbsolutePath TemporaryDirectory => (RootDirectory / ".tamp" / "temp").EnsureDirectoryExists();

    private static CiHost? _ciHostCache;
    private static bool _ciHostResolved;

    /// <summary>
    /// The active CI host (GitHub Actions, Azure DevOps, TeamCity, …) when
    /// running under a recognised CI environment, or null otherwise.
    /// Cached after first access; build scripts on a developer machine see
    /// null and can branch on that.
    /// </summary>
    public static CiHost? CiHost
    {
        get
        {
            if (_ciHostResolved) return _ciHostCache;
            _ciHostCache = Tamp.CiHost.Detect();
            _ciHostResolved = true;
            return _ciHostCache;
        }
    }

    /// <summary>Reset the cached <see cref="RootDirectory"/>. Test-only.</summary>
    internal static void ResetCachedDirectories()
    {
        _rootDirectoryCache = null;
        _ciHostCache = null;
        _ciHostResolved = false;
    }

    private static string? LocateRootDirectory(string startDirectory)
    {
        var current = new DirectoryInfo(startDirectory);
        while (current is not null)
        {
            if (Directory.Exists(Path.Combine(current.FullName, ".git"))) return current.FullName;
            if (Directory.Exists(Path.Combine(current.FullName, ".tamp"))) return current.FullName;
            if (Directory.GetFiles(current.FullName, "*.slnx", SearchOption.TopDirectoryOnly).Length > 0)
                return current.FullName;
            if (Directory.GetFiles(current.FullName, "*.sln", SearchOption.TopDirectoryOnly).Length > 0)
                return current.FullName;
            current = current.Parent;
        }
        return null;
    }

    /// <summary>
    /// Print the framework banner (ASCII logo + version + URL) plus a
    /// host-info panel (OS, arch, CPU/memory, runtime, CI vendor, cgroup).
    /// Always shown, regardless of verbosity — useful for after-the-fact
    /// debugging of runner-specific build issues where the runner config
    /// isn't otherwise visible in the log.
    /// </summary>
    public static void PrintBanner(TextWriter writer)
    {
        // Prefer the InformationalVersion (carries pre-release suffixes like
        // "0.0.1-alpha") over Assembly.Version (which is a 3-part numeric
        // and drops everything after the patch).
        var asm = typeof(TampBuild).Assembly;
        var infoVersion = asm.GetCustomAttribute<System.Reflection.AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        // SourceLink appends "+<commit>" to InformationalVersion; trim that
        // for display since the host panel below shows commit info already.
        if (infoVersion is not null)
        {
            var plus = infoVersion.IndexOf('+');
            if (plus > 0) infoVersion = infoVersion[..plus];
        }
        var version = infoVersion ?? asm.GetName().Version?.ToString(3) ?? "0.0.0";
        writer.WriteLine();
        writer.WriteLine(@"  ████████╗ █████╗ ███╗   ███╗██████╗ ");
        writer.WriteLine(@"  ╚══██╔══╝██╔══██╗████╗ ████║██╔══██╗");
        writer.WriteLine(@"     ██║   ███████║██╔████╔██║██████╔╝");
        writer.WriteLine(@"     ██║   ██╔══██║██║╚██╔╝██║██╔═══╝ ");
        writer.WriteLine(@"     ██║   ██║  ██║██║ ╚═╝ ██║██║     ");
        writer.WriteLine(@"     ╚═╝   ╚═╝  ╚═╝╚═╝     ╚═╝╚═╝     ");
        writer.WriteLine();
        writer.WriteLine($"  Tamp {version}  ·  https://github.com/tamp-build/tamp");
        writer.WriteLine();

        var host = HostProfileBuilder.Build();
        var ci = Tamp.CiHost.Detect();

        var os = host.Os switch
        {
            OSFamily.Windows => "Windows",
            OSFamily.Linux => host.InWsl ? "Linux (WSL)" : "Linux",
            OSFamily.MacOs => "macOS",
            _ => "Unknown",
        };
        var arch = host.Arch switch
        {
            System.Runtime.InteropServices.Architecture.X64 => "x64",
            System.Runtime.InteropServices.Architecture.Arm64 => "arm64",
            System.Runtime.InteropServices.Architecture.X86 => "x86",
            _ => host.Arch.ToString().ToLowerInvariant(),
        };
        var totalGb = host.TotalMemoryBytes / 1024.0 / 1024.0 / 1024.0;
        var freeGb = host.AvailableMemoryBytes / 1024.0 / 1024.0 / 1024.0;
        var runtime = $".NET {Environment.Version}";
        var runner = ci is not null
            ? FormatCiVendor(ci.Vendor)
            : (host.InContainer ? "container (local)" : "local");

        writer.WriteLine($"  Host:     {os} {arch}  ·  {host.LogicalCpuCount} core{(host.LogicalCpuCount == 1 ? "" : "s")}  ·  {totalGb:F1} GB total / {freeGb:F1} GB free");
        writer.WriteLine($"  Runtime:  {runtime}  ·  Runner: {runner}");

        if (host.Cgroup is { } cg)
        {
            var memLimit = cg.MemoryLimitBytes is { } mb
                ? $"{mb / 1024.0 / 1024.0 / 1024.0:F1} GB memory limit"
                : "no memory limit";
            var cpuQuota = cg.CpuQuota is { } cq
                ? $"{cq:F2} cpu quota"
                : "no cpu quota";
            writer.WriteLine($"  Cgroup:   v{cg.Version}  ·  {cpuQuota}  ·  {memLimit}");
        }

        writer.WriteLine();
    }

    private static string FormatCiVendor(CiVendor v) => v switch
    {
        CiVendor.GitHubActions => "GitHub Actions",
        CiVendor.AzureDevOps => "Azure DevOps",
        CiVendor.GitLabCi => "GitLab CI",
        CiVendor.AppVeyor => "AppVeyor",
        CiVendor.TeamCity => "TeamCity",
        CiVendor.Jenkins => "Jenkins",
        CiVendor.CircleCI => "CircleCI",
        CiVendor.Buildkite => "Buildkite",
        CiVendor.Travis => "Travis CI",
        CiVendor.Unknown => "CI (vendor unknown)",
        _ => "local",
    };

    /// <summary>
    /// Emit a CI-vendor-specific masking instruction when a <see cref="Secret"/>
    /// resolves. The instruction tells the CI runner to scrub the value
    /// from subsequent log lines, even ones produced by child processes
    /// the wrapper spawns. Tamp's own <c>RedactingTextWriter</c> handles
    /// in-process scrubbing; this adds vendor-side defense in depth.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    ///   <item><b>GitHub Actions</b> — emits <c>::add-mask::&lt;value&gt;</c>.
    ///         Subsequent occurrences in stdout / stderr are replaced
    ///         with <c>***</c> by the runner.</item>
    ///   <item><b>Azure DevOps</b> — emits
    ///         <c>##vso[task.setvariable variable=...;issecret=true]&lt;value&gt;</c>
    ///         which registers the value with the Azure Pipelines
    ///         secret store + masking.</item>
    ///   <item><b>TeamCity / Jenkins / GitLab / Travis / others</b> —
    ///         no portable equivalent; relies on Tamp's in-process
    ///         redaction only.</item>
    ///   <item><b>Local</b> — no-op; nothing reads the instruction.</item>
    /// </list>
    /// </remarks>
    private static void RegisterSecretForCiMasking(Secret secret)
    {
        var host = HostProfileBuilder.Build();
        if (host.Ci is null) return;

        var value = secret.Reveal();
        switch (host.Ci)
        {
            case CiVendor.GitHubActions:
                Console.WriteLine($"::add-mask::{value}");
                break;
            case CiVendor.AzureDevOps:
                // The variable name is just the secret's label; the
                // important bit is issecret=true which registers the
                // value with the pipeline's masking store.
                Console.WriteLine($"##vso[task.setvariable variable=TAMP_SECRET_{secret.Name};issecret=true]{value}");
                break;
            // Other vendors: no portable instruction. Rely on in-process redaction.
        }
    }

    /// <summary>Top-level build entry point. Pass <c>args</c> from <c>Main</c>.</summary>
    public static int Execute<T>(string[] args) where T : TampBuild, new()
    {
        try
        {
            var build = new T();

            // Parameter binding happens before target discovery so that any
            // [Parameter] reads inside a target's authoring lambda see the
            // resolved values.
            ParameterBinder.Bind(build, args, Environment.GetEnvironmentVariable);

            // Secret binding: env-var leg (TAM-78). The onResolved callback
            // emits CI-vendor masking instructions (e.g. ::add-mask:: on
            // GitHub Actions) so subsequent log lines don't leak the value.
            // EnsureResolved (interactive prompt + future legs) runs later,
            // just before a target that .Requires() the secret executes.
            SecretBinder.Bind(build, Environment.GetEnvironmentVariable, RegisterSecretForCiMasking);

            var targets = CollectTargets(build);
            var graph = new TargetGraph(targets);

            var (mode, targetNames, listMode, showAll, verbosity) = ParseInvocation(args, targets);

            if (listMode is ListMode.Flat)
            {
                PrintTargetList(targets, tree: false, showAll);
                return 0;
            }
            if (listMode is ListMode.Tree)
            {
                PrintTargetList(targets, tree: true, showAll);
                return 0;
            }

            if (targetNames.Count == 0)
            {
                Console.Error.WriteLine("No target specified and no `Default` or `Ci` target found.");
                Console.Error.WriteLine("Use `--list` to see available targets.");
                return 2;
            }

            PrintBanner(Console.Out);

            var executor = new Executor(graph, mode, output: null, verbosity);
            return executor.Run(targetNames.ToArray()).ExitCode;
        }
        catch (InvalidOperationException ex)
        {
            Console.Error.WriteLine($"tamp: {ex.Message}");
            return 1;
        }
    }

    /// <summary>Parse the build invocation: zero-or-more target names plus mode flags.</summary>
    /// <remarks>
    /// All non-flag tokens are target names; the executor runs them as a
    /// deduped invoked set. Flags <c>--dry-run</c>, <c>--plan</c>,
    /// <c>--list</c>, <c>--list-tree</c> control mode. If no targets are
    /// given, the build defaults to a target literally named <c>Default</c>
    /// or <c>Ci</c> if present.
    /// </remarks>
    internal static (ExecutionMode, IReadOnlyList<string>, ListMode, bool ShowAll, LogLevel Verbosity) ParseInvocation(
        string[] args, IReadOnlyDictionary<string, TargetSpec> targets)
    {
        var mode = ExecutionMode.Run;
        var listMode = ListMode.None;
        var showAll = false;
        var verbosity = LogLevel.Info;
        var targetNames = new List<string>();
        var skipNextValue = false;

        for (var i = 0; i < args.Length; i++)
        {
            var raw = args[i];
            if (skipNextValue) { skipNextValue = false; continue; }

            if (raw.StartsWith("--", StringComparison.Ordinal))
            {
                var rest = raw[2..];
                var key = rest;
                string? inlineValue = null;
                var eq = key.IndexOf('=');
                if (eq >= 0) { inlineValue = key[(eq + 1)..]; key = key[..eq]; }
                switch (key)
                {
                    case "dry-run": mode = ExecutionMode.DryRun; break;
                    case "plan": mode = ExecutionMode.Plan; break;
                    case "list": listMode = ListMode.Flat; break;
                    case "list-tree": listMode = ListMode.Tree; break;
                    case "all": showAll = true; break;
                    case "verbosity":
                        var verbValue = inlineValue ?? (i + 1 < args.Length ? args[++i] : null);
                        if (verbValue is not null) verbosity = ParseVerbosity(verbValue);
                        break;
                    case "quiet": verbosity = LogLevel.Error; break;
                    case "verbose": verbosity = LogLevel.Debug; break;
                    case "diagnostic": verbosity = LogLevel.Trace; break;
                    default:
                        // Unknown flag is a parameter binding handled by
                        // ParameterBinder. If the next arg is a value (not
                        // a flag), it's the parameter's value — skip it so
                        // it doesn't get picked up as a target name.
                        if (inlineValue is null && i + 1 < args.Length
                            && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
                            skipNextValue = true;
                        break;
                }
                continue;
            }
            targetNames.Add(raw);
        }

        if (targetNames.Count == 0 && listMode is ListMode.None)
        {
            if (targets.ContainsKey("Default")) targetNames.Add("Default");
            else if (targets.ContainsKey("Ci")) targetNames.Add("Ci");
        }

        return (mode, targetNames, listMode, showAll, verbosity);
    }

    /// <summary>Maps user-facing verbosity strings to internal log levels.</summary>
    internal static LogLevel ParseVerbosity(string value) => value.Trim().ToLowerInvariant() switch
    {
        "quiet" or "q" => LogLevel.Error,
        "minimal" or "m" => LogLevel.Warn,
        "normal" or "n" => LogLevel.Info,
        "verbose" or "v" => LogLevel.Debug,
        "diagnostic" or "d" => LogLevel.Trace,
        _ => throw new InvalidOperationException(
            $"Unknown --verbosity value '{value}'. Use quiet, minimal, normal, verbose, or diagnostic."),
    };

    private static void PrintTargetList(IReadOnlyDictionary<string, TargetSpec> targets, bool tree, bool showAll)
    {
        if (targets.Count == 0)
        {
            Console.WriteLine("(no targets defined)");
            return;
        }

        // If any target is marked TopLevel, the default listing is just
        // those — pass --all to see everything. If none are marked, every
        // target appears (no breaking change for builds that haven't
        // adopted the marker).
        var hasTopLevel = targets.Values.Any(t => t.TopLevel);
        var visible = (hasTopLevel && !showAll)
            ? targets.Values.Where(t => t.TopLevel)
            : targets.Values;

        var sorted = visible.OrderBy(t => t.Name, StringComparer.Ordinal).ToList();
        if (sorted.Count == 0)
        {
            Console.WriteLine("(no top-level targets; pass --all to see internals)");
            return;
        }

        foreach (var t in sorted)
        {
            var phase = t.Phase == Phase.None ? string.Empty : $" [{t.Phase}]";
            var desc = string.IsNullOrEmpty(t.Description) ? string.Empty : $"  — {t.Description}";
            var marker = (showAll && hasTopLevel && !t.TopLevel) ? " (internal)" : string.Empty;
            Console.WriteLine($"{t.Name}{phase}{marker}{desc}");
            if (tree && t.Dependencies.Count > 0)
                foreach (var dep in t.Dependencies)
                    Console.WriteLine($"    depends on: {dep}");
        }

        if (hasTopLevel && !showAll)
        {
            var hidden = targets.Values.Count(t => !t.TopLevel);
            if (hidden > 0)
            {
                Console.WriteLine();
                Console.WriteLine($"({hidden} internal target{(hidden == 1 ? "" : "s")} hidden; pass --all to show)");
            }
        }
    }

    internal enum ListMode { None, Flat, Tree }

    /// <summary>
    /// Reflect over <paramref name="build"/>, materialise every
    /// <see cref="Target"/>-typed property into a frozen
    /// <see cref="TargetSpec"/>, and return them keyed by name.
    /// </summary>
    internal static IReadOnlyDictionary<string, TargetSpec> CollectTargets(TampBuild build)
    {
        if (build is null) throw new ArgumentNullException(nameof(build));

        var result = new Dictionary<string, TargetSpec>(StringComparer.Ordinal);
        var type = build.GetType();

        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        foreach (var prop in type.GetProperties(flags))
        {
            if (prop.PropertyType != typeof(Target)) continue;
            if (prop.GetIndexParameters().Length > 0) continue;

            var del = (Target?)prop.GetValue(build);
            if (del is null)
                throw new InvalidOperationException(
                    $"Target property '{type.FullName}.{prop.Name}' returned null.");

            var def = new TargetDefinition();
            del(def);
            var spec = def.Build(prop.Name);
            if (result.ContainsKey(spec.Name))
                throw new InvalidOperationException(
                    $"Duplicate target name '{spec.Name}' in {type.FullName}.");
            result[spec.Name] = spec;
        }

        return result;
    }
}
