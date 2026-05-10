using Xunit;

namespace Tamp.Core.Tests;

public sealed class LoggerTests
{
    [Fact]
    public void Default_Minimum_Level_Is_Info()
    {
        var sw = new StringWriter();
        var log = new Logger(sw);
        Assert.Equal(LogLevel.Info, log.MinimumLevel);
    }

    [Fact]
    public void Below_Threshold_Lines_Are_Dropped()
    {
        var sw = new StringWriter();
        var log = new Logger(sw, LogLevel.Info);
        log.Trace("trace");
        log.Debug("debug");
        log.Info("info");
        var output = sw.ToString();
        Assert.DoesNotContain("trace", output);
        Assert.DoesNotContain("debug", output);
        Assert.Contains("info", output);
    }

    [Fact]
    public void At_Or_Above_Threshold_Lines_Are_Emitted()
    {
        var sw = new StringWriter();
        var log = new Logger(sw, LogLevel.Warn);
        log.Info("info");
        log.Warn("warn");
        log.Error("error");
        var output = sw.ToString();
        Assert.DoesNotContain("info", output);
        Assert.Contains("warn", output);
        Assert.Contains("error", output);
    }

    [Fact]
    public void Info_Has_No_Prefix()
    {
        var sw = new StringWriter();
        var log = new Logger(sw, LogLevel.Info);
        log.Info("plain message");
        Assert.Equal("plain message" + Environment.NewLine, sw.ToString());
    }

    [Theory]
    [InlineData(LogLevel.Trace, "[TRACE]")]
    [InlineData(LogLevel.Debug, "[DEBUG]")]
    [InlineData(LogLevel.Warn, "[WARN]")]
    [InlineData(LogLevel.Error, "[ERROR]")]
    public void Non_Info_Levels_Carry_Prefix(LogLevel level, string prefix)
    {
        var sw = new StringWriter();
        var log = new Logger(sw, LogLevel.Trace);
        var write = level switch
        {
            LogLevel.Trace => (Action<string>)log.Trace,
            LogLevel.Debug => log.Debug,
            LogLevel.Info => log.Info,
            LogLevel.Warn => log.Warn,
            LogLevel.Error => log.Error,
            _ => throw new ArgumentOutOfRangeException(nameof(level)),
        };
        write("body");
        Assert.Contains(prefix, sw.ToString());
        Assert.Contains("body", sw.ToString());
    }

    [Fact]
    public void IsEnabled_Reflects_Current_Threshold()
    {
        var log = new Logger(new StringWriter(), LogLevel.Warn);
        Assert.False(log.IsEnabled(LogLevel.Info));
        Assert.True(log.IsEnabled(LogLevel.Warn));
        Assert.True(log.IsEnabled(LogLevel.Error));
    }

    [Fact]
    public void WriteRaw_Bypasses_Level_Filter()
    {
        var sw = new StringWriter();
        var log = new Logger(sw, LogLevel.Error);
        log.WriteRaw("always");
        Assert.Contains("always", sw.ToString());
    }

    [Fact]
    public void Constructor_Throws_On_Null_Writer()
    {
        Assert.Throws<ArgumentNullException>(() => new Logger(null!));
    }

    // ---- Verbosity flag parsing ----

    [Theory]
    [InlineData("quiet", LogLevel.Error)]
    [InlineData("Q", LogLevel.Error)]
    [InlineData("minimal", LogLevel.Warn)]
    [InlineData("M", LogLevel.Warn)]
    [InlineData("normal", LogLevel.Info)]
    [InlineData("verbose", LogLevel.Debug)]
    [InlineData("V", LogLevel.Debug)]
    [InlineData("diagnostic", LogLevel.Trace)]
    public void ParseVerbosity_Accepts_Common_Forms(string input, LogLevel expected)
    {
        Assert.Equal(expected, TampBuild.ParseVerbosity(input));
    }

    [Fact]
    public void ParseVerbosity_Throws_On_Garbage()
    {
        Assert.Throws<InvalidOperationException>(() => TampBuild.ParseVerbosity("trousers"));
    }

    // ---- ParseInvocation integration ----

    [Fact]
    public void Verbosity_Flag_With_Value_Is_Parsed()
    {
        var targets = new Dictionary<string, TargetSpec>(StringComparer.Ordinal)
        {
            ["X"] = new TargetSpec { Name = "X" },
        };
        var (_, _, _, _, v) = TampBuild.ParseInvocation(["X", "--verbosity", "verbose"], targets);
        Assert.Equal(LogLevel.Debug, v);
    }

    [Fact]
    public void Verbosity_Flag_With_Equals_Is_Parsed()
    {
        var targets = new Dictionary<string, TargetSpec>(StringComparer.Ordinal)
        {
            ["X"] = new TargetSpec { Name = "X" },
        };
        var (_, _, _, _, v) = TampBuild.ParseInvocation(["X", "--verbosity=quiet"], targets);
        Assert.Equal(LogLevel.Error, v);
    }

    [Fact]
    public void Quiet_Shortcut_Maps_To_Error_Level()
    {
        var targets = new Dictionary<string, TargetSpec>(StringComparer.Ordinal)
        {
            ["X"] = new TargetSpec { Name = "X" },
        };
        var (_, _, _, _, v) = TampBuild.ParseInvocation(["X", "--quiet"], targets);
        Assert.Equal(LogLevel.Error, v);
    }

    [Fact]
    public void Verbose_Shortcut_Maps_To_Debug_Level()
    {
        var targets = new Dictionary<string, TargetSpec>(StringComparer.Ordinal)
        {
            ["X"] = new TargetSpec { Name = "X" },
        };
        var (_, _, _, _, v) = TampBuild.ParseInvocation(["X", "--verbose"], targets);
        Assert.Equal(LogLevel.Debug, v);
    }

    [Fact]
    public void Diagnostic_Shortcut_Maps_To_Trace_Level()
    {
        var targets = new Dictionary<string, TargetSpec>(StringComparer.Ordinal)
        {
            ["X"] = new TargetSpec { Name = "X" },
        };
        var (_, _, _, _, v) = TampBuild.ParseInvocation(["X", "--diagnostic"], targets);
        Assert.Equal(LogLevel.Trace, v);
    }

    [Fact]
    public void Default_Verbosity_Is_Info_When_Not_Specified()
    {
        var targets = new Dictionary<string, TargetSpec>(StringComparer.Ordinal)
        {
            ["X"] = new TargetSpec { Name = "X" },
        };
        var (_, _, _, _, v) = TampBuild.ParseInvocation(["X"], targets);
        Assert.Equal(LogLevel.Info, v);
    }

    // ---- Executor integration ----

    [Fact]
    public void Executor_Default_Verbosity_Shows_Target_Status()
    {
        var sw = new StringWriter();
        var graph = new TargetGraph(new Dictionary<string, TargetSpec>(StringComparer.Ordinal)
        {
            ["X"] = new TargetSpec { Name = "X", Actions = new Action[] { () => { } } },
        });
        new Executor(graph, ExecutionMode.Run, sw, LogLevel.Info).Run("X");
        Assert.Contains("==> X", sw.ToString());
    }

    [Fact]
    public void Executor_Quiet_Verbosity_Still_Shows_Build_Summary()
    {
        var sw = new StringWriter();
        var graph = new TargetGraph(new Dictionary<string, TargetSpec>(StringComparer.Ordinal)
        {
            ["X"] = new TargetSpec { Name = "X", Actions = new Action[] { () => { } } },
        });
        // Build summary uses WriteRaw so it appears even at LogLevel.Error.
        new Executor(graph, ExecutionMode.Run, sw, LogLevel.Error).Run("X");
        Assert.Contains("Build Summary", sw.ToString());
    }

    [Fact]
    public void Executor_Exposes_Logger_For_Build_Script_Use()
    {
        var graph = new TargetGraph(new Dictionary<string, TargetSpec>(StringComparer.Ordinal)
        {
            ["X"] = new TargetSpec { Name = "X" },
        });
        var exec = new Executor(graph, ExecutionMode.Plan);
        Assert.NotNull(exec.Log);
    }
}
