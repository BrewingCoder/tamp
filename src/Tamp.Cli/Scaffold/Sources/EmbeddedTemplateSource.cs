using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Tamp.Cli.Scaffold.Templates;
using Tamp.Scaffold;

namespace Tamp.Cli.Scaffold.Sources;

/// <summary>
/// Returns the template(s) compiled into the <c>Tamp.Cli</c> binary itself.
/// v0.1.0 carries exactly one — <see cref="MinimalTemplate"/>. Works offline
/// by construction; this is the on-ramp guarantee for federal / locked-down
/// environments.
/// </summary>
public sealed class EmbeddedTemplateSource : IScaffoldTemplateSource
{
    private readonly IReadOnlyList<IScaffoldTemplate> _templates =
    [
        new MinimalTemplate(),
    ];

    public string Source => "embedded";

    public ValueTask<IReadOnlyList<IScaffoldTemplate>> ListAsync(CancellationToken ct)
        => new(_templates);

    public ValueTask<IScaffoldTemplate?> ResolveAsync(string name, CancellationToken ct)
        => new(_templates.FirstOrDefault(t => string.Equals(t.Name, name, System.StringComparison.OrdinalIgnoreCase)));
}
