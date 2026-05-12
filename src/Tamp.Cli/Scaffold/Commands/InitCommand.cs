using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Tamp.Cli.Scaffold.Probes;
using Tamp.Cli.Scaffold.Sources;
using Tamp.Scaffold;

namespace Tamp.Cli.Scaffold.Commands;

/// <summary>
/// Handles <c>tamp init</c>. v0.1.0 ships exactly one template
/// (<see cref="Templates.MinimalTemplate"/>) via the embedded source.
/// </summary>
public static class InitCommand
{
    public const int ExitOk = 0;
    public const int ExitUsage = 64;
    public const int ExitFileExists = 73;
    public const int ExitTemplateNotFound = 78;
    public const int ExitDriftMismatch = 79;
    public const int ExitNotImplemented = 80;

    /// <summary>
    /// Parse <c>tamp init ...</c> args and run. Returns the process exit code.
    /// </summary>
    public static int Run(string[] args, TextWriter? stdout = null, TextWriter? stderr = null)
    {
        stdout ??= Console.Out;
        stderr ??= Console.Error;

        var opts = ParseArgs(args, out var parseError);
        if (parseError is not null)
        {
            stderr.WriteLine($"tamp init: {parseError}");
            stderr.WriteLine();
            PrintHelp(stderr);
            return ExitUsage;
        }

        if (opts.Help) { PrintHelp(stdout); return ExitOk; }

        return RunAsync(opts, stdout, stderr, CancellationToken.None).GetAwaiter().GetResult();
    }

    private static async Task<int> RunAsync(InitOptions opts, TextWriter stdout, TextWriter stderr, CancellationToken ct)
    {
        var repoRoot = AbsolutePath.Create(opts.WorkingDirectory ?? Environment.CurrentDirectory);

        // Sources in priority order: embedded first (offline guarantee), then NuGet.
        // v0.1.0 only registers embedded; the NuGet slot is reserved.
        var sources = new IScaffoldTemplateSource[] { new EmbeddedTemplateSource() };

        if (opts.ListTemplates)
        {
            await ListTemplatesAsync(sources, stdout, ct);
            return ExitOk;
        }

        // Reserved flags — fail with a clean message in v0.1.0.
        if (opts.UnsupportedFlag is { } flag)
        {
            stderr.WriteLine($"tamp init: '{flag.Flag}' lands in {flag.Version}; not available in this CLI. " +
                             "Use `tamp init` (with no template flag) for the minimal scaffold.");
            return ExitNotImplemented;
        }

        // Probe.
        var probes = new IRepoProbe[] { new DotnetSolutionProbe() };
        var ctxBuilder = new ScaffoldContextBuilder
        {
            RepoRoot = repoRoot,
            TampCoreVersion = ResolveCliVersion(),
        };
        ctxBuilder.DotnetToolsJsonExists = File.Exists(Path.Combine(repoRoot.Value, ".config", "dotnet-tools.json"));
        ctxBuilder.BuildCsAlreadyPresent = File.Exists(Path.Combine(repoRoot.Value, "build", "Build.cs"));
        if (opts.SolutionOverride is not null) ctxBuilder.Solution = opts.SolutionOverride;
        else foreach (var p in probes) p.Probe(repoRoot, ctxBuilder);
        var ctx = ctxBuilder.Build();

        // Pre-flight refusal: if Build.cs already exists, no flags can override in v0.1.0.
        if (ctx.BuildCsAlreadyPresent)
        {
            stderr.WriteLine($"tamp init: build/Build.cs already exists at '{ctx.RepoRoot.Value}'. Refusing to overwrite. " +
                             "(--force lands in 0.2.0.)");
            return ExitFileExists;
        }

        // Resolve template.
        const string defaultTemplateName = "minimal";
        IScaffoldTemplate? template = null;
        foreach (var src in sources)
        {
            template = await src.ResolveAsync(defaultTemplateName, ct);
            if (template is not null) break;
        }
        if (template is null)
        {
            stderr.WriteLine($"tamp init: no template named '{defaultTemplateName}' registered. (This is a bug in this CLI build.)");
            return ExitTemplateNotFound;
        }

        // Drift check.
        if (!TampCoreVersionIsAtLeast(ctx.TampCoreVersion, template.MinimumTampCoreVersion))
        {
            stderr.WriteLine(
                $"tamp init: template '{template.Name}' requires Tamp.Core >= {template.MinimumTampCoreVersion}; " +
                $"this CLI ships {ctx.TampCoreVersion}. " +
                "Upgrade: `dotnet tool update -g dotnet-tamp`.");
            return ExitDriftMismatch;
        }

        // Report probe diagnostics (informational; doesn't fail the run).
        if (ctx.ProbeData.TryGetValue("dotnet.solution.detection", out var msg))
            stdout.WriteLine($"  note: solution probe — {msg}; generated Build.cs will use [Solution] auto-discovery.");

        // Render + run.
        var specs = template.Render(ctx).ToList();
        var runner = new ScaffoldRunner(dryRun: opts.DryRun);
        IReadOnlyList<FileWriteResult> results;
        try
        {
            results = runner.Run(specs);
        }
        catch (IOException ex)
        {
            stderr.WriteLine($"tamp init: {ex.Message}");
            return ExitFileExists;
        }

        // Print outcome.
        foreach (var r in results)
        {
            var verb = r.Outcome switch
            {
                FileWriteOutcome.Written => "  wrote   ",
                FileWriteOutcome.Skipped => "  skipped ",
                FileWriteOutcome.Planned => "  would-write ",
                _ => "  ?       ",
            };
            stdout.WriteLine(verb + Relative(repoRoot, r.Path));
        }

        if (opts.DryRun)
        {
            stdout.WriteLine();
            stdout.WriteLine("(dry-run; nothing was written.)");
        }
        else
        {
            stdout.WriteLine();
            stdout.WriteLine("Next steps:");
            if (!ctx.DotnetToolsJsonExists)
                stdout.WriteLine("  dotnet tool restore         # pulls dotnet-tamp into the local tool manifest");
            stdout.WriteLine("  dotnet tamp Test            # run the scaffolded Test target");
            stdout.WriteLine();
            stdout.WriteLine("Docs: https://github.com/tamp-build/tamp/wiki/Getting-Started");
        }

        return ExitOk;
    }

    private static async Task ListTemplatesAsync(IReadOnlyList<IScaffoldTemplateSource> sources, TextWriter stdout, CancellationToken ct)
    {
        stdout.WriteLine("Available templates:");
        foreach (var src in sources)
        {
            IReadOnlyList<IScaffoldTemplate> templates;
            try { templates = await src.ListAsync(ct); }
            catch (System.NotImplementedException) { continue; }            // NuGet source stub; skip silently in v0.1.0
            foreach (var t in templates)
                stdout.WriteLine($"  {t.Name,-16} ({src.Source})  {t.Description}");
        }
    }

    internal static InitOptions ParseArgs(string[] args, out string? error)
    {
        error = null;
        var opts = new InitOptions();
        for (var i = 0; i < args.Length; i++)
        {
            var a = args[i];
            switch (a)
            {
                case "-h": case "--help": opts.Help = true; break;
                case "--dry-run": opts.DryRun = true; break;
                case "--list-templates": opts.ListTemplates = true; break;
                case "--solution":
                    if (++i >= args.Length) { error = "--solution requires a path."; return opts; }
                    opts.SolutionOverride = AbsolutePath.Create(Path.GetFullPath(args[i]));
                    break;
                case "--template":
                    opts.UnsupportedFlag = (a, "0.2.0"); if (++i < args.Length) { /* consume value */ }
                    break;
                case "--template-source":
                    opts.UnsupportedFlag = (a, "0.2.0"); if (++i < args.Length) { /* consume value */ }
                    break;
                case "--offline":
                    opts.UnsupportedFlag = (a, "0.2.0"); break;
                case "--force":
                    opts.UnsupportedFlag = (a, "0.2.0"); break;
                case "--with-ci":
                    opts.UnsupportedFlag = (a, "0.3.0"); if (++i < args.Length) { /* consume value */ }
                    break;
                case "--interactive":
                    opts.UnsupportedFlag = (a, "0.4.0"); break;
                default:
                    if (a.StartsWith("--", System.StringComparison.Ordinal))
                    { error = $"unknown flag '{a}'"; return opts; }
                    if (opts.WorkingDirectory is not null)
                    { error = $"unexpected positional arg '{a}' (already have working directory)"; return opts; }
                    opts.WorkingDirectory = a;
                    break;
            }
        }
        return opts;
    }

    /// <summary>
    /// The CLI's own assembly version becomes the Tamp.Core / Tamp.NetCli.V10
    /// pin in the generated Build.csproj. Single source of truth — no chance
    /// of the CLI shipping templates that point at a Core version different
    /// from the one shipping in the same release.
    /// </summary>
    internal static string ResolveCliVersion()
    {
        var v = typeof(InitCommand).Assembly.GetName().Version;
        if (v is null) return "1.4.0";  // fallback; should never hit in a packed build
        return $"{v.Major}.{v.Minor}.{v.Build}";
    }

    internal static bool TampCoreVersionIsAtLeast(string have, string require)
    {
        if (!System.Version.TryParse(NormalizeSemver(have), out var h)) return true;       // unparseable → be permissive
        if (!System.Version.TryParse(NormalizeSemver(require), out var r)) return true;
        return h >= r;
    }

    private static string NormalizeSemver(string s)
    {
        // System.Version.TryParse handles X.Y / X.Y.Z / X.Y.Z.W but trips on prerelease
        // suffixes ("1.4.0-alpha.1"). Strip the suffix and rely on a coarse compare —
        // good enough for drift gating; can tighten in 0.2.0 if it pulls weight.
        var dash = s.IndexOf('-', System.StringComparison.Ordinal);
        return dash < 0 ? s : s[..dash];
    }

    private static string Relative(AbsolutePath root, AbsolutePath p)
    {
        var r = root.Value.TrimEnd(Path.DirectorySeparatorChar);
        return p.Value.StartsWith(r, System.StringComparison.Ordinal)
            ? p.Value[(r.Length + 1)..]
            : p.Value;
    }

    private static void PrintHelp(TextWriter w)
    {
        w.WriteLine("tamp init — scaffold a Tamp build script into the current directory");
        w.WriteLine();
        w.WriteLine("USAGE:");
        w.WriteLine("  tamp init [--solution <path>] [--dry-run] [--list-templates]");
        w.WriteLine();
        w.WriteLine("WHAT IT WRITES (minimal template):");
        w.WriteLine("  build/Build.cs               minimal build script");
        w.WriteLine("  build/Build.csproj           pins Tamp.Core + Tamp.NetCli.V10");
        w.WriteLine("  .config/dotnet-tools.json    registers dotnet-tamp as a local tool (if absent)");
        w.WriteLine();
        w.WriteLine("FLAGS:");
        w.WriteLine("  --solution <path>     Explicit .NET solution path (otherwise auto-detected)");
        w.WriteLine("  --dry-run             Print would-write list; touch nothing");
        w.WriteLine("  --list-templates      List templates from all registered sources");
        w.WriteLine("  -h | --help           This message");
        w.WriteLine();
        w.WriteLine("RESERVED (parsed; not implemented):");
        w.WriteLine("  --template <name>          (0.2.0)  Pick a non-minimal template");
        w.WriteLine("  --template-source <pkg>    (0.2.0)  Pin a specific NuGet template package");
        w.WriteLine("  --offline                  (0.2.0)  Refuse network fallback");
        w.WriteLine("  --force                    (0.2.0)  Overwrite existing files");
        w.WriteLine("  --with-ci <vendor>         (0.3.0)  Emit CI workflow files");
        w.WriteLine("  --interactive              (0.4.0)  Prompt for choices");
    }

    /// <summary>Parsed flag state.</summary>
    internal sealed class InitOptions
    {
        public bool Help;
        public bool DryRun;
        public bool ListTemplates;
        public string? WorkingDirectory;
        public AbsolutePath? SolutionOverride;
        public (string Flag, string Version)? UnsupportedFlag;
    }
}
