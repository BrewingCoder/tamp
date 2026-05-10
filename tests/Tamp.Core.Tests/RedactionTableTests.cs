using Xunit;

namespace Tamp.Core.Tests;

public sealed class RedactionTableTests
{
    [Fact]
    public void Empty_Table_Passes_Strings_Through()
    {
        var t = new RedactionTable();
        Assert.Equal("hello world", t.Redact("hello world"));
    }

    [Fact]
    public void Registered_Value_Is_Replaced_By_Placeholder()
    {
        var t = new RedactionTable();
        t.Register(new Secret("ApiKey", "verysecret"));
        Assert.Equal("Authorization: <Secret:ApiKey>", t.Redact("Authorization: verysecret"));
    }

    [Fact]
    public void Register_Empty_Value_Is_Noop()
    {
        var t = new RedactionTable();
        t.Register(new Secret("Empty", string.Empty));
        Assert.Equal(0, t.Count);
        Assert.Equal("hello", t.Redact("hello"));
    }

    [Fact]
    public void Multiple_Occurrences_Are_All_Redacted()
    {
        var t = new RedactionTable();
        t.Register(new Secret("X", "abc"));
        Assert.Equal("<Secret:X>def<Secret:X>", t.Redact("abcdefabc"));
    }

    [Fact]
    public void Long_Match_Wins_Over_Short_Match_When_One_Is_Substring_Of_Another()
    {
        var t = new RedactionTable();
        t.Register(new Secret("Short", "abc"));
        t.Register(new Secret("Long", "abcdef"));
        // The long secret's value contains the short secret's value, so the
        // table sorts longest-first and replaces the long match before the
        // short one can steal a substring.
        Assert.Equal("<Secret:Long>", t.Redact("abcdef"));
    }

    [Fact]
    public void Re_Registering_Same_Value_Keeps_First_Placeholder()
    {
        var t = new RedactionTable();
        t.Register(new Secret("First", "value"));
        t.Register(new Secret("Second", "value"));  // Same value, different name.
        Assert.Equal("<Secret:First>", t.Redact("value"));
    }

    [Fact]
    public void RegisterAll_Pulls_From_CommandPlan()
    {
        var plan = new CommandPlan
        {
            Executable = "tool",
            Arguments = [],
            Secrets = [new Secret("A", "alpha"), new Secret("B", "beta")],
        };
        var t = new RedactionTable();
        t.RegisterAll(plan);
        Assert.Equal(2, t.Count);
        Assert.Contains("<Secret:A>", t.Redact("alpha"));
        Assert.Contains("<Secret:B>", t.Redact("beta"));
    }

    [Fact]
    public void Null_Input_Returns_Empty()
    {
        var t = new RedactionTable();
        Assert.Equal(string.Empty, t.Redact(null));
    }

    [Fact]
    public void Concurrent_Registrations_Do_Not_Lose_Entries()
    {
        var t = new RedactionTable();
        Parallel.For(0, 100, i => t.Register(new Secret($"Name{i}", $"value{i}")));
        Assert.Equal(100, t.Count);
        for (var i = 0; i < 100; i++)
            Assert.Equal($"<Secret:Name{i}>", t.Redact($"value{i}"));
    }

    [Fact]
    public void Register_Throws_On_Null_Secret()
    {
        var t = new RedactionTable();
        Assert.Throws<ArgumentNullException>(() => t.Register(null!));
    }
}
