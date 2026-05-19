namespace Tamp.OpenGrep;

/// <summary>
/// Severity threshold filter for the <c>opengrep scan --severity</c> flag.
/// Findings below the threshold are suppressed.
/// </summary>
public enum OpenGrepSeverity
{
    Info,
    Warning,
    Error,
}
