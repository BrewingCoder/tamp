namespace Tamp.OpenGrep.V1;

/// <summary>
/// Tamp wrapper for the <c>opengrep</c> CLI — the multi-vendor-governed
/// fork of Semgrep chosen for license stability (no proprietary Pro tier
/// that can paywall rules in a future release).
/// </summary>
/// <remarks>
/// Adopter installs the tool (homebrew, pip, or a binary release).
/// Output is SARIF by default so downstream
/// <c>Tamp.DefectDojo.ImportSarifAsync</c> can consume it directly.
/// </remarks>
public static class OpenGrep
{
    /// <summary>Build an <c>opengrep scan</c> CommandPlan.</summary>
    public static CommandPlan Scan(Action<OpenGrepScanSettings> configure)
    {
        if (configure is null) throw new ArgumentNullException(nameof(configure));
        var settings = new OpenGrepScanSettings();
        configure(settings);
        return settings.ToCommandPlan();
    }
}
