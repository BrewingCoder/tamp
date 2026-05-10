using System.Runtime.InteropServices;
using Xunit;

namespace Tamp.Core.Tests;

public sealed class CaptureResultTests
{
    private static bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    private static CommandPlan EchoPlan(string text)
    {
        if (IsWindows)
        {
            return new CommandPlan
            {
                Executable = "cmd.exe",
                Arguments = ["/c", "echo", text],
            };
        }
        return new CommandPlan
        {
            Executable = "/bin/sh",
            Arguments = ["-c", $"echo {text}"],
        };
    }

    private static CommandPlan StderrEchoPlan(string text)
    {
        if (IsWindows)
        {
            // cmd echo to stderr
            return new CommandPlan
            {
                Executable = "cmd.exe",
                Arguments = ["/c", $"echo {text} 1>&2"],
            };
        }
        return new CommandPlan
        {
            Executable = "/bin/sh",
            Arguments = ["-c", $"echo {text} >&2"],
        };
    }

    private static CommandPlan ExitPlan(int code)
    {
        if (IsWindows)
        {
            return new CommandPlan
            {
                Executable = "cmd.exe",
                Arguments = ["/c", $"exit {code}"],
            };
        }
        return new CommandPlan
        {
            Executable = "/bin/sh",
            Arguments = ["-c", $"exit {code}"],
        };
    }

    [Fact]
    public void Capture_Returns_Stdout_Lines()
    {
        var result = ProcessRunner.Capture(EchoPlan("hello world"));
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("hello world", result.StdoutText);
    }

    [Fact]
    public void Capture_Distinguishes_Stdout_From_Stderr()
    {
        var result = ProcessRunner.Capture(StderrEchoPlan("oops"));
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("oops", result.StderrText);
        Assert.DoesNotContain("oops", result.StdoutText);
    }

    [Fact]
    public void Capture_Reports_Exit_Code_For_Failed_Process()
    {
        var result = ProcessRunner.Capture(ExitPlan(7));
        Assert.Equal(7, result.ExitCode);
        Assert.True(result.Failed);
    }

    [Fact]
    public void ThrowOnFailure_Throws_On_NonZero_Exit()
    {
        var result = ProcessRunner.Capture(ExitPlan(2));
        var ex = Assert.Throws<ProcessExecutionException>(() => result.ThrowOnFailure());
        Assert.Equal(2, ex.ExitCode);
    }

    [Fact]
    public void ThrowOnFailure_Returns_Self_On_Success_For_Fluent_Chaining()
    {
        var result = ProcessRunner.Capture(EchoPlan("ok"));
        Assert.Same(result, result.ThrowOnFailure());
    }

    [Fact]
    public void Capture_Lines_Preserve_Source_Stream_Tagging()
    {
        var result = ProcessRunner.Capture(EchoPlan("hi"));
        Assert.All(result.Lines.Where(l => l.Text.Contains("hi")),
            l => Assert.Equal(OutputType.Stdout, l.Type));
    }

    [Fact]
    public void Capture_With_Also_Writers_Tees_Output()
    {
        var stdoutSink = new StringWriter();
        var result = ProcessRunner.Capture(EchoPlan("teed"), alsoStdout: stdoutSink);
        Assert.Contains("teed", result.StdoutText);
        Assert.Contains("teed", stdoutSink.ToString());
    }

    [Fact]
    public void Capture_Throws_On_Null_Plan()
    {
        Assert.Throws<ArgumentNullException>(() => ProcessRunner.Capture(null!));
    }

    [Fact]
    public void OutputLine_Equality_Is_Value_Based()
    {
        var a = new OutputLine(OutputType.Stdout, "x");
        var b = new OutputLine(OutputType.Stdout, "x");
        var c = new OutputLine(OutputType.Stderr, "x");
        Assert.Equal(a, b);
        Assert.NotEqual(a, c);
    }
}
