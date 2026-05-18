namespace Tamp.Trivy;

/// <summary>
/// Tamp wrapper for Aqua Security's <c>trivy</c> CLI. Three subcommand
/// methods cover the canonical scan modes:
/// <list type="bullet">
///   <item><see cref="ScanImage"/> — <c>trivy image</c>; container OS-package + lockfile vulnerabilities.</item>
///   <item><see cref="ScanConfig"/> — <c>trivy config</c>; IaC misconfiguration scan (Terraform / Kubernetes / Dockerfile / CloudFormation / Helm / Ansible).</item>
///   <item><see cref="ScanFilesystem"/> — <c>trivy fs</c>; source-tree secrets + IaC + lockfile vulns.</item>
/// </list>
/// </summary>
/// <remarks>
/// Adopter installs Trivy (homebrew, apt/yum, or release binary). Default
/// format is SARIF so output slots into the chain alongside OpenGrep, the
/// Roslyn ErrorLog leg, and OSV-Scanner.
/// </remarks>
public static class Trivy
{
    /// <summary>Scan a container image (registry, daemon, or local tar).</summary>
    public static CommandPlan ScanImage(Action<TrivyImageSettings> configure)
    {
        if (configure is null) throw new ArgumentNullException(nameof(configure));
        var settings = new TrivyImageSettings();
        configure(settings);
        return settings.ToCommandPlan();
    }

    /// <summary>Scan IaC configurations for misconfiguration findings.</summary>
    public static CommandPlan ScanConfig(Action<TrivyConfigSettings> configure)
    {
        if (configure is null) throw new ArgumentNullException(nameof(configure));
        var settings = new TrivyConfigSettings();
        configure(settings);
        return settings.ToCommandPlan();
    }

    /// <summary>Scan a filesystem path for secrets, misconfig, and/or lockfile vulnerabilities.</summary>
    public static CommandPlan ScanFilesystem(Action<TrivyFilesystemSettings> configure)
    {
        if (configure is null) throw new ArgumentNullException(nameof(configure));
        var settings = new TrivyFilesystemSettings();
        configure(settings);
        return settings.ToCommandPlan();
    }
}
