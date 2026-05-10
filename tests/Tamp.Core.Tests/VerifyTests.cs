using Xunit;

namespace Tamp.Core.Tests;

public sealed class VerifyTests
{
    [Fact]
    public void NotNull_Reference_Returns_Value_When_NonNull()
    {
        var s = "hello";
        Assert.Equal("hello", Verify.NotNull(s));
    }

    [Fact]
    public void NotNull_Reference_Throws_On_Null()
    {
        string? s = null;
        var ex = Assert.Throws<InvalidOperationException>(() => Verify.NotNull(s));
        Assert.Contains("s", ex.Message);
    }

    [Fact]
    public void NotNull_Value_Returns_Underlying_When_Set()
    {
        int? n = 42;
        Assert.Equal(42, Verify.NotNull(n));
    }

    [Fact]
    public void NotNull_Value_Throws_When_Null()
    {
        int? n = null;
        Assert.Throws<InvalidOperationException>(() => Verify.NotNull(n));
    }

    [Fact]
    public void NotNullOrEmpty_Round_Trips_Non_Empty()
    {
        Assert.Equal("x", Verify.NotNullOrEmpty("x"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void NotNullOrEmpty_Throws_On_Null_Or_Empty(string? value)
    {
        Assert.Throws<InvalidOperationException>(() => Verify.NotNullOrEmpty(value));
    }

    [Fact]
    public void NotNullOrWhiteSpace_Throws_On_Whitespace()
    {
        Assert.Throws<InvalidOperationException>(() => Verify.NotNullOrWhiteSpace("   \t\n"));
    }

    [Fact]
    public void True_And_False_Round_Trip()
    {
        Verify.True(1 + 1 == 2);
        Verify.False(1 + 1 == 3);
        Assert.Throws<InvalidOperationException>(() => Verify.True(false));
        Assert.Throws<InvalidOperationException>(() => Verify.False(true));
    }

    [Fact]
    public void True_Includes_Expression_In_Default_Message()
    {
        var x = 5;
        var ex = Assert.Throws<InvalidOperationException>(() => Verify.True(x > 10));
        Assert.Contains("x > 10", ex.Message);
    }

    [Fact]
    public void True_With_Custom_Message_Uses_It()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => Verify.True(false, "explicit reason"));
        Assert.Equal("explicit reason", ex.Message);
    }

    [Fact]
    public void NotEmpty_Round_Trips_Non_Empty_Sequence()
    {
        var result = Verify.NotEmpty(new[] { 1, 2, 3 });
        Assert.Equal(3, result.Count());
    }

    [Fact]
    public void NotEmpty_Throws_On_Empty()
    {
        Assert.Throws<InvalidOperationException>(() => Verify.NotEmpty(Array.Empty<int>()));
    }

    [Fact]
    public void NotEmpty_Throws_On_Null()
    {
        Assert.Throws<InvalidOperationException>(() => Verify.NotEmpty<int>(null));
    }

    [Fact]
    public void Single_Returns_The_One_Element()
    {
        Assert.Equal(42, Verify.Single(new[] { 42 }));
    }

    [Fact]
    public void Single_Throws_When_Empty()
    {
        Assert.Throws<InvalidOperationException>(() => Verify.Single(Array.Empty<int>()));
    }

    [Fact]
    public void Single_Throws_When_Multiple()
    {
        Assert.Throws<InvalidOperationException>(() => Verify.Single(new[] { 1, 2 }));
    }

    [Fact]
    public void Fail_Throws_With_Message()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => Verify.Fail("nope"));
        Assert.Equal("nope", ex.Message);
    }
}
