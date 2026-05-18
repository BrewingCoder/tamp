namespace Tamp.Trivy;

/// <summary>Trivy severity filter (<c>--severity</c>). Wire values are uppercase.</summary>
public enum TrivySeverity
{
    Unknown,
    Low,
    Medium,
    High,
    Critical,
}
