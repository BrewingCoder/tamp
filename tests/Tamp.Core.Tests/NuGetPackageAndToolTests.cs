using System.Reflection;
using System.Runtime.InteropServices;
using Xunit;

namespace Tamp.Core.Tests;

public sealed class NuGetPackageAndToolTests : IDisposable
{
    private readonly string _scratch;

    public NuGetPackageAndToolTests()
    {
        _scratch = Path.Combine(Path.GetTempPath(), "tamp-tool-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_scratch);
    }

    public void Dispose()
    {
        try { Directory.Delete(_scratch, recursive: true); } catch { }
    }

    // ---- Tool ----

    [Fact]
    public void Tool_Plan_Sets_Executable_And_Args()
    {
        var exe = AbsolutePath.Create(_scratch);
        var t = new Tool(exe);
        var plan = t.Plan("--help");
        Assert.Equal(exe.Value, plan.Executable);
        Assert.Equal(["--help"], plan.Arguments);
    }

    [Fact]
    public void Tool_Plan_Accepts_Enumerable_Arguments()
    {
        var t = new Tool(AbsolutePath.Create(_scratch));
        var args = new[] { "build", "--configuration", "Release" };
        var plan = t.Plan((IEnumerable<string>)args);
        Assert.Equal(args, plan.Arguments);
    }

    [Fact]
    public void Tool_Carries_WorkingDirectory_Through_To_Plan()
    {
        var t = new Tool(AbsolutePath.Create(_scratch), workingDirectory: "/tmp/work");
        var plan = t.Plan("x");
        Assert.Equal("/tmp/work", plan.WorkingDirectory);
    }

    [Fact]
    public void Tool_Throws_On_Null_Executable()
    {
        Assert.Throws<ArgumentNullException>(() => new Tool(null!));
    }

    // ---- NuGetPackageAttribute — wrong member type ----

    [Fact]
    public void Throws_When_Decorating_Non_Tool_Member_Type()
    {
        var attr = new NuGetPackageAttribute("dotnet-format");
        // Fake a member-info using any string property; the attribute's
        // GetValue checks the supplied memberType.
        var fakeMember = typeof(NuGetPackageAndToolTests).GetMembers().First();
        Assert.Throws<InvalidOperationException>(
            () => attr.GetValue(fakeMember, typeof(string)));
    }

    // ---- LocalCachePath path ----

    [Fact]
    public void LocalCachePath_Resolves_Without_Touching_Network()
    {
        var ext = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ".exe" : string.Empty;
        var fakeExe = AbsolutePath.Create(Path.Combine(_scratch, "myfaketool" + ext));
        File.WriteAllText(fakeExe.Value, "fake");
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            File.SetUnixFileMode(fakeExe.Value, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

        var attr = new NuGetPackageAttribute("myfaketool") { LocalCachePath = _scratch };
        var fakeMember = typeof(NuGetPackageAndToolTests).GetMembers().First();
        var result = attr.GetValue(fakeMember, typeof(Tool));
        var tool = Assert.IsType<Tool>(result);
        Assert.Equal(fakeExe.Value, tool.Executable.Value);
    }

    [Fact]
    public void LocalCachePath_Throws_When_Executable_Missing()
    {
        var attr = new NuGetPackageAttribute("definitely-missing") { LocalCachePath = _scratch };
        var fakeMember = typeof(NuGetPackageAndToolTests).GetMembers().First();
        Assert.Throws<InvalidOperationException>(
            () => attr.GetValue(fakeMember, typeof(Tool)));
    }

    [Fact]
    public void Custom_ExecutableName_Overrides_PackageId_For_Lookup()
    {
        var ext = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ".exe" : string.Empty;
        var fakeExe = AbsolutePath.Create(Path.Combine(_scratch, "actual-binary" + ext));
        File.WriteAllText(fakeExe.Value, "fake");

        var attr = new NuGetPackageAttribute("the-package-name")
        {
            LocalCachePath = _scratch,
            ExecutableName = "actual-binary",
        };
        var fakeMember = typeof(NuGetPackageAndToolTests).GetMembers().First();
        var tool = Assert.IsType<Tool>(attr.GetValue(fakeMember, typeof(Tool)));
        Assert.Equal(fakeExe.Value, tool.Executable.Value);
    }

    // ---- UseSystemPath ----

    [Fact]
    public void UseSystemPath_Resolves_Real_Tool_From_PATH()
    {
        // dotnet itself must be on PATH for our build environment to work.
        var attr = new NuGetPackageAttribute("dotnet") { UseSystemPath = true };
        var fakeMember = typeof(NuGetPackageAndToolTests).GetMembers().First();
        var tool = Assert.IsType<Tool>(attr.GetValue(fakeMember, typeof(Tool)));
        Assert.True(tool.Executable.FileExists(), $"dotnet should resolve to an existing path; got {tool.Executable}");
    }

    [Fact]
    public void UseSystemPath_Throws_For_Tool_Not_On_PATH()
    {
        var attr = new NuGetPackageAttribute("definitely-not-on-path-12345") { UseSystemPath = true };
        var fakeMember = typeof(NuGetPackageAndToolTests).GetMembers().First();
        Assert.Throws<InvalidOperationException>(
            () => attr.GetValue(fakeMember, typeof(Tool)));
    }

    // ---- Property defaults ----

    [Fact]
    public void Default_Version_Is_Null()
    {
        var attr = new NuGetPackageAttribute("x");
        Assert.Null(attr.Version);
    }

    [Fact]
    public void Default_ExecutableName_Is_Null_So_Resolver_Uses_PackageId()
    {
        var attr = new NuGetPackageAttribute("x");
        Assert.Null(attr.ExecutableName);
    }

    [Fact]
    public void Constructor_Throws_On_Null_PackageId()
    {
        Assert.Throws<ArgumentNullException>(() => new NuGetPackageAttribute(null!));
    }
}
