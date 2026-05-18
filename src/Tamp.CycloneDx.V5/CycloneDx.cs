namespace Tamp.CycloneDx.V5;

/// <summary>
/// Tamp wrapper for the <c>dotnet-CycloneDX</c> global tool. Build a
/// <see cref="CommandPlan"/> the runner dispatches.
/// </summary>
/// <remarks>
/// Pre-requisite: adopters must install the tool with
/// <c>dotnet tool install --global CycloneDX</c>. Wave 1 keeps the tool
/// install as an adopter responsibility; a Tamp-managed-tool flow may
/// land in a follow-up.
/// </remarks>
public static class CycloneDx
{
    /// <summary>Generate a CycloneDX SBOM from a project, solution, or directory.</summary>
    public static CommandPlan Generate(Action<CycloneDxGenerateSettings> configure)
    {
        if (configure is null) throw new ArgumentNullException(nameof(configure));
        var settings = new CycloneDxGenerateSettings();
        configure(settings);
        return settings.ToCommandPlan();
    }
}
