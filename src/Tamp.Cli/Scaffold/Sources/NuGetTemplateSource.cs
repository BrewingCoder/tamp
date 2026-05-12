using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Tamp.Scaffold;

namespace Tamp.Cli.Scaffold.Sources;

/// <summary>
/// NuGet-distributed template source. <strong>v0.2.0+ feature.</strong>
/// </summary>
/// <remarks>
/// <para>
/// The shape is implemented so v0.1.0 leaves the integration path obvious and
/// future-version flag parsing has a real type to register against; the actual
/// restore + load is not wired in v0.1.0. Calling <see cref="ListAsync"/> or
/// <see cref="ResolveAsync"/> throws with a clean "lands in 0.2.0" message.
/// </para>
/// <para>
/// When v0.2.0 lands the implementation, the algorithm is:
/// <list type="number">
///   <item>Restore the requested package (e.g. <c>Tamp.Templates.Fullstack</c>) via the NuGet client.</item>
///   <item>Load the package assembly into the current process.</item>
///   <item>Resolve a public <see cref="IScaffoldTemplateProvider"/> impl from the assembly.</item>
///   <item>Invoke <c>GetTemplate()</c> and return the result.</item>
///   <item>Compare <c>template.MinimumTampCoreVersion</c> against this CLI's own version; refuse on mismatch.</item>
/// </list>
/// </para>
/// </remarks>
public sealed class NuGetTemplateSource : IScaffoldTemplateSource
{
    public string Source => "nuget";

    public ValueTask<IReadOnlyList<IScaffoldTemplate>> ListAsync(CancellationToken ct)
        => throw new System.NotImplementedException(
            "NuGet template distribution lands in Tamp.Cli 0.2.0. " +
            "Use the embedded minimal template for now: `tamp init`.");

    public ValueTask<IScaffoldTemplate?> ResolveAsync(string name, CancellationToken ct)
        => throw new System.NotImplementedException(
            "NuGet template distribution lands in Tamp.Cli 0.2.0. " +
            $"Cannot resolve '{name}' yet. Use the embedded minimal template: `tamp init`.");
}
