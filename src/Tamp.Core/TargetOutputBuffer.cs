using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Tamp;

/// <summary>
/// Per-target ring buffer of recent output lines. The Executor instantiates
/// one of these around each target's invocation; merged stdout+stderr is
/// fed through <see cref="CapturingTextWriter"/> so the last N lines stay
/// in memory at all times. On target failure the buffer is drained into the
/// <see cref="TargetFailureDetail.OutputTail"/> payload; on success the
/// content is discarded.
/// </summary>
internal sealed class TargetOutputBuffer
{
    private readonly Queue<string> _lines;
    private readonly int _capacity;
    private readonly object _lock = new();

    public TargetOutputBuffer(int capacity = 50)
    {
        if (capacity < 1) throw new System.ArgumentOutOfRangeException(nameof(capacity), "capacity must be >= 1");
        _capacity = capacity;
        _lines = new Queue<string>(capacity);
    }

    /// <summary>Append one line (the trailing newline is stripped by the writer).</summary>
    public void Append(string line)
    {
        lock (_lock)
        {
            _lines.Enqueue(line);
            while (_lines.Count > _capacity) _lines.Dequeue();
        }
    }

    /// <summary>Return the buffer's current contents (oldest first) and clear it.</summary>
    public IReadOnlyList<string> Drain()
    {
        lock (_lock)
        {
            var snapshot = _lines.ToArray();
            _lines.Clear();
            return snapshot;
        }
    }
}

/// <summary>
/// <see cref="TextWriter"/> wrapper that mirrors every write to an inner
/// writer (so existing human-readable output paths keep working) and
/// line-buffers a copy into a <see cref="TargetOutputBuffer"/>. Single-
/// threaded by contract — instantiate one per target inside the Executor
/// loop, discard at target end.
/// </summary>
internal sealed class CapturingTextWriter : TextWriter
{
    private readonly TextWriter _inner;
    private readonly TargetOutputBuffer _buffer;
    private readonly StringBuilder _pending = new();

    public CapturingTextWriter(TextWriter inner, TargetOutputBuffer buffer)
    {
        _inner = inner ?? throw new System.ArgumentNullException(nameof(inner));
        _buffer = buffer ?? throw new System.ArgumentNullException(nameof(buffer));
    }

    public override Encoding Encoding => _inner.Encoding;

    public override void Write(char value)
    {
        _inner.Write(value);
        AbsorbChar(value);
    }

    public override void Write(string? value)
    {
        if (value is null) return;
        _inner.Write(value);
        AbsorbString(value);
    }

    public override void Write(char[] buffer, int index, int count)
    {
        _inner.Write(buffer, index, count);
        AbsorbString(new string(buffer, index, count));
    }

    public override void WriteLine(string? value)
    {
        // Bypass the base class's WriteLine implementation that would call Write
        // for both the value AND the line terminator — that double-dispatch sends
        // duplicate fragments through AbsorbString. Call the inner directly and
        // feed the buffer once.
        if (value is not null) _inner.Write(value);
        _inner.Write(_inner.NewLine);
        if (value is not null) AbsorbString(value);
        AbsorbString(_inner.NewLine);
    }

    public override void WriteLine()
    {
        _inner.Write(_inner.NewLine);
        AbsorbString(_inner.NewLine);
    }

    public override void Flush() => _inner.Flush();

    private void AbsorbChar(char c)
    {
        if (c == '\n')
        {
            _buffer.Append(_pending.ToString());
            _pending.Clear();
        }
        else if (c != '\r')
        {
            _pending.Append(c);
        }
    }

    private void AbsorbString(string s)
    {
        for (var i = 0; i < s.Length; i++) AbsorbChar(s[i]);
    }

    /// <summary>Flush any buffered partial line into the ring buffer. Called by the Executor at target end.</summary>
    public void FlushPendingLine()
    {
        if (_pending.Length > 0)
        {
            _buffer.Append(_pending.ToString());
            _pending.Clear();
        }
    }
}
