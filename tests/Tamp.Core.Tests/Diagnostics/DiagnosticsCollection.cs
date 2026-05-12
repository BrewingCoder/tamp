using Xunit;

namespace Tamp.Core.Tests.Diagnostics;

/// <summary>
/// xUnit collection that serializes tests subscribing to Tamp's process-global
/// <see cref="System.Diagnostics.ActivitySource"/>s. ActivityListener
/// subscriptions are process-wide; parallel tests racing on Activity creation
/// produce nondeterministic "more than one matching element" failures when
/// listener A sees activities created by test B's build. Tag any test that
/// installs an <see cref="System.Diagnostics.ActivityListener"/> with
/// <c>[Collection(nameof(DiagnosticsCollection))]</c>.
/// </summary>
[CollectionDefinition(nameof(DiagnosticsCollection), DisableParallelization = true)]
public sealed class DiagnosticsCollection
{
}
