using System.Globalization;
using System.Runtime.InteropServices;

namespace Tamp;

/// <summary>
/// Builds the <see cref="HostProfile"/> exactly once at startup. The result
/// is immutable and represents a snapshot of the host as seen at construction
/// time — environment changes after Build() are not reflected.
/// </summary>
/// <remarks>
/// Detection is best-effort and platform-aware. Where a value cannot be
/// determined cheaply and reliably, we prefer a sentinel (zero, null, empty
/// string) over a process spawn. Targets and modules that need higher
/// fidelity in a specific area can implement that area themselves.
/// </remarks>
public static class HostProfileBuilder
{
    /// <summary>Build a profile using the live process environment.</summary>
    public static HostProfile Build() => Build(Environment.GetEnvironmentVariable);

    /// <summary>
    /// Build a profile using a caller-supplied env-var lookup. Used by tests
    /// to exercise CI-vendor and container-detection code paths without
    /// polluting the global process environment.
    /// </summary>
    internal static HostProfile Build(Func<string, string?> getEnv)
    {
        var os = DetectOSFamily();
        var memInfo = GC.GetGCMemoryInfo();

        return new HostProfile
        {
            Os = os,
            Arch = RuntimeInformation.ProcessArchitecture,
            LogicalCpuCount = Environment.ProcessorCount,
            PhysicalCpuCount = DetectPhysicalCpuCount(os),
            TotalMemoryBytes = Math.Max(0, memInfo.TotalAvailableMemoryBytes),
            AvailableMemoryBytes = Math.Max(0, memInfo.TotalAvailableMemoryBytes - memInfo.MemoryLoadBytes),
            InContainer = DetectInContainer(getEnv),
            InWsl = DetectInWsl(os),
            Cgroup = DetectCgroupLimits(os),
            Ci = DetectCiVendor(getEnv),
            Windows = os == OSFamily.Windows ? DetectWindowsInfo() : null,
            Linux = os == OSFamily.Linux ? DetectLinuxInfo() : null,
            MacOs = os == OSFamily.MacOs ? DetectMacOsInfo() : null,
        };
    }

    internal static OSFamily DetectOSFamily()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return OSFamily.Windows;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) return OSFamily.Linux;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) return OSFamily.MacOs;
        return OSFamily.Unknown;
    }

    private static int DetectPhysicalCpuCount(OSFamily os)
    {
        // Physical-core detection requires OS-specific work (parsing
        // /proc/cpuinfo on Linux, querying WMI on Windows, sysctl on macOS).
        // For v0 we report logical-equals-physical as a defensible upper
        // bound — over-reporting is conservative for scheduling decisions
        // (we won't accidentally over-subscribe on physical-core counts
        // we don't actually have).
        // TODO: implement OS-specific physical-core detection.
        return Environment.ProcessorCount;
    }

    internal static bool DetectInContainer(Func<string, string?> getEnv)
    {
        // .NET base images set this; honour it as authoritative.
        if (string.Equals(getEnv("DOTNET_RUNNING_IN_CONTAINER"), "true", StringComparison.OrdinalIgnoreCase))
            return true;

        // Docker writes a marker file at the container's root. Linux only.
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            if (File.Exists("/.dockerenv")) return true;
            if (File.Exists("/run/.containerenv")) return true;  // Podman.
        }

        return false;
    }

    internal static bool DetectInWsl(OSFamily os)
    {
        if (os != OSFamily.Linux) return false;
        try
        {
            const string osreleasePath = "/proc/sys/kernel/osrelease";
            if (!File.Exists(osreleasePath)) return false;
            var text = File.ReadAllText(osreleasePath);
            return text.Contains("microsoft", StringComparison.OrdinalIgnoreCase)
                || text.Contains("WSL", StringComparison.Ordinal);
        }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
    }

    internal static CgroupLimits? DetectCgroupLimits(OSFamily os)
    {
        if (os != OSFamily.Linux) return null;

        try
        {
            // cgroup v2 marker: /sys/fs/cgroup/cgroup.controllers exists.
            if (File.Exists("/sys/fs/cgroup/cgroup.controllers"))
            {
                long? memLimit = ReadCgroupV2MemoryLimit();
                double? cpuQuota = ReadCgroupV2CpuQuota();
                if (memLimit is null && cpuQuota is null) return null;
                return new CgroupLimits { Version = 2, MemoryLimitBytes = memLimit, CpuQuota = cpuQuota };
            }

            // cgroup v1 fallback.
            long? v1Mem = ReadCgroupV1MemoryLimit();
            double? v1Cpu = ReadCgroupV1CpuQuota();
            if (v1Mem is null && v1Cpu is null) return null;
            return new CgroupLimits { Version = 1, MemoryLimitBytes = v1Mem, CpuQuota = v1Cpu };
        }
        catch (IOException) { return null; }
        catch (UnauthorizedAccessException) { return null; }
    }

    private static long? ReadCgroupV2MemoryLimit()
    {
        const string path = "/sys/fs/cgroup/memory.max";
        if (!File.Exists(path)) return null;
        var text = File.ReadAllText(path).Trim();
        if (text == "max") return null;  // No limit set.
        return long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : null;
    }

    private static double? ReadCgroupV2CpuQuota()
    {
        const string path = "/sys/fs/cgroup/cpu.max";
        if (!File.Exists(path)) return null;
        var text = File.ReadAllText(path).Trim();
        // Format: "<quota> <period>" or "max <period>".
        var parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2) return null;
        if (parts[0] == "max") return null;
        if (!long.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var quota)) return null;
        if (!long.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var period)) return null;
        if (period <= 0) return null;
        return (double)quota / period;
    }

    private static long? ReadCgroupV1MemoryLimit()
    {
        const string path = "/sys/fs/cgroup/memory/memory.limit_in_bytes";
        if (!File.Exists(path)) return null;
        var text = File.ReadAllText(path).Trim();
        if (!long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v)) return null;
        // v1 uses ~9 exabytes as the "no limit" sentinel; treat huge values as unlimited.
        if (v > long.MaxValue / 2) return null;
        return v;
    }

    private static double? ReadCgroupV1CpuQuota()
    {
        const string quotaPath = "/sys/fs/cgroup/cpu/cpu.cfs_quota_us";
        const string periodPath = "/sys/fs/cgroup/cpu/cpu.cfs_period_us";
        if (!File.Exists(quotaPath) || !File.Exists(periodPath)) return null;
        if (!long.TryParse(File.ReadAllText(quotaPath).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var quota)) return null;
        if (!long.TryParse(File.ReadAllText(periodPath).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var period)) return null;
        if (quota <= 0 || period <= 0) return null;  // -1 = unlimited.
        return (double)quota / period;
    }

    internal static CiVendor? DetectCiVendor(Func<string, string?> getEnv)
    {
        // Vendor-specific markers checked first; the generic CI=true var is
        // the last-resort signal (when we know we're in CI but don't know
        // which one).
        if (Truthy(getEnv("GITHUB_ACTIONS"))) return CiVendor.GitHubActions;
        if (Truthy(getEnv("TF_BUILD"))) return CiVendor.AzureDevOps;
        if (Truthy(getEnv("GITLAB_CI"))) return CiVendor.GitLabCi;
        if (Truthy(getEnv("APPVEYOR"))) return CiVendor.AppVeyor;
        if (!string.IsNullOrEmpty(getEnv("TEAMCITY_VERSION"))) return CiVendor.TeamCity;
        if (!string.IsNullOrEmpty(getEnv("JENKINS_URL"))) return CiVendor.Jenkins;
        if (Truthy(getEnv("CIRCLECI"))) return CiVendor.CircleCI;
        if (Truthy(getEnv("BUILDKITE"))) return CiVendor.Buildkite;
        if (Truthy(getEnv("TRAVIS"))) return CiVendor.Travis;
        if (Truthy(getEnv("CI"))) return CiVendor.Unknown;  // CI=true, vendor unknown.
        return null;  // Not in CI.
    }

    private static bool Truthy(string? value)
        => string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
        || string.Equals(value, "1", StringComparison.Ordinal)
        || string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);

    private static WindowsHostInfo DetectWindowsInfo()
    {
        // Defender state and admin role lookups live in OS-specific code
        // paths that aren't reachable from this multi-target file without
        // platform guards. For v0 we ship the type with default values and
        // leave authoritative detection to a follow-up that lives behind a
        // platform-conditional source file.
        // TODO: Windows-specific source file with `IsAdmin` and Defender state.
        return new WindowsHostInfo();
    }

    internal static LinuxHostInfo DetectLinuxInfo()
    {
        var info = new LinuxHostInfo();
        try
        {
            const string path = "/etc/os-release";
            if (!File.Exists(path)) return info;

            string id = string.Empty;
            string version = string.Empty;
            foreach (var line in File.ReadLines(path))
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("ID=", StringComparison.Ordinal))
                    id = StripQuotes(trimmed["ID=".Length..]);
                else if (trimmed.StartsWith("VERSION_ID=", StringComparison.Ordinal))
                    version = StripQuotes(trimmed["VERSION_ID=".Length..]);
            }
            return info with { DistroId = id, DistroVersion = version };
        }
        catch (IOException) { return info; }
        catch (UnauthorizedAccessException) { return info; }
    }

    private static MacOsHostInfo DetectMacOsInfo()
    {
        // Environment.OSVersion on macOS reports the Darwin kernel version,
        // which is not the marketing version users expect. Authoritative
        // marketing-version detection requires reading SystemVersion.plist
        // or spawning `sw_vers -productVersion`. For v0 we report the
        // kernel version with a clear marker; consumers can branch on the
        // string if they need the marketing version themselves.
        // TODO: parse /System/Library/CoreServices/SystemVersion.plist.
        var v = Environment.OSVersion.Version;
        return new MacOsHostInfo { ProductVersion = $"darwin {v}" };
    }

    private static string StripQuotes(string s)
    {
        if (s.Length >= 2 && s[0] == '"' && s[^1] == '"') return s[1..^1];
        if (s.Length >= 2 && s[0] == '\'' && s[^1] == '\'') return s[1..^1];
        return s;
    }
}
