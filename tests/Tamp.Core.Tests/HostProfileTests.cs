using System.Runtime.InteropServices;
using Bogus;
using Xunit;

namespace Tamp.Core.Tests;

public sealed class HostProfileTests
{
    private static readonly Faker Faker = new() { Random = new Randomizer(unchecked((int)0xCAFEF00D)) };

    private static HostProfile MinimalLinux() => new()
    {
        Os = OSFamily.Linux,
        Arch = Architecture.X64,
        LogicalCpuCount = 8,
        PhysicalCpuCount = 4,
        TotalMemoryBytes = 16L * 1024 * 1024 * 1024,
        AvailableMemoryBytes = 8L * 1024 * 1024 * 1024,
    };

    [Fact]
    public void Required_Properties_Round_Trip()
    {
        var p = MinimalLinux();
        Assert.Equal(OSFamily.Linux, p.Os);
        Assert.Equal(Architecture.X64, p.Arch);
        Assert.Equal(8, p.LogicalCpuCount);
        Assert.Equal(4, p.PhysicalCpuCount);
    }

    [Fact]
    public void Optional_Properties_Default_To_Sensible_Values()
    {
        var p = MinimalLinux();
        Assert.False(p.InContainer);
        Assert.False(p.InWsl);
        Assert.Null(p.Cgroup);
        Assert.Null(p.Ci);
        Assert.Null(p.Windows);
        Assert.Null(p.Linux);
        Assert.Null(p.MacOs);
    }

    [Fact]
    public void Records_Are_Value_Equal()
    {
        var a = MinimalLinux();
        var b = MinimalLinux();
        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void With_Returns_Mutated_Copy_Without_Touching_Original()
    {
        var a = MinimalLinux();
        var b = a with { LogicalCpuCount = 16 };
        Assert.Equal(8, a.LogicalCpuCount);
        Assert.Equal(16, b.LogicalCpuCount);
        Assert.NotEqual(a, b);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(int.MaxValue)]
    public void Cpu_Counts_Accept_Boundary_Values(int n)
    {
        var p = MinimalLinux() with { LogicalCpuCount = n, PhysicalCpuCount = n };
        Assert.Equal(n, p.LogicalCpuCount);
        Assert.Equal(n, p.PhysicalCpuCount);
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(1L)]
    [InlineData(long.MaxValue)]
    public void Memory_Bytes_Accept_Boundary_Values(long bytes)
    {
        var p = MinimalLinux() with { TotalMemoryBytes = bytes, AvailableMemoryBytes = bytes };
        Assert.Equal(bytes, p.TotalMemoryBytes);
        Assert.Equal(bytes, p.AvailableMemoryBytes);
    }

    [Fact]
    public void Cgroup_Limits_Are_Independent_Optional_Properties()
    {
        var memOnly = new CgroupLimits { Version = 2, MemoryLimitBytes = 4_000_000_000L };
        var cpuOnly = new CgroupLimits { Version = 2, CpuQuota = 2.5 };
        var both = new CgroupLimits { Version = 2, MemoryLimitBytes = 4_000_000_000L, CpuQuota = 2.5 };

        Assert.Null(memOnly.CpuQuota);
        Assert.NotNull(memOnly.MemoryLimitBytes);
        Assert.Null(cpuOnly.MemoryLimitBytes);
        Assert.NotNull(cpuOnly.CpuQuota);
        Assert.NotEqual(memOnly, cpuOnly);
        Assert.NotEqual(memOnly, both);
    }

    [Fact]
    public void Os_Family_Defaults_To_Unknown()
    {
        // Unknown is the zero-value; users who skip detection get a clear sentinel.
        Assert.Equal(0, (int)OSFamily.Unknown);
    }

    [Fact]
    public void Ci_Vendor_Defaults_To_Unknown()
    {
        Assert.Equal(0, (int)CiVendor.Unknown);
    }

    [Theory]
    [InlineData(OSFamily.Windows)]
    [InlineData(OSFamily.Linux)]
    [InlineData(OSFamily.MacOs)]
    public void All_Os_Families_Round_Trip(OSFamily os)
    {
        var p = MinimalLinux() with { Os = os };
        Assert.Equal(os, p.Os);
    }

    [Fact]
    public void Random_HostProfiles_Round_Trip()
    {
        for (var i = 0; i < 50; i++)
        {
            var os = Faker.PickRandom<OSFamily>();
            var arch = Faker.PickRandom<Architecture>();
            var logical = Faker.Random.Int(1, 256);
            var physical = Faker.Random.Int(1, logical);
            var total = Faker.Random.Long(1, long.MaxValue);
            var available = Faker.Random.Long(0, total);
            var p = new HostProfile
            {
                Os = os,
                Arch = arch,
                LogicalCpuCount = logical,
                PhysicalCpuCount = physical,
                TotalMemoryBytes = total,
                AvailableMemoryBytes = available,
            };
            var clone = p with { };
            Assert.Equal(p, clone);
        }
    }
}
