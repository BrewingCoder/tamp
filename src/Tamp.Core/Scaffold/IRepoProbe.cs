namespace Tamp.Scaffold;

/// <summary>
/// Discovers facts about the working directory and contributes them to the
/// <see cref="ScaffoldContext"/> templates will consume. The CLI ships
/// <c>DotnetSolutionProbe</c>; third-party templates can register their own
/// probes (e.g. for monorepo / Yarn-workspace / Helm-chart detection).
/// </summary>
public interface IRepoProbe
{
    /// <summary>Inspect <paramref name="repoRoot"/> and contribute findings to <paramref name="ctx"/>.</summary>
    void Probe(AbsolutePath repoRoot, ScaffoldContextBuilder ctx);
}
