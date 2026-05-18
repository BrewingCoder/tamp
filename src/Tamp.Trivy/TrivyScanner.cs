namespace Tamp.Trivy;

/// <summary>
/// Trivy scanner family (<c>--scanners</c>). Comma-joined on the wire.
/// </summary>
public enum TrivyScanner
{
    /// <summary>Known-CVE matching against the project's dependencies / OS packages.</summary>
    Vuln,

    /// <summary>IaC misconfiguration (Terraform, Kubernetes, Dockerfile, CloudFormation, Helm, Ansible).</summary>
    Misconfig,

    /// <summary>Hard-coded credentials / API keys in source.</summary>
    Secret,

    /// <summary>License compliance findings.</summary>
    License,
}
