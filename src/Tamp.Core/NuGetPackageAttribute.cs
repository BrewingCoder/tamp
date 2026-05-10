using System.Reflection;
using System.Runtime.InteropServices;

namespace Tamp;

/// <summary>
/// Auto-resolve a .NET tool from NuGet. On first use, the tool is
/// installed via <c>dotnet tool install --tool-path</c> into a Tamp-
/// managed cache under <see cref="TampBuild.TemporaryDirectory"/>;
/// subsequent runs reuse the cache.
/// </summary>
/// <remarks>
/// <para>
/// Decorated property must be of type <see cref="Tool"/>. The cache key
/// is <c>{packageId}-{version}</c>; bumping <see cref="Version"/> will
/// install a fresh copy alongside any existing one.
/// </para>
/// <para>
/// Requires network access on the first install. For locked-down
/// environments where <c>dotnet tool install</c> can't reach
/// nuget.org, set <see cref="LocalCachePath"/> to point at a pre-
/// populated tools directory and the attribute will skip the install
/// and just resolve from there.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class NuGetPackageAttribute : ValueInjectionAttribute
{
    public NuGetPackageAttribute(string packageId)
    {
        PackageId = packageId ?? throw new ArgumentNullException(nameof(packageId));
    }

    public string PackageId { get; }

    /// <summary>NuGet version. If null, installs the latest stable version.</summary>
    public string? Version { get; init; }

    /// <summary>
    /// Override the executable name to look up. Defaults to <see cref="PackageId"/>
    /// (which matches the typical .NET tool naming convention). Set if the
    /// installed binary differs from the package id.
    /// </summary>
    public string? ExecutableName { get; init; }

    /// <summary>
    /// Skip the install step and resolve directly from this directory.
    /// Useful for offline / locked-down environments where the tool was
    /// pre-staged.
    /// </summary>
    public string? LocalCachePath { get; init; }

    /// <summary>
    /// Skip the install step entirely and treat the property as already
    /// resolved by the system PATH. Set when the tool is expected to be
    /// system-installed.
    /// </summary>
    public bool UseSystemPath { get; init; }

    public override object? GetValue(MemberInfo member, Type memberType)
    {
        if (memberType != typeof(Tool))
            throw new InvalidOperationException(
                $"[NuGetPackage] requires a member of type Tamp.Tool; '{member.Name}' is {memberType.Name}.");

        if (UseSystemPath)
            return new Tool(ResolveOnPath(ExecutableName ?? PackageId));

        var executableName = ExecutableName ?? PackageId;
        if (!string.IsNullOrEmpty(LocalCachePath))
        {
            var custom = AbsolutePath.Create(LocalCachePath!);
            return new Tool(LocateExecutable(custom, executableName));
        }

        var cache = TampBuild.TemporaryDirectory / "nuget-tools" / $"{PackageId}-{Version ?? "latest"}";
        if (!cache.DirectoryExists())
        {
            cache.EnsureDirectoryExists();
            InstallTool(cache);
        }
        return new Tool(LocateExecutable(cache, executableName));
    }

    private void InstallTool(AbsolutePath cache)
    {
        var args = new List<string> { "tool", "install", "--tool-path", cache.Value };
        if (!string.IsNullOrEmpty(Version)) { args.Add("--version"); args.Add(Version!); }
        args.Add(PackageId);

        var plan = new CommandPlan
        {
            Executable = "dotnet",
            Arguments = args,
        };
        var result = ProcessRunner.Capture(plan);
        if (result.Failed)
            throw new InvalidOperationException(
                $"`dotnet tool install` failed for {PackageId}{(Version is null ? "" : $"@{Version}")} (exit {result.ExitCode}). " +
                $"Output: {(result.StderrText.Length > 0 ? result.StderrText : result.StdoutText)}");
    }

    private static AbsolutePath LocateExecutable(AbsolutePath dir, string name)
    {
        // dotnet tools install into <tool-path>/<name>(.exe on Windows)
        // and also leave a wrapper script. Prefer the executable form.
        var ext = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ".exe" : string.Empty;
        var candidate = dir / (name + ext);
        if (candidate.FileExists()) return candidate;

        // Fallback: search the directory for any executable matching the name (case-insensitive).
        if (dir.DirectoryExists())
        {
            foreach (var f in dir.EnumerateFiles())
                if (string.Equals(f.NameWithoutExtension, name, StringComparison.OrdinalIgnoreCase))
                    return f;
        }

        throw new InvalidOperationException(
            $"Could not locate executable '{name}' in {dir}. Set ExecutableName explicitly if the tool's binary name differs from the package id.");
    }

    private static AbsolutePath ResolveOnPath(string name)
    {
        var pathVar = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(pathVar))
            throw new InvalidOperationException("PATH environment variable is empty; cannot resolve tool from system PATH.");

        var separator = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ';' : ':';
        var ext = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ".exe" : string.Empty;

        foreach (var dir in pathVar.Split(separator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                var candidate = AbsolutePath.Create(Path.Combine(dir, name + ext));
                if (candidate.FileExists()) return candidate;
                // Also try without extension on Windows in case the user named it explicitly.
                if (!string.IsNullOrEmpty(ext))
                {
                    var noExt = AbsolutePath.Create(Path.Combine(dir, name));
                    if (noExt.FileExists()) return noExt;
                }
            }
            catch
            {
                // Skip path entries that fail to normalise (rare; bad PATH).
            }
        }

        throw new InvalidOperationException($"'{name}' not found on system PATH.");
    }
}
