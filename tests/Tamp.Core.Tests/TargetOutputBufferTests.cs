using System.IO;
using Xunit;

namespace Tamp.Core.Tests;

/// <summary>
/// TAM-230 — TargetOutputBuffer + CapturingTextWriter behavior. Both
/// types are internal to Tamp.Core; tests reach them via InternalsVisibleTo.
/// </summary>
public sealed class TargetOutputBufferTests
{
    [Fact]
    public void Buffer_Retains_Only_Last_N_Lines()
    {
        var buffer = new TargetOutputBuffer(capacity: 3);
        buffer.Append("one");
        buffer.Append("two");
        buffer.Append("three");
        buffer.Append("four");
        buffer.Append("five");

        Assert.Equal(new[] { "three", "four", "five" }, buffer.Drain());
    }

    [Fact]
    public void Drain_Empties_The_Buffer()
    {
        var buffer = new TargetOutputBuffer(capacity: 5);
        buffer.Append("a");
        buffer.Append("b");

        Assert.Equal(new[] { "a", "b" }, buffer.Drain());
        Assert.Empty(buffer.Drain());
    }

    [Fact]
    public void Capacity_Less_Than_One_Throws()
    {
        Assert.Throws<System.ArgumentOutOfRangeException>(() => new TargetOutputBuffer(0));
        Assert.Throws<System.ArgumentOutOfRangeException>(() => new TargetOutputBuffer(-5));
    }

    [Fact]
    public void Capturing_Writer_Splits_On_Newlines_And_Strips_Carriage_Returns()
    {
        var inner = new StringWriter { NewLine = "\n" };
        var buffer = new TargetOutputBuffer(capacity: 50);
        var capturing = new CapturingTextWriter(inner, buffer);

        capturing.Write("line one\nline two\r\nline three\n");

        Assert.Equal(new[] { "line one", "line two", "line three" }, buffer.Drain());
        Assert.Equal("line one\nline two\r\nline three\n", inner.ToString());
    }

    [Fact]
    public void Capturing_Writer_Coalesces_Partial_Line_Across_Multiple_Writes()
    {
        var inner = new StringWriter { NewLine = "\n" };
        var buffer = new TargetOutputBuffer(capacity: 10);
        var capturing = new CapturingTextWriter(inner, buffer);

        capturing.Write("hel");
        capturing.Write("lo, ");
        capturing.Write("world\n");

        Assert.Equal(new[] { "hello, world" }, buffer.Drain());
    }

    [Fact]
    public void Flush_Pending_Line_Emits_The_Partial_Last_Line()
    {
        var inner = new StringWriter { NewLine = "\n" };
        var buffer = new TargetOutputBuffer(capacity: 10);
        var capturing = new CapturingTextWriter(inner, buffer);

        capturing.Write("a complete\n");
        capturing.Write("a partial without terminator");

        // Before flush: only the complete line is in the buffer.
        // After flush: both.
        capturing.FlushPendingLine();
        Assert.Equal(new[] { "a complete", "a partial without terminator" }, buffer.Drain());
    }

    [Fact]
    public void WriteLine_Captures_Exactly_One_Buffer_Entry_Per_Call()
    {
        var inner = new StringWriter { NewLine = "\n" };
        var buffer = new TargetOutputBuffer(capacity: 10);
        var capturing = new CapturingTextWriter(inner, buffer);

        // Confirms our override of WriteLine bypasses the base-class double-dispatch
        // that would otherwise stamp duplicate fragments into the buffer.
        capturing.WriteLine("first");
        capturing.WriteLine("second");
        capturing.WriteLine();

        Assert.Equal(new[] { "first", "second", "" }, buffer.Drain());
    }
}
