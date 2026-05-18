namespace Tamp.OsvScanner.V2;

/// <summary>
/// Tamp wrapper for Google's <c>osv-scanner</c> CLI (2.x). Build a
/// <see cref="CommandPlan"/> the runner dispatches.
/// </summary>
/// <remarks>
/// Adopter installs the binary (homebrew, scoop, or GitHub release).
/// Default output is SARIF so the result slots into the existing
/// SecurityScan / DefectDojo flow alongside OpenGrep + Roslyn.
/// </remarks>
public static class OsvScanner
{
    /// <summary>Build an <c>osv-scanner scan source</c> CommandPlan.</summary>
    public static CommandPlan ScanSource(Action<OsvScannerScanSettings> configure)
    {
        if (configure is null) throw new ArgumentNullException(nameof(configure));
        var settings = new OsvScannerScanSettings();
        configure(settings);
        return settings.ToCommandPlan();
    }
}
