namespace Tamp;

/// <summary>
/// Declares the project name + optional area that this build script belongs to.
/// Language-agnostic: pure-JS, pure-Python, Rust, Go, mixed-stack repos use
/// this the same way — it identifies the LOGICAL project, not anything
/// .NET-specific. Tamp builds DON'T require a .NET solution or csproj to run;
/// the build script itself happens to be .NET, but what it builds can be
/// anything.
/// Surfaced on the root build span as <c>tamp.build.project.name</c> and
/// <c>tamp.build.project.area</c> tags (ADR 0018); the <c>Tamp.Otel</c>
/// satellite maps these to OpenTelemetry's <c>service.name</c> /
/// <c>service.namespace</c> resource attributes.
/// </summary>
/// <remarks>
/// <para>
/// Designed for the polyrepo case: a single product spread across multiple
/// repos and components — common shape regardless of language stack. Set
/// <see cref="Name"/> to the product (<c>"HoldFast"</c>), <see cref="Area"/>
/// to the component (<c>"frontend"</c> / <c>"backend"</c> / <c>"infra"</c>).
/// Telemetry from every repo in the product groups under one name;
/// per-component queries stay possible via the area.
/// </para>
/// <para>
/// Apply to the build class:
/// </para>
/// <code>
/// [BuildProject(Name = "HoldFast", Area = "frontend")]
/// class Build : TampBuild { ... }
/// </code>
/// <para>
/// Absent the attribute, Tamp falls back through best-effort guesses:
/// <c>[Solution]</c> filename when the build script declares one (.NET-only;
/// inapplicable to pure-JS / Python / etc. builds — skipped silently when
/// absent), then the repository root directory name. The repo-dir fallback
/// works for any language stack. Use the attribute when the repo-dir name
/// isn't the right product name, or whenever polyrepo correlation matters.
/// </para>
/// </remarks>
[System.AttributeUsage(System.AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
public sealed class BuildProjectAttribute : System.Attribute
{
    /// <summary>Logical product name (e.g. <c>"HoldFast"</c>). Required when the attribute is applied.</summary>
    public string Name { get; }

    /// <summary>Optional sub-area within the product (e.g. <c>"frontend"</c>, <c>"backend"</c>, <c>"infra"</c>).</summary>
    public string? Area { get; init; }

    public BuildProjectAttribute(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new System.ArgumentException("BuildProject name must be non-empty.", nameof(name));
        Name = name;
    }
}
