namespace Tamp.Sarif;

/// <summary>
/// Implemented by anything that produces SARIF findings — SAST wrappers
/// (OpenGrep, Trivy), IaC scanners (Checkov), secret scanners (gitleaks),
/// and so on. Sinks (DefectDojo, console reporters, beacon telemetry)
/// consume <see cref="SarifLog"/> and never depend on the concrete source.
/// </summary>
public interface IFindingSource
{
    /// <summary>
    /// Run the scan and return the resulting SARIF log. Implementations
    /// SHOULD return a syntactically valid SARIF 2.1.0 log even when no
    /// findings are produced (an empty <c>results</c> array, not a missing
    /// run).
    /// </summary>
    Task<SarifLog> ScanAsync(CancellationToken cancellationToken = default);
}
