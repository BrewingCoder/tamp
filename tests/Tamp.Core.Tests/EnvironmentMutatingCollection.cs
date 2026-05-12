using Xunit;

namespace Tamp.Core.Tests;

/// <summary>
/// xUnit collection that serializes tests which mutate process-global environment
/// variables (most often <c>PATH</c>). <see cref="Environment.SetEnvironmentVariable"/>
/// changes are visible to every test in the same process, so parallel tests reading
/// <c>PATH</c> can observe a transient value set by a concurrent test and fail with
/// "tool not found"-style errors. Tag any test that calls <c>SetEnvironmentVariable</c>
/// — and any test that reads PATH for resolution — with
/// <c>[Collection(nameof(EnvironmentMutatingCollection))]</c>.
/// </summary>
[CollectionDefinition(nameof(EnvironmentMutatingCollection), DisableParallelization = true)]
public sealed class EnvironmentMutatingCollection
{
}
