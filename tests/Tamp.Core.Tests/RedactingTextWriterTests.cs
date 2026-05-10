using Xunit;

namespace Tamp.Core.Tests;

public sealed class RedactingTextWriterTests
{
    private static (RedactingTextWriter Writer, StringWriter Sink, RedactionTable Table) Setup(params (string Name, string Value)[] secrets)
    {
        var sink = new StringWriter();
        var table = new RedactionTable();
        foreach (var (n, v) in secrets) table.Register(new Secret(n, v));
        var w = new RedactingTextWriter(sink, table);
        return (w, sink, table);
    }

    [Fact]
    public void WriteLine_Redacts_Single_Line()
    {
        var (w, sink, _) = Setup(("A", "secret"));
        w.WriteLine("Authorization: secret");
        w.Flush();
        Assert.Equal("Authorization: <Secret:A>" + Environment.NewLine, sink.ToString());
    }

    [Fact]
    public void Write_Buffers_Until_Newline_Then_Redacts()
    {
        var (w, sink, _) = Setup(("A", "secret"));
        // Stream the secret one character at a time across two writes; the
        // line buffer must hold both halves and redact when the newline
        // arrives.
        w.Write("Authorization: sec");
        w.Write("ret\n");
        Assert.Equal("Authorization: <Secret:A>" + Environment.NewLine, sink.ToString());
    }

    [Fact]
    public void Char_By_Char_Write_Redacts_Across_Boundary()
    {
        var (w, sink, _) = Setup(("A", "secret"));
        foreach (var ch in "Authorization: secret\n") w.Write(ch);
        Assert.Equal("Authorization: <Secret:A>" + Environment.NewLine, sink.ToString());
    }

    [Fact]
    public void Multiple_Lines_Each_Redacted_Independently()
    {
        var (w, sink, _) = Setup(("A", "alpha"));
        w.Write("alpha is here\nand alpha here too\n");
        var lines = sink.ToString().Split(Environment.NewLine);
        Assert.Equal("<Secret:A> is here", lines[0]);
        Assert.Equal("and <Secret:A> here too", lines[1]);
    }

    [Fact]
    public void Flush_Without_Newline_Still_Redacts_Buffered_Content()
    {
        var (w, sink, _) = Setup(("A", "secret"));
        w.Write("trailing secret");
        w.Flush();
        Assert.Equal("trailing <Secret:A>", sink.ToString());
    }

    [Fact]
    public void Empty_Table_Produces_Identical_Output()
    {
        var (w, sink, _) = Setup();
        w.WriteLine("nothing to redact");
        w.Flush();
        Assert.Equal("nothing to redact" + Environment.NewLine, sink.ToString());
    }

    [Fact]
    public void Late_Registration_Affects_Subsequent_Lines_Not_Already_Flushed_Ones()
    {
        var (w, sink, table) = Setup();
        w.WriteLine("alpha is unredacted");
        table.Register(new Secret("A", "alpha"));
        w.WriteLine("alpha is now redacted");
        w.Flush();
        var lines = sink.ToString().Split(Environment.NewLine);
        Assert.Equal("alpha is unredacted", lines[0]);
        Assert.Equal("<Secret:A> is now redacted", lines[1]);
    }

    [Fact]
    public void Constructor_Throws_On_Null_Inner()
    {
        Assert.Throws<ArgumentNullException>(() => new RedactingTextWriter(null!, new RedactionTable()));
    }

    [Fact]
    public void Constructor_Throws_On_Null_Table()
    {
        Assert.Throws<ArgumentNullException>(() => new RedactingTextWriter(TextWriter.Null, null!));
    }

    [Fact]
    public void Encoding_Mirrors_Inner_Writer()
    {
        var sink = new StringWriter();
        var w = new RedactingTextWriter(sink, new RedactionTable());
        Assert.Equal(sink.Encoding, w.Encoding);
    }

    [Fact]
    public void Dispose_Flushes_Buffer()
    {
        var sink = new StringWriter();
        var table = new RedactionTable();
        table.Register(new Secret("A", "secret"));
        using (var w = new RedactingTextWriter(sink, table))
        {
            w.Write("trailing secret");
        }
        Assert.Equal("trailing <Secret:A>", sink.ToString());
    }
}
