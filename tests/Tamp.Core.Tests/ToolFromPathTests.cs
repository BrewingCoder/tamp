using Xunit;

namespace Tamp.Core.Tests;

public sealed class ToolFromPathTests : IDisposable
{
    private readonly string _scratch;
    private readonly string _originalPath;

    public ToolFromPathTests()
    {
        _scratch = Path.Combine(Path.GetTempPath(), "tamp-frompath-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_scratch);
        _originalPath = Environment.GetEnvironmentVariable("PATH") ?? "";
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("PATH", _originalPath);
        try { Directory.Delete(_scratch, recursive: true); } catch { }
    }

    private string MkExe(string name, string? ext = null)
    {
        // On non-Windows we drop a file with no extension. On Windows tests, the resolver
        // probes extensions in order; we use ".cmd" by default since that's first on Windows.
        ext ??= OperatingSystem.IsWindows() ? ".cmd" : "";
        var path = Path.Combine(_scratch, name + ext);
        File.WriteAllText(path, "echo hi");
        return path;
    }

    // ---- TryFromPath ----

    [Fact]
    public void TryFromPath_Finds_Executable_On_PATH()
    {
        var expected = MkExe("tamp-fake-tool");
        Environment.SetEnvironmentVariable("PATH", _scratch);
        var tool = Tool.TryFromPath("tamp-fake-tool");
        Assert.NotNull(tool);
        Assert.Equal(expected, tool!.Executable.Value);
    }

    [Fact]
    public void TryFromPath_Returns_Null_When_Not_Found()
    {
        Environment.SetEnvironmentVariable("PATH", _scratch);
        Assert.Null(Tool.TryFromPath("tamp-no-such-tool-" + Guid.NewGuid().ToString("N")[..8]));
    }

    [Fact]
    public void TryFromPath_Rejects_Empty_Name()
    {
        Assert.Throws<ArgumentException>(() => Tool.TryFromPath(""));
        Assert.Throws<ArgumentException>(() => Tool.TryFromPath("  "));
        Assert.Throws<ArgumentException>(() => Tool.TryFromPath(null!));
    }

    [Fact]
    public void TryFromPath_Passes_Working_Directory_Through()
    {
        MkExe("tamp-fake-tool");
        Environment.SetEnvironmentVariable("PATH", _scratch);
        var tool = Tool.TryFromPath("tamp-fake-tool", workingDirectory: "/tmp");
        Assert.NotNull(tool);
        Assert.Equal("/tmp", tool!.WorkingDirectory);
    }

    [Fact]
    public void TryFromPath_Honors_PATH_Order()
    {
        // Two scratch dirs each with the same-name shim — first dir on PATH wins.
        var first = Path.Combine(Path.GetTempPath(), "tamp-frompath-first-" + Guid.NewGuid().ToString("N"));
        var second = Path.Combine(Path.GetTempPath(), "tamp-frompath-second-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(first);
        Directory.CreateDirectory(second);
        try
        {
            var ext = OperatingSystem.IsWindows() ? ".cmd" : "";
            var firstExe = Path.Combine(first, "tamp-fake" + ext);
            var secondExe = Path.Combine(second, "tamp-fake" + ext);
            File.WriteAllText(firstExe, "first");
            File.WriteAllText(secondExe, "second");

            var sep = OperatingSystem.IsWindows() ? ';' : ':';
            Environment.SetEnvironmentVariable("PATH", $"{first}{sep}{second}");
            var tool = Tool.TryFromPath("tamp-fake");
            Assert.NotNull(tool);
            Assert.Equal(firstExe, tool!.Executable.Value);
        }
        finally
        {
            try { Directory.Delete(first, recursive: true); } catch { }
            try { Directory.Delete(second, recursive: true); } catch { }
        }
    }

    // ---- FromPath (throwing variant) ----

    [Fact]
    public void FromPath_Throws_When_Not_Found()
    {
        Environment.SetEnvironmentVariable("PATH", _scratch);
        var ex = Assert.Throws<InvalidOperationException>(
            () => Tool.FromPath("tamp-missing-" + Guid.NewGuid().ToString("N")[..8]));
        Assert.Contains("Could not find", ex.Message);
        Assert.Contains("PATH", ex.Message);
    }

    [Fact]
    public void FromPath_Returns_Tool_When_Found()
    {
        MkExe("tamp-fake-tool");
        Environment.SetEnvironmentVariable("PATH", _scratch);
        var tool = Tool.FromPath("tamp-fake-tool");
        Assert.NotNull(tool);
    }

    // ---- FromPath attribute ----

    [Fact]
    public void FromPathAttribute_Rejects_Empty_Name()
    {
        Assert.Throws<ArgumentException>(() => new FromPathAttribute(""));
        Assert.Throws<ArgumentException>(() => new FromPathAttribute("  "));
        Assert.Throws<ArgumentException>(() => new FromPathAttribute(null!));
    }

    [Fact]
    public void FromPathAttribute_Optional_False_Throws_When_Missing()
    {
        Environment.SetEnvironmentVariable("PATH", _scratch);
        var attr = new FromPathAttribute("tamp-missing-" + Guid.NewGuid().ToString("N")[..8]);
        Assert.False(attr.Optional);
        var memberInfo = typeof(ToolFromPathTests).GetProperty(nameof(_scratch))
                         ?? (System.Reflection.MemberInfo)typeof(ToolFromPathTests).GetField(nameof(_scratch),
                             System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        Assert.Throws<InvalidOperationException>(() => attr.GetValue(memberInfo, typeof(Tool)));
    }

    [Fact]
    public void FromPathAttribute_Optional_True_Injects_Null_When_Missing()
    {
        Environment.SetEnvironmentVariable("PATH", _scratch);
        var attr = new FromPathAttribute("tamp-missing-" + Guid.NewGuid().ToString("N")[..8]) { Optional = true };
        var memberInfo = typeof(ToolFromPathTests).GetField(nameof(_scratch),
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        Assert.Null(attr.GetValue(memberInfo, typeof(Tool)));
    }

    [Fact]
    public void FromPathAttribute_Returns_Tool_When_Found()
    {
        MkExe("tamp-fake-tool");
        Environment.SetEnvironmentVariable("PATH", _scratch);
        var attr = new FromPathAttribute("tamp-fake-tool");
        var memberInfo = typeof(ToolFromPathTests).GetField(nameof(_scratch),
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        var result = attr.GetValue(memberInfo, typeof(Tool));
        Assert.IsType<Tool>(result);
    }

    // ---- TryFromNodeModules ----

    [Fact]
    public void TryFromNodeModules_Finds_Executable_In_Bin()
    {
        var root = AbsolutePath.Create(_scratch);
        var binDir = (root / "node_modules" / ".bin").Value;
        Directory.CreateDirectory(binDir);
        var ext = OperatingSystem.IsWindows() ? ".cmd" : "";
        var exePath = Path.Combine(binDir, "turbo" + ext);
        File.WriteAllText(exePath, "x");

        var tool = Tool.TryFromNodeModules("turbo", root);
        Assert.NotNull(tool);
        Assert.Equal(exePath, tool!.Executable.Value);
    }

    [Fact]
    public void TryFromNodeModules_Returns_Null_When_Missing()
    {
        var root = AbsolutePath.Create(_scratch);
        Assert.Null(Tool.TryFromNodeModules("turbo", root));
    }

    [Fact]
    public void TryFromNodeModules_Default_Working_Directory_Is_Project_Root()
    {
        var root = AbsolutePath.Create(_scratch);
        var binDir = (root / "node_modules" / ".bin").Value;
        Directory.CreateDirectory(binDir);
        var ext = OperatingSystem.IsWindows() ? ".cmd" : "";
        File.WriteAllText(Path.Combine(binDir, "turbo" + ext), "x");

        var tool = Tool.TryFromNodeModules("turbo", root);
        Assert.NotNull(tool);
        Assert.Equal(root.Value, tool!.WorkingDirectory);
    }

    [Fact]
    public void TryFromNodeModules_Rejects_Empty_Name()
    {
        var root = AbsolutePath.Create(_scratch);
        Assert.Throws<ArgumentException>(() => Tool.TryFromNodeModules("", root));
    }

    [Fact]
    public void TryFromNodeModules_Rejects_Null_Root()
    {
        Assert.Throws<ArgumentNullException>(() => Tool.TryFromNodeModules("turbo", null!));
    }

    [Fact]
    public void FromNodeModules_Throws_With_Yarn_Install_Hint()
    {
        var root = AbsolutePath.Create(_scratch);
        var ex = Assert.Throws<InvalidOperationException>(() => Tool.FromNodeModules("turbo", root));
        Assert.Contains("yarn install", ex.Message);
    }
}
