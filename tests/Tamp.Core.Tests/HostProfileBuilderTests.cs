using System.Runtime.InteropServices;
using Xunit;

namespace Tamp.Core.Tests;

public sealed class HostProfileBuilderTests
{
    [Fact]
    public void Build_Returns_Non_Null_Profile()
    {
        var p = HostProfileBuilder.Build();
        Assert.NotNull(p);
    }

    [Fact]
    public void Build_Reports_Logical_Cpu_Count_Greater_Than_Zero()
    {
        var p = HostProfileBuilder.Build();
        Assert.True(p.LogicalCpuCount > 0,
            $"LogicalCpuCount={p.LogicalCpuCount} but Environment.ProcessorCount={Environment.ProcessorCount}");
    }

    [Fact]
    public void Build_Reports_Physical_Cpu_Count_Greater_Than_Zero()
    {
        var p = HostProfileBuilder.Build();
        Assert.True(p.PhysicalCpuCount > 0);
    }

    [Fact]
    public void Build_Reports_Total_Memory_Greater_Than_Zero()
    {
        var p = HostProfileBuilder.Build();
        Assert.True(p.TotalMemoryBytes > 0);
    }

    [Fact]
    public void Build_Reports_Available_Memory_Non_Negative_And_Bounded()
    {
        var p = HostProfileBuilder.Build();
        Assert.True(p.AvailableMemoryBytes >= 0);
        Assert.True(p.AvailableMemoryBytes <= p.TotalMemoryBytes);
    }

    [Fact]
    public void Build_Reports_Os_Family_Matching_RuntimeInformation()
    {
        var p = HostProfileBuilder.Build();
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            Assert.Equal(OSFamily.Windows, p.Os);
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            Assert.Equal(OSFamily.Linux, p.Os);
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            Assert.Equal(OSFamily.MacOs, p.Os);
        else
            Assert.Equal(OSFamily.Unknown, p.Os);
    }

    [Fact]
    public void Build_Reports_Process_Architecture()
    {
        var p = HostProfileBuilder.Build();
        Assert.Equal(RuntimeInformation.ProcessArchitecture, p.Arch);
    }

    [Fact]
    public void Per_Os_Info_Records_Are_Set_Only_For_Their_Own_Os()
    {
        var p = HostProfileBuilder.Build();
        switch (p.Os)
        {
            case OSFamily.Windows:
                Assert.NotNull(p.Windows);
                Assert.Null(p.Linux);
                Assert.Null(p.MacOs);
                break;
            case OSFamily.Linux:
                Assert.Null(p.Windows);
                Assert.NotNull(p.Linux);
                Assert.Null(p.MacOs);
                break;
            case OSFamily.MacOs:
                Assert.Null(p.Windows);
                Assert.Null(p.Linux);
                Assert.NotNull(p.MacOs);
                break;
            default:
                Assert.Null(p.Windows);
                Assert.Null(p.Linux);
                Assert.Null(p.MacOs);
                break;
        }
    }

    [Fact]
    public void Build_Reports_Stable_Output_Across_Repeated_Calls()
    {
        // The host doesn't change between two close-together Builds, so any
        // detection that depended on transient state would surface as an
        // inconsistency here. AvailableMemoryBytes is the only legitimately
        // varying field; everything else should be identical.
        var a = HostProfileBuilder.Build();
        var b = HostProfileBuilder.Build();
        Assert.Equal(a.Os, b.Os);
        Assert.Equal(a.Arch, b.Arch);
        Assert.Equal(a.LogicalCpuCount, b.LogicalCpuCount);
        Assert.Equal(a.PhysicalCpuCount, b.PhysicalCpuCount);
        Assert.Equal(a.TotalMemoryBytes, b.TotalMemoryBytes);
        Assert.Equal(a.InContainer, b.InContainer);
        Assert.Equal(a.InWsl, b.InWsl);
        Assert.Equal(a.Ci, b.Ci);
    }

    // ---- CI vendor detection (env-controlled, deterministic) ----

    [Theory]
    [InlineData("GITHUB_ACTIONS", "true", CiVendor.GitHubActions)]
    [InlineData("GITHUB_ACTIONS", "TRUE", CiVendor.GitHubActions)]
    [InlineData("TF_BUILD", "True", CiVendor.AzureDevOps)]
    [InlineData("GITLAB_CI", "true", CiVendor.GitLabCi)]
    [InlineData("APPVEYOR", "True", CiVendor.AppVeyor)]
    [InlineData("CIRCLECI", "true", CiVendor.CircleCI)]
    [InlineData("BUILDKITE", "true", CiVendor.Buildkite)]
    [InlineData("TRAVIS", "true", CiVendor.Travis)]
    public void Detect_Ci_Vendor_From_Truthy_Env_Var(string varName, string varValue, CiVendor expected)
    {
        string? Env(string name) => name == varName ? varValue : null;
        Assert.Equal(expected, HostProfileBuilder.DetectCiVendor(Env));
    }

    [Theory]
    [InlineData("TEAMCITY_VERSION", "2024.03", CiVendor.TeamCity)]
    [InlineData("JENKINS_URL", "https://ci.example.com/", CiVendor.Jenkins)]
    public void Detect_Ci_Vendor_From_Presence_Env_Var(string varName, string varValue, CiVendor expected)
    {
        string? Env(string name) => name == varName ? varValue : null;
        Assert.Equal(expected, HostProfileBuilder.DetectCiVendor(Env));
    }

    [Fact]
    public void Generic_CI_True_Maps_To_Unknown_When_No_Specific_Vendor()
    {
        string? Env(string name) => name == "CI" ? "true" : null;
        Assert.Equal(CiVendor.Unknown, HostProfileBuilder.DetectCiVendor(Env));
    }

    [Fact]
    public void No_Ci_Env_Vars_Returns_Null()
    {
        string? Env(string name) => null;
        Assert.Null(HostProfileBuilder.DetectCiVendor(Env));
    }

    [Theory]
    [InlineData("false")]
    [InlineData("0")]
    [InlineData("")]
    [InlineData("no")]
    public void Falsy_Or_Empty_CI_Var_Is_Not_Treated_As_CI(string value)
    {
        string? Env(string name) => name == "CI" ? value : null;
        // "no" is technically rejected by Truthy(); empty is rejected; "false"/"0" rejected.
        Assert.Null(HostProfileBuilder.DetectCiVendor(Env));
    }

    [Fact]
    public void Specific_Vendor_Wins_Over_Generic_CI_True()
    {
        string? Env(string name) => name switch
        {
            "CI" => "true",
            "GITHUB_ACTIONS" => "true",
            _ => null,
        };
        Assert.Equal(CiVendor.GitHubActions, HostProfileBuilder.DetectCiVendor(Env));
    }

    // ---- Container detection ----

    [Fact]
    public void DotNet_Container_Env_Var_Triggers_InContainer()
    {
        string? Env(string name) => name == "DOTNET_RUNNING_IN_CONTAINER" ? "true" : null;
        Assert.True(HostProfileBuilder.DetectInContainer(Env));
    }

    [Fact]
    public void DotNet_Container_Env_Var_Case_Insensitive()
    {
        string? Env(string name) => name == "DOTNET_RUNNING_IN_CONTAINER" ? "TRUE" : null;
        Assert.True(HostProfileBuilder.DetectInContainer(Env));
    }

    [Fact]
    public void DotNet_Container_Env_Var_Falsy_Does_Not_Trigger()
    {
        string? Env(string name) => name == "DOTNET_RUNNING_IN_CONTAINER" ? "false" : null;
        // On non-Linux this returns false; on Linux this depends on whether
        // /.dockerenv exists. The test machine for v0 is macOS, so we expect
        // false here. This test is pinned to macOS to keep it deterministic.
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            Assert.False(HostProfileBuilder.DetectInContainer(Env));
    }

    // ---- OS family detection ----

    [Fact]
    public void Os_Family_Detection_Matches_Process_OS()
    {
        var os = HostProfileBuilder.DetectOSFamily();
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) Assert.Equal(OSFamily.Windows, os);
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) Assert.Equal(OSFamily.Linux, os);
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) Assert.Equal(OSFamily.MacOs, os);
        else Assert.Equal(OSFamily.Unknown, os);
    }

    [Fact]
    public void Wsl_Detection_Returns_False_On_Non_Linux()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) return;  // Skip on Linux.
        Assert.False(HostProfileBuilder.DetectInWsl(OSFamily.Windows));
        Assert.False(HostProfileBuilder.DetectInWsl(OSFamily.MacOs));
        Assert.False(HostProfileBuilder.DetectInWsl(OSFamily.Unknown));
    }

    [Fact]
    public void Cgroup_Detection_Returns_Null_On_Non_Linux()
    {
        Assert.Null(HostProfileBuilder.DetectCgroupLimits(OSFamily.Windows));
        Assert.Null(HostProfileBuilder.DetectCgroupLimits(OSFamily.MacOs));
        Assert.Null(HostProfileBuilder.DetectCgroupLimits(OSFamily.Unknown));
    }
}
