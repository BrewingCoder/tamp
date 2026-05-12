using System.Collections.Generic;

namespace Tamp.Scaffold;

/// <summary>
/// A scaffolding template. Renders against a <see cref="ScaffoldContext"/> to
/// produce a sequence of <see cref="FileSpec"/>s the CLI writes.
/// </summary>
/// <remarks>
/// <para>
/// The CLI ships one embedded template (<c>MinimalTemplate</c>). Additional
/// templates are distributed as NuGet packages (channel lands in 0.2.0):
/// authors implement <see cref="IScaffoldTemplate"/> and expose it via
/// <see cref="IScaffoldTemplateProvider"/>.
/// </para>
/// <para>
/// The contract lives in Tamp.Core so external template packages take exactly
/// one dependency — the one their generated Build.cs already references.
/// </para>
/// </remarks>
public interface IScaffoldTemplate
{
    /// <summary>Stable identifier passed via <c>--template &lt;name&gt;</c>.</summary>
    string Name { get; }

    /// <summary>One-line description shown in <c>tamp init --list-templates</c>.</summary>
    string Description { get; }

    /// <summary>
    /// Lowest Tamp.Core version this template's generated files require to
    /// compile. The CLI compares against its own version and refuses with a
    /// friendly upgrade message on mismatch (drift protection).
    /// </summary>
    string MinimumTampCoreVersion { get; }

    /// <summary>Emit the files this template wants written.</summary>
    IEnumerable<FileSpec> Render(ScaffoldContext ctx);
}

/// <summary>
/// Entry-point interface a NuGet template package exposes so the CLI's
/// NuGet-distributed template channel (v0.2.0+) can load and invoke it.
/// </summary>
/// <remarks>
/// Convention: the template assembly contains exactly one public type
/// implementing this interface, discovered via reflection at load time.
/// (Final discovery mechanism — attribute vs convention vs both — settles
/// in the v0.2.0 NuGet-source implementation.)
/// </remarks>
public interface IScaffoldTemplateProvider
{
    IScaffoldTemplate GetTemplate();
}
