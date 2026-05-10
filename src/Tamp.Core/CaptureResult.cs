namespace Tamp;

/// <summary>Which standard stream a captured line came from.</summary>
public enum OutputType
{
    Stdout,
    Stderr,
}

/// <summary>A single line of captured output with its source stream.</summary>
public sealed record OutputLine(OutputType Type, string Text);

/// <summary>
/// Result of <see cref="ProcessRunner.Capture(CommandPlan, TextWriter?, TextWriter?)"/>:
/// the child's exit code plus every stdout/stderr line in arrival order.
/// </summary>
public sealed class CaptureResult
{
    public CaptureResult(int exitCode, IReadOnlyList<OutputLine> lines)
    {
        ExitCode = exitCode;
        Lines = lines ?? throw new ArgumentNullException(nameof(lines));
    }

    public int ExitCode { get; }
    public IReadOnlyList<OutputLine> Lines { get; }

    /// <summary>Stdout lines only, in arrival order.</summary>
    public IEnumerable<string> StdoutLines
        => Lines.Where(l => l.Type == OutputType.Stdout).Select(l => l.Text);

    /// <summary>Stderr lines only, in arrival order.</summary>
    public IEnumerable<string> StderrLines
        => Lines.Where(l => l.Type == OutputType.Stderr).Select(l => l.Text);

    /// <summary>Stdout joined with <see cref="Environment.NewLine"/>.</summary>
    public string StdoutText => string.Join(Environment.NewLine, StdoutLines);

    /// <summary>Stderr joined with <see cref="Environment.NewLine"/>.</summary>
    public string StderrText => string.Join(Environment.NewLine, StderrLines);

    /// <summary>True if the process exited with a non-zero code.</summary>
    public bool Failed => ExitCode != 0;

    /// <summary>
    /// Throw <see cref="ProcessExecutionException"/> if the process failed.
    /// Returns this for fluent use.
    /// </summary>
    public CaptureResult ThrowOnFailure()
    {
        if (!Failed) return this;
        throw new ProcessExecutionException(ExitCode, StderrText.Length > 0 ? StderrText : StdoutText);
    }
}

/// <summary>Thrown by <see cref="CaptureResult.ThrowOnFailure"/>.</summary>
public sealed class ProcessExecutionException : Exception
{
    public ProcessExecutionException(int exitCode, string output)
        : base($"Process exited with code {exitCode}.{(string.IsNullOrEmpty(output) ? "" : " Output: " + output)}")
    {
        ExitCode = exitCode;
        Output = output;
    }

    public int ExitCode { get; }
    public string Output { get; }
}
