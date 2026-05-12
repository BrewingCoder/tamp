using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Tamp.Scaffold;

namespace Tamp.Cli.Scaffold;

/// <summary>
/// Pluggable provider of <see cref="IScaffoldTemplate"/>s. The hinge that lets
/// v0.2.0 add NuGet-distributed templates without touching the
/// <c>init</c> command code path.
/// </summary>
/// <remarks>
/// v0.1.0 implementations:
/// <list type="bullet">
///   <item><see cref="Sources.EmbeddedTemplateSource"/> — returns the single minimal template baked into <c>Tamp.Cli</c>.</item>
///   <item><see cref="Sources.NuGetTemplateSource"/> — stub; throws when asked to actually restore. v0.2.0 enables it.</item>
/// </list>
/// The CLI command iterates registered sources in priority order (embedded
/// first for offline guarantee) and picks the first match.
/// </remarks>
public interface IScaffoldTemplateSource
{
    /// <summary>Source identifier (e.g. <c>"embedded"</c>, <c>"nuget:Tamp.Templates.Fullstack"</c>). For diagnostics.</summary>
    string Source { get; }

    /// <summary>Enumerate every template this source can produce.</summary>
    ValueTask<IReadOnlyList<IScaffoldTemplate>> ListAsync(CancellationToken ct);

    /// <summary>Resolve a template by name. Returns null if this source doesn't carry it.</summary>
    ValueTask<IScaffoldTemplate?> ResolveAsync(string name, CancellationToken ct);
}
