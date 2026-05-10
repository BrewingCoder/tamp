using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Tamp;

/// <summary>
/// Read-only view of a .NET solution file (<c>.slnx</c> or <c>.sln</c>).
/// </summary>
/// <remarks>
/// Surface is deliberately minimal for v1: enumerate projects (with
/// resolved paths), enumerate folders, look up by name. Authoring support
/// (add/remove/save) is not in scope — that belongs to <c>dotnet sln</c>
/// and the IDE.
/// </remarks>
public sealed class Solution
{
    private Solution(AbsolutePath path, string name, IReadOnlyList<SolutionProject> projects, IReadOnlyList<SolutionFolder> folders)
    {
        Path = path;
        Name = name;
        Projects = projects;
        Folders = folders;
    }

    public AbsolutePath Path { get; }
    public string Name { get; }
    public IReadOnlyList<SolutionProject> Projects { get; }
    public IReadOnlyList<SolutionFolder> Folders { get; }

    /// <summary>Find a project by its display name (case-insensitive).</summary>
    public SolutionProject? GetProject(string name)
        => Projects.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));

    /// <summary>Load a solution from its file path. Format detected by extension.</summary>
    public static Solution Load(AbsolutePath path)
    {
        if (!path.FileExists())
            throw new InvalidOperationException($"Solution file not found: {path}");
        return path.Extension.ToLowerInvariant() switch
        {
            ".slnx" => LoadSlnx(path),
            ".sln" => LoadSln(path),
            _ => throw new InvalidOperationException($"Unsupported solution extension '{path.Extension}'. Use .slnx or .sln."),
        };
    }

    private static Solution LoadSlnx(AbsolutePath path)
    {
        var doc = XDocument.Load(path.Value);
        var root = doc.Root ?? throw new InvalidOperationException($"Solution file is empty: {path}");
        var slnDir = path.Parent ?? throw new InvalidOperationException($"Cannot resolve parent of {path}");

        var projects = new List<SolutionProject>();
        var folders = new List<SolutionFolder>();
        WalkSlnx(root, slnDir, currentFolderPath: null, projects, folders);

        return new Solution(path, path.NameWithoutExtension, projects, folders);
    }

    private static void WalkSlnx(XElement element, AbsolutePath slnDir, string? currentFolderPath, List<SolutionProject> projects, List<SolutionFolder> folders)
    {
        foreach (var child in element.Elements())
        {
            switch (child.Name.LocalName)
            {
                case "Project":
                    var projPath = (string?)child.Attribute("Path") ?? string.Empty;
                    if (string.IsNullOrEmpty(projPath)) break;
                    var resolved = AbsolutePath.Create(System.IO.Path.Combine(slnDir.Value, projPath));
                    projects.Add(new SolutionProject(
                        Name: System.IO.Path.GetFileNameWithoutExtension(projPath),
                        Path: resolved,
                        SolutionFolderPath: currentFolderPath));
                    break;

                case "Folder":
                    var folderName = (string?)child.Attribute("Name") ?? "/";
                    folders.Add(new SolutionFolder(folderName));
                    WalkSlnx(child, slnDir, folderName, projects, folders);
                    break;

                default:
                    // Files, properties, configurations etc. — ignored at v1.
                    break;
            }
        }
    }

    private static readonly Regex ProjectLineRegex = new(
        @"^Project\(""\{(?<typeGuid>[^}]+)\}""\)\s*=\s*""(?<name>[^""]+)"",\s*""(?<path>[^""]+)"",\s*""\{(?<projGuid>[^}]+)\}""",
        RegexOptions.Compiled);

    /// <summary>Solution-folder type GUID used by Visual Studio in legacy .sln files.</summary>
    private const string SolutionFolderTypeGuid = "2150E333-8FDC-42A3-9474-1A3956D46DE8";

    private static Solution LoadSln(AbsolutePath path)
    {
        var slnDir = path.Parent ?? throw new InvalidOperationException($"Cannot resolve parent of {path}");
        var projects = new List<SolutionProject>();
        var folders = new List<SolutionFolder>();
        foreach (var raw in path.ReadAllLines())
        {
            var line = raw.TrimStart('﻿', ' ', '\t');
            if (!line.StartsWith("Project(", StringComparison.Ordinal)) continue;
            var m = ProjectLineRegex.Match(line);
            if (!m.Success) continue;

            var name = m.Groups["name"].Value;
            var typeGuid = m.Groups["typeGuid"].Value.ToUpperInvariant();
            var relPath = m.Groups["path"].Value.Replace('\\', System.IO.Path.DirectorySeparatorChar);

            if (string.Equals(typeGuid, SolutionFolderTypeGuid, StringComparison.OrdinalIgnoreCase))
            {
                folders.Add(new SolutionFolder("/" + name + "/"));
                continue;
            }

            var resolved = AbsolutePath.Create(System.IO.Path.Combine(slnDir.Value, relPath));
            projects.Add(new SolutionProject(
                Name: name,
                Path: resolved,
                SolutionFolderPath: null));  // .sln folder→project nesting lives elsewhere; ignore for v1
        }
        return new Solution(path, path.NameWithoutExtension, projects, folders);
    }
}

/// <summary>A single project entry in a solution.</summary>
public sealed record SolutionProject(string Name, AbsolutePath Path, string? SolutionFolderPath);

/// <summary>A single solution folder.</summary>
public sealed record SolutionFolder(string Name);

/// <summary>
/// Auto-inject the build's <see cref="Solution"/> from the discovered
/// solution file. Path resolution: explicit <see cref="Path"/> property
/// (relative to <see cref="TampBuild.RootDirectory"/>) wins; otherwise
/// the first <c>.slnx</c> or <c>.sln</c> at <see cref="TampBuild.RootDirectory"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class SolutionAttribute : ValueInjectionAttribute
{
    /// <summary>Optional explicit path, relative to <see cref="TampBuild.RootDirectory"/>.</summary>
    public string? Path { get; init; }

    public override object? GetValue(System.Reflection.MemberInfo member, Type memberType)
    {
        var path = LocateSolution();
        if (path is null)
            throw new InvalidOperationException(
                $"[Solution] could not locate a solution file at {TampBuild.RootDirectory}. Set Path explicitly or place a .slnx/.sln there.");
        return Solution.Load(path);
    }

    private AbsolutePath? LocateSolution()
    {
        if (!string.IsNullOrEmpty(Path))
        {
            var explicitPath = TampBuild.RootDirectory / Path;
            return explicitPath.FileExists() ? explicitPath : null;
        }
        var slnxFiles = TampBuild.RootDirectory.GlobFiles("*.slnx");
        if (slnxFiles.Count > 0) return slnxFiles[0];
        var slnFiles = TampBuild.RootDirectory.GlobFiles("*.sln");
        return slnFiles.Count > 0 ? slnFiles[0] : null;
    }
}
