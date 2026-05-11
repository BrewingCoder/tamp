using Xunit;

namespace Tamp.Core.Tests;

/// <summary>
/// xUnit collection that serializes tests which capture Console.Error or Console.Out.
/// `Console.SetError` / `Console.SetOut` are process-global; parallel tests racing on
/// those calls produce nondeterministic failures (the second SetError wins and the first
/// test's StringWriter receives nothing). Tag any test that captures stdout/stderr with
/// <c>[Collection(nameof(ConsoleCaptureCollection))]</c>.
/// </summary>
[CollectionDefinition(nameof(ConsoleCaptureCollection), DisableParallelization = true)]
public sealed class ConsoleCaptureCollection
{
}
