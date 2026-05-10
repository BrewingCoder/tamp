namespace Tamp;

/// <summary>Verbosity level controlling what the logger writes.</summary>
public enum LogLevel
{
    /// <summary>Per-step internal trace; chatty.</summary>
    Trace = 0,
    /// <summary>Plan-generation, skip-reason, deeper detail.</summary>
    Debug = 1,
    /// <summary>Default: target start/end, build summary.</summary>
    Info = 2,
    /// <summary>Recoverable issues, deprecations, soft-failure paths.</summary>
    Warn = 3,
    /// <summary>Errors that abort the build or a target.</summary>
    Error = 4,
}

/// <summary>
/// Build-script logger. Filters by <see cref="MinimumLevel"/> and writes
/// through the supplied <see cref="TextWriter"/> (which the executor wraps
/// in a <see cref="RedactingTextWriter"/> so secrets never reach the
/// downstream sink unredacted).
/// </summary>
/// <remarks>
/// Deliberately small: no structured-event vocabulary, no external deps.
/// A future <c>Tamp.Logging.Serilog</c> package could implement a sink
/// that adapts these calls into Serilog events for consumers who want
/// the structured pipeline.
/// </remarks>
public sealed class Logger
{
    private readonly TextWriter _writer;

    public Logger(TextWriter writer, LogLevel minimumLevel = LogLevel.Info)
    {
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));
        MinimumLevel = minimumLevel;
    }

    /// <summary>Lowest level that will be written; messages below are dropped.</summary>
    public LogLevel MinimumLevel { get; set; }

    public bool IsEnabled(LogLevel level) => level >= MinimumLevel;

    public void Trace(string message) => Write(LogLevel.Trace, message);
    public void Debug(string message) => Write(LogLevel.Debug, message);
    public void Info(string message) => Write(LogLevel.Info, message);
    public void Warn(string message) => Write(LogLevel.Warn, message);
    public void Error(string message) => Write(LogLevel.Error, message);

    /// <summary>Raw line (no level prefix). Used for build summary, banners, and plan output.</summary>
    public void WriteRaw(string message)
    {
        _writer.WriteLine(message);
    }

    /// <summary>Raw blank line.</summary>
    public void WriteRaw() => _writer.WriteLine();

    public void Flush() => _writer.Flush();

    private void Write(LogLevel level, string message)
    {
        if (!IsEnabled(level)) return;
        var prefix = level switch
        {
            LogLevel.Trace => "[TRACE]",
            LogLevel.Debug => "[DEBUG]",
            LogLevel.Info => "",
            LogLevel.Warn => "[WARN]",
            LogLevel.Error => "[ERROR]",
            _ => $"[{level}]",
        };
        _writer.WriteLine(string.IsNullOrEmpty(prefix) ? message : $"{prefix} {message}");
    }
}
