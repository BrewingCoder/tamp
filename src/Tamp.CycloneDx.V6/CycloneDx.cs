namespace Tamp.CycloneDx.V6;

/// <summary>
/// Tamp wrapper for the <c>dotnet-CycloneDX</c> global tool (6.x). Build
/// a <see cref="CommandPlan"/> the runner dispatches.
/// </summary>
/// <remarks>
/// Adopter installs with <c>dotnet tool install --global CycloneDX</c>.
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
