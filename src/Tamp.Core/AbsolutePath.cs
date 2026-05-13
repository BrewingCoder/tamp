using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.FileSystemGlobbing;

namespace Tamp;

/// <summary>
/// First-class absolute path. Replaces stringly-typed path manipulation in
/// build scripts. Supports the <c>/</c> operator for combining (mirrors
/// NUKE's idiom) plus the operations a build script actually does:
/// existence checks, read/write, hash, copy/move/delete, and globbing.
/// </summary>
/// <remarks>
/// Construction always normalises to an absolute path. We deliberately do
/// NOT define an implicit conversion <c>string → AbsolutePath</c>: the
/// normalisation step can change the meaning of a relative path silently,
/// so we require <see cref="Create"/> at the boundary. The reverse
/// (<c>AbsolutePath → string</c>) is implicit so any string-taking API
/// works without ceremony.
/// </remarks>
public sealed record AbsolutePath
{
    private AbsolutePath(string normalisedAbsolute) { Value = normalisedAbsolute; }

    /// <summary>The underlying path string. Always absolute and normalised.</summary>
    public string Value { get; }

    /// <summary>
    /// Construct from any path. Relative paths are resolved against the
    /// current working directory at the call site.
    /// </summary>
    public static AbsolutePath Create(string path)
    {
        if (path is null) throw new ArgumentNullException(nameof(path));
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Path must be non-empty.", nameof(path));
        return new AbsolutePath(Path.GetFullPath(path));
    }

    // ---- OS temp-path factories ----

    /// <summary>
    /// The host OS's temp directory root (e.g. <c>/tmp</c>, <c>%TEMP%</c>) as an
    /// <see cref="AbsolutePath"/>. Non-creating; the directory always exists per
    /// OS contract. Useful when an adopter wants to compose temp paths manually
    /// rather than via <see cref="CreateTempDirectory"/>.
    /// </summary>
    public static AbsolutePath GetTempDirectoryRoot() => new(Path.GetFullPath(Path.GetTempPath()));

    /// <summary>
    /// Create a uniquely-named subdirectory under the OS temp root and return its
    /// <see cref="AbsolutePath"/>. The directory exists on disk when this method
    /// returns. The caller is responsible for cleanup; for build scripts use
    /// <see cref="TampBuild.Scratch"/> instead — it tracks the directory and
    /// deletes it at end of build (success or failure).
    /// </summary>
    /// <param name="namePrefix">
    /// Optional prefix for the directory name (final shape: <c>&lt;prefix&gt;-&lt;guid&gt;</c>).
    /// Defaults to <c>tamp</c>. Useful for grepping <c>/tmp</c> during a
    /// post-mortem.
    /// </param>
    public static AbsolutePath CreateTempDirectory(string? namePrefix = null)
    {
        var prefix = string.IsNullOrWhiteSpace(namePrefix) ? "tamp" : namePrefix.Trim();
        var dir = Path.Combine(Path.GetTempPath(), $"{prefix}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        return new AbsolutePath(Path.GetFullPath(dir));
    }

    /// <summary>
    /// Create a uniquely-named empty file under the OS temp root and return its
    /// <see cref="AbsolutePath"/>. The file exists (zero bytes) on disk when
    /// this method returns. The caller is responsible for cleanup.
    /// </summary>
    /// <param name="extension">
    /// Optional file extension. Leading dot is optional: both <c>".pfx"</c> and
    /// <c>"pfx"</c> produce the same result.
    /// </param>
    public static AbsolutePath CreateTempFile(string? extension = null)
    {
        var ext = string.IsNullOrEmpty(extension)
            ? string.Empty
            : (extension.StartsWith('.') ? extension : "." + extension);
        var path = Path.Combine(Path.GetTempPath(), $"tamp-{Guid.NewGuid():N}{ext}");
        File.Create(path).Dispose();
        return new AbsolutePath(Path.GetFullPath(path));
    }

    /// <summary>Implicit string conversion for interop with string-taking APIs.</summary>
    public static implicit operator string(AbsolutePath path) => path.Value;

    /// <summary>
    /// Combine: <c>path / "subdir"</c>. If <paramref name="right"/> is
    /// itself absolute, it replaces (matching <see cref="Path.Combine(string, string)"/>).
    /// </summary>
    public static AbsolutePath operator /(AbsolutePath left, string right)
    {
        if (left is null) throw new ArgumentNullException(nameof(left));
        if (right is null) throw new ArgumentNullException(nameof(right));
        return new AbsolutePath(Path.GetFullPath(Path.Combine(left.Value, right)));
    }

    public override string ToString() => Value;

    // ---- Path components ----

    public AbsolutePath? Parent
    {
        get
        {
            var parent = Path.GetDirectoryName(Value);
            return parent is null ? null : new AbsolutePath(parent);
        }
    }

    public string Name => Path.GetFileName(Value);
    public string NameWithoutExtension => Path.GetFileNameWithoutExtension(Value);
    public string Extension => Path.GetExtension(Value);

    // ---- Existence ----

    public bool FileExists() => File.Exists(Value);
    public bool DirectoryExists() => Directory.Exists(Value);
    public bool Exists() => FileExists() || DirectoryExists();

    // ---- Mutating operations ----

    public AbsolutePath EnsureDirectoryExists()
    {
        Directory.CreateDirectory(Value);
        return this;
    }

    /// <summary>
    /// NUKE-style alias for <see cref="EnsureDirectoryExists"/>. Idempotent —
    /// calling on an already-existing directory is a no-op. Returns this path
    /// for chaining.
    /// </summary>
    public AbsolutePath CreateDirectory() => EnsureDirectoryExists();

    /// <summary>
    /// Ensure the parent directory of this path exists. Returns this path for
    /// chaining. Idiomatic before writing a file whose containing directory
    /// may not exist yet.
    /// </summary>
    public AbsolutePath EnsureParentDirectoryExists()
    {
        Parent?.EnsureDirectoryExists();
        return this;
    }

    /// <summary>
    /// Create an empty file at this path if it doesn't exist; update the last
    /// write time to <see cref="DateTime.UtcNow"/> if it does. Idempotent.
    /// Parent directory is created as needed.
    /// </summary>
    public AbsolutePath Touch()
    {
        Parent?.EnsureDirectoryExists();
        if (FileExists())
            File.SetLastWriteTimeUtc(Value, DateTime.UtcNow);
        else
            File.Create(Value).Dispose();
        return this;
    }

    public AbsolutePath DeleteFile()
    {
        if (FileExists()) File.Delete(Value);
        return this;
    }

    public AbsolutePath DeleteDirectory(bool recursive = true)
    {
        if (DirectoryExists()) Directory.Delete(Value, recursive);
        return this;
    }

    /// <summary>
    /// Delete whatever's at this path — file or directory. If the path
    /// doesn't exist, this is a no-op.
    /// </summary>
    public AbsolutePath Delete()
    {
        if (FileExists()) File.Delete(Value);
        else if (DirectoryExists()) Directory.Delete(Value, recursive: true);
        return this;
    }

    public AbsolutePath CopyTo(AbsolutePath destination, bool overwrite = false)
    {
        if (destination is null) throw new ArgumentNullException(nameof(destination));
        if (DirectoryExists())
        {
            CopyDirectoryRecursive(Value, destination.Value, overwrite);
        }
        else
        {
            destination.Parent?.EnsureDirectoryExists();
            File.Copy(Value, destination.Value, overwrite);
        }
        return destination;
    }

    /// <summary>
    /// Copy this file <em>into</em> <paramref name="destinationDirectory"/>,
    /// preserving the filename. Returns the new path inside the destination
    /// directory. Distinct from <see cref="CopyTo"/>, which treats its argument
    /// as the full destination path.
    /// </summary>
    public AbsolutePath CopyToDirectory(AbsolutePath destinationDirectory, bool overwrite = true)
    {
        if (destinationDirectory is null) throw new ArgumentNullException(nameof(destinationDirectory));
        if (!FileExists())
            throw new InvalidOperationException(
                $"CopyToDirectory only operates on files; '{Value}' is not a file.");
        destinationDirectory.EnsureDirectoryExists();
        var dst = destinationDirectory / Name;
        File.Copy(Value, dst.Value, overwrite);
        return dst;
    }

    public AbsolutePath MoveTo(AbsolutePath destination, bool overwrite = false)
    {
        if (destination is null) throw new ArgumentNullException(nameof(destination));
        destination.Parent?.EnsureDirectoryExists();
        if (DirectoryExists())
        {
            if (overwrite && destination.DirectoryExists()) destination.DeleteDirectory();
            Directory.Move(Value, destination.Value);
        }
        else
        {
            if (overwrite && destination.FileExists()) destination.DeleteFile();
            File.Move(Value, destination.Value);
        }
        return destination;
    }

    private static void CopyDirectoryRecursive(string source, string dest, bool overwrite)
    {
        Directory.CreateDirectory(dest);
        foreach (var file in Directory.EnumerateFiles(source))
            File.Copy(file, Path.Combine(dest, Path.GetFileName(file)), overwrite);
        foreach (var dir in Directory.EnumerateDirectories(source))
            CopyDirectoryRecursive(dir, Path.Combine(dest, Path.GetFileName(dir)), overwrite);
    }

    // ---- Read / Write ----

    public string ReadAllText() => File.ReadAllText(Value);
    public string[] ReadAllLines() => File.ReadAllLines(Value);
    public byte[] ReadAllBytes() => File.ReadAllBytes(Value);

    public AbsolutePath WriteAllText(string content)
    {
        Parent?.EnsureDirectoryExists();
        File.WriteAllText(Value, content);
        return this;
    }

    public AbsolutePath WriteAllLines(IEnumerable<string> contents)
    {
        Parent?.EnsureDirectoryExists();
        File.WriteAllLines(Value, contents);
        return this;
    }

    public AbsolutePath WriteAllBytes(byte[] content)
    {
        Parent?.EnsureDirectoryExists();
        File.WriteAllBytes(Value, content);
        return this;
    }

    public AbsolutePath AppendAllText(string content)
    {
        Parent?.EnsureDirectoryExists();
        File.AppendAllText(Value, content);
        return this;
    }

    // ---- File size ----

    /// <summary>
    /// File size in bytes. Throws <see cref="FileNotFoundException"/> if the
    /// path is not a file (missing or a directory).
    /// </summary>
    public long SizeBytes()
    {
        if (!FileExists()) throw new FileNotFoundException("Path is not a file.", Value);
        return new FileInfo(Value).Length;
    }

    // ---- Hashing ----

    /// <summary>
    /// Hex-encoded SHA-256 of the file's contents. Throws if the path is
    /// not a file. Useful for input-hash declarations on idempotent targets.
    /// </summary>
    public string Sha256()
    {
        using var stream = File.OpenRead(Value);
        var bytes = SHA256.HashData(stream);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    /// <summary>Hex-encoded SHA-256 of an arbitrary string. Convenience for inline use.</summary>
    public static string Sha256Of(string content)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    // ---- Children / enumeration ----

    /// <summary>Top-level files in this directory.</summary>
    public IEnumerable<AbsolutePath> EnumerateFiles()
    {
        if (!DirectoryExists()) yield break;
        foreach (var f in Directory.EnumerateFiles(Value))
            yield return new AbsolutePath(f);
    }

    /// <summary>Top-level subdirectories.</summary>
    public IEnumerable<AbsolutePath> EnumerateDirectories()
    {
        if (!DirectoryExists()) yield break;
        foreach (var d in Directory.EnumerateDirectories(Value))
            yield return new AbsolutePath(d);
    }

    // ---- Globbing ----

    /// <summary>
    /// Glob files relative to this directory. Patterns support
    /// <c>**</c> (any depth), <c>*</c> (single segment), and <c>?</c>
    /// (single character) per <see cref="Microsoft.Extensions.FileSystemGlobbing"/>.
    /// Multiple patterns can be passed; results are deduped.
    /// </summary>
    public IReadOnlyList<AbsolutePath> GlobFiles(params string[] patterns)
    {
        if (!DirectoryExists()) return Array.Empty<AbsolutePath>();
        var matcher = new Matcher();
        foreach (var p in patterns) matcher.AddInclude(p);
        var result = matcher.GetResultsInFullPath(Value);
        return result.Select(p => new AbsolutePath(Path.GetFullPath(p)))
            .Distinct().ToList();
    }

    /// <summary>
    /// Glob directories relative to this directory. Patterns target directory paths directly —
    /// <c>"**/bin"</c> matches every <c>bin/</c> directory at any depth, <c>"*/obj"</c> matches
    /// only top-level <c>obj/</c> subtrees.
    /// </summary>
    /// <remarks>
    /// Microsoft.Extensions.FileSystemGlobbing's <c>Matcher</c> is file-oriented — <c>GetResultsInFullPath</c>
    /// against pattern <c>"**/bin"</c> returns no hits because nothing in the tree is a FILE literally named
    /// <c>bin</c>. We walk every directory and test each one's relative path against the matcher instead.
    /// </remarks>
    public IReadOnlyList<AbsolutePath> GlobDirectories(params string[] patterns)
    {
        if (!DirectoryExists()) return Array.Empty<AbsolutePath>();
        var matcher = new Matcher();
        foreach (var p in patterns) matcher.AddInclude(p);

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var dir in Directory.EnumerateDirectories(Value, "*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(Value, dir).Replace(Path.DirectorySeparatorChar, '/');
            if (matcher.Match(rel).HasMatches) seen.Add(dir);
        }
        return seen.Select(d => new AbsolutePath(d)).ToList();
    }
}
