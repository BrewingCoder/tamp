namespace Tamp.Cli;

/// <summary>
/// Walks up from a starting directory looking for a Tamp build project.
/// Honors the conventional layouts described in ADR 0006 (build script
/// lives at <c>build/Build.csproj</c>) but is permissive about exact name.
/// </summary>
internal static class BuildProjectLocator
{
    /// <summary>Folder names searched at each level.</summary>
    private static readonly string[] BuildFolderNames = ["build", "_build", ".tamp/build"];

    /// <summary>csproj filenames searched within each candidate folder.</summary>
    private static readonly string[] BuildCsprojNames =
        ["Build.csproj", "_build.csproj", "build.csproj"];

    /// <summary>
    /// Search <paramref name="startDirectory"/> and each parent for a build
    /// project. Returns the resolved csproj path or null if none is found.
    /// </summary>
    public static string? Locate(string startDirectory)
    {
        var current = new DirectoryInfo(startDirectory);
        while (current is not null)
        {
            foreach (var folder in BuildFolderNames)
            {
                var dir = Path.Combine(current.FullName, folder);
                if (!Directory.Exists(dir)) continue;

                // 1. Conventional name match.
                foreach (var name in BuildCsprojNames)
                {
                    var path = Path.Combine(dir, name);
                    if (File.Exists(path)) return path;
                }

                // 2. Single .csproj fallback — if there's exactly one
                //    csproj in the build folder, use it regardless of
                //    name. Avoids being too prescriptive.
                var csprojs = Directory.GetFiles(dir, "*.csproj", SearchOption.TopDirectoryOnly);
                if (csprojs.Length == 1) return csprojs[0];
            }
            current = current.Parent;
        }
        return null;
    }
}
