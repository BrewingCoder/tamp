using System.Text;

namespace Tamp;

/// <summary>
/// A <see cref="TextWriter"/> that scrubs every write through a
/// <see cref="RedactionTable"/> before passing it to an inner writer.
/// Wrap any logger output, child-process passthrough writer, or dry-run
/// destination in this to ensure registered secret values never appear
/// downstream verbatim.
/// </summary>
/// <remarks>
/// Redaction is applied per-line for stream APIs. Partial-line writes are
/// buffered until a newline arrives so a secret that lands across two
/// <see cref="Write(char)"/> calls still redacts correctly.
/// </remarks>
public sealed class RedactingTextWriter : TextWriter
{
    private readonly TextWriter _inner;
    private readonly RedactionTable _table;
    private readonly StringBuilder _lineBuffer = new();

    public RedactingTextWriter(TextWriter inner, RedactionTable table)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _table = table ?? throw new ArgumentNullException(nameof(table));
    }

    public override System.Text.Encoding Encoding => _inner.Encoding;

    public override void Write(char value)
    {
        if (value == '\n')
        {
            FlushLineBuffer(appendNewline: true);
        }
        else
        {
            _lineBuffer.Append(value);
        }
    }

    public override void Write(string? value)
    {
        if (value is null) return;

        var span = value.AsSpan();
        while (!span.IsEmpty)
        {
            var nl = span.IndexOf('\n');
            if (nl < 0)
            {
                _lineBuffer.Append(span);
                break;
            }
            _lineBuffer.Append(span[..nl]);
            FlushLineBuffer(appendNewline: true);
            span = span[(nl + 1)..];
        }
    }

    public override void WriteLine(string? value)
    {
        Write(value);
        Write('\n');
    }

    public override void WriteLine() => Write('\n');

    public override void Flush()
    {
        if (_lineBuffer.Length > 0) FlushLineBuffer(appendNewline: false);
        _inner.Flush();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) Flush();
        base.Dispose(disposing);
    }

    private void FlushLineBuffer(bool appendNewline)
    {
        var line = _lineBuffer.ToString();
        _lineBuffer.Clear();
        var redacted = _table.Redact(line);
        if (appendNewline) _inner.WriteLine(redacted);
        else _inner.Write(redacted);
    }
}
