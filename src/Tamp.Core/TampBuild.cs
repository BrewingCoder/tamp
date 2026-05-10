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
public abstract class TampBuild
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

            var targets = CollectTargets(build);
            var graph = new TargetGraph(targets);

            var (mode, targetName, listMode) = ParseInvocation(args, targets);

            if (listMode is ListMode.Flat)
            {
                PrintTargetList(targets, tree: false);
                return 0;
            }
            if (listMode is ListMode.Tree)
            {
                PrintTargetList(targets, tree: true);
                return 0;
            }

            if (targetName is null)
            {
                Console.Error.WriteLine("No target specified and no `Default` or `Ci` target found.");
                Console.Error.WriteLine("Use `--list` to see available targets.");
                return 2;
            }

            var executor = new Executor(graph, mode);
            return executor.Run(targetName).ExitCode;
        }
        catch (InvalidOperationException ex)
        {
            Console.Error.WriteLine($"tamp: {ex.Message}");
            return 1;
        }
    }

    /// <summary>Parse the build invocation: target name plus mode flags.</summary>
    /// <remarks>
    /// First non-flag token is the target name. Flags <c>--dry-run</c>,
    /// <c>--plan</c>, <c>--list</c>, <c>--list-tree</c> control mode. If no
    /// target is given, the build defaults to a target literally named
    /// <c>Default</c> or <c>Ci</c> if present.
    /// </remarks>
    internal static (ExecutionMode, string?, ListMode) ParseInvocation(
        string[] args, IReadOnlyDictionary<string, TargetSpec> targets)
    {
        var mode = ExecutionMode.Run;
        var listMode = ListMode.None;
        string? targetName = null;

        foreach (var raw in args)
        {
            if (raw.StartsWith("--", StringComparison.Ordinal))
            {
                var key = raw[2..];
                var eq = key.IndexOf('=');
                if (eq >= 0) key = key[..eq];
                switch (key)
                {
                    case "dry-run": mode = ExecutionMode.DryRun; break;
                    case "plan": mode = ExecutionMode.Plan; break;
                    case "list": listMode = ListMode.Flat; break;
                    case "list-tree": listMode = ListMode.Tree; break;
                    // Other flags are parameter bindings and handled elsewhere.
                }
                continue;
            }
            if (targetName is null) targetName = raw;
        }

        if (targetName is null && listMode is ListMode.None)
        {
            if (targets.ContainsKey("Default")) targetName = "Default";
            else if (targets.ContainsKey("Ci")) targetName = "Ci";
        }

        return (mode, targetName, listMode);
    }

    private static void PrintTargetList(IReadOnlyDictionary<string, TargetSpec> targets, bool tree)
    {
        if (targets.Count == 0)
        {
            Console.WriteLine("(no targets defined)");
            return;
        }
        var sorted = targets.Values.OrderBy(t => t.Name, StringComparer.Ordinal).ToList();
        foreach (var t in sorted)
        {
            var phase = t.Phase == Phase.None ? string.Empty : $" [{t.Phase}]";
            var desc = string.IsNullOrEmpty(t.Description) ? string.Empty : $"  — {t.Description}";
            Console.WriteLine($"{t.Name}{phase}{desc}");
            if (tree && t.Dependencies.Count > 0)
                foreach (var dep in t.Dependencies)
                    Console.WriteLine($"    depends on: {dep}");
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
