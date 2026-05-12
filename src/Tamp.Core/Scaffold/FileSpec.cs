namespace Tamp.Scaffold;

/// <summary>
/// One file a template wants written. The runner reads <see cref="Mode"/> to
/// decide whether to write, skip, or fail when the target path already exists.
/// Set <see cref="Executable"/> on POSIX shim scripts so the runner emits them
/// with mode 0755 (no-op on Windows).
/// </summary>
public sealed record FileSpec(AbsolutePath Path, string Content, WriteMode Mode)
{
    /// <summary>
    /// When true, the runner chmods the written file to 0755 on POSIX. No effect on Windows.
    /// Defaults to false; set for shell-script shims (<c>tamp.sh</c>, hook scripts, etc.).
    /// </summary>
    public bool Executable { get; init; }
}

/// <summary>
/// What to do when <see cref="FileSpec.Path"/> already exists on disk.
/// </summary>
public enum WriteMode
{
    /// <summary>Fail if the file already exists. Default for primary scaffold outputs (Build.cs, Build.csproj).</summary>
    Create,

    /// <summary>Leave the existing file alone and continue. For optional companions (.config/dotnet-tools.json initial create).</summary>
    SkipIfExists,

    /// <summary>
    /// Reserved for v0.2.0+ — semantic merge into structured files (JSON tools.json, YAML CI workflows).
    /// v0.1.0 implementations should throw if asked to handle this mode.
    /// </summary>
    Merge,
}
