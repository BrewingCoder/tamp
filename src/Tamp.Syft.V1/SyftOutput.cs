namespace Tamp.Syft.V1;

/// <summary>
/// A single syft output target. <see cref="Path"/> is optional — when
/// null, syft writes to stdout. Maps to one <c>-o &lt;format&gt;[=&lt;file&gt;]</c>
/// argument.
/// </summary>
public sealed record SyftOutput(SyftFormat Format, string? Path = null);
