namespace Tamp;

/// <summary>
/// Snapshot of the machine Tamp is running on, captured once at startup and frozen.
/// Targets and modules read from this; nothing mutates it after construction.
/// </summary>
public sealed record HostProfile
{
    public required OSFamily Os { get; init; }
    public required System.Runtime.InteropServices.Architecture Arch { get; init; }
    public required int LogicalCpuCount { get; init; }
    public required int PhysicalCpuCount { get; init; }
    public required long TotalMemoryBytes { get; init; }
    public required long AvailableMemoryBytes { get; init; }

    public bool InContainer { get; init; }
    public bool InWsl { get; init; }
    public CgroupLimits? Cgroup { get; init; }

    public CiVendor? Ci { get; init; }

    public WindowsHostInfo? Windows { get; init; }
    public LinuxHostInfo? Linux { get; init; }
    public MacOsHostInfo? MacOs { get; init; }
}

public enum OSFamily
{
    Unknown = 0,
    Windows,
    Linux,
    MacOs,
}

public enum CiVendor
{
    Unknown = 0,
    GitHubActions,
    AzureDevOps,
    GitLabCi,
    AppVeyor,
    TeamCity,
    Jenkins,
    CircleCI,
    Buildkite,
    Travis,
}

public sealed record CgroupLimits
{
    public required int Version { get; init; }
    public long? MemoryLimitBytes { get; init; }
    public double? CpuQuota { get; init; }
}

public sealed record WindowsHostInfo
{
    public bool IsAdmin { get; init; }
    public bool DefenderActive { get; init; }
}

public sealed record LinuxHostInfo
{
    public string DistroId { get; init; } = string.Empty;
    public string DistroVersion { get; init; } = string.Empty;
}

public sealed record MacOsHostInfo
{
    public string ProductVersion { get; init; } = string.Empty;
}
