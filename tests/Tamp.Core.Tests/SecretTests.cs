using Bogus;
using Xunit;

namespace Tamp.Core.Tests;

public sealed class SecretTests
{
    private static readonly Faker Faker = new() { Random = new Randomizer(unchecked((int)0xBEEFCAFE)) };

    [Fact]
    public void Constructor_Accepts_Name_And_Value()
    {
        var s = new Secret("RegistryPassword", "p@ssw0rd!");
        Assert.Equal("RegistryPassword", s.Name);
        Assert.Equal("p@ssw0rd!", s.Reveal());
    }

    [Fact]
    public void ToString_Returns_Redacted_Form_Not_Value()
    {
        var s = new Secret("ApiKey", "verysecret");
        var str = s.ToString();
        Assert.DoesNotContain("verysecret", str);
        Assert.Contains("ApiKey", str);
        Assert.Equal("<Secret:ApiKey>", str);
    }

    [Fact]
    public void String_Interpolation_Uses_Redacted_Form()
    {
        var s = new Secret("ApiKey", "verysecret");
        var msg = $"Auth header: {s}";
        Assert.DoesNotContain("verysecret", msg);
        Assert.Contains("<Secret:ApiKey>", msg);
    }

    [Fact]
    public void StringFormat_Uses_Redacted_Form()
    {
        var s = new Secret("Token", "abc-xyz-123");
        var msg = string.Format("auth={0}", s);
        Assert.DoesNotContain("abc-xyz-123", msg);
    }

    [Fact]
    public void Object_ToString_Path_Uses_Redacted_Form()
    {
        // When code calls .ToString() through object boxing — e.g., generic
        // logger frameworks that take object? — the secret must still redact.
        object boxed = new Secret("Token", "abc-xyz-123");
        Assert.Equal("<Secret:Token>", boxed.ToString());
    }

    [Fact]
    public void Constructor_Throws_On_Null_Name()
    {
        Assert.Throws<ArgumentNullException>(() => new Secret(null!, "value"));
    }

    [Fact]
    public void Constructor_Throws_On_Empty_Name()
    {
        Assert.Throws<ArgumentException>(() => new Secret(string.Empty, "value"));
    }

    [Fact]
    public void Constructor_Throws_On_Null_Value()
    {
        Assert.Throws<ArgumentNullException>(() => new Secret("Name", null!));
    }

    [Fact]
    public void Empty_Value_Is_Permitted()
    {
        // An empty secret is unusual but legal — a wrapper might construct one
        // from an env var that exists but is unset. Caller's responsibility to
        // decide whether that's an error.
        var s = new Secret("Maybe", string.Empty);
        Assert.Equal(string.Empty, s.Reveal());
    }

    [Theory]
    [InlineData("simple")]
    [InlineData("with spaces")]
    [InlineData("with\ttabs\nand\nnewlines")]
    [InlineData("\"quotes\" and 'apostrophes'")]
    [InlineData("éó中💀")]
    [InlineData("$pecial$ ${variable} characters")]
    public void Values_With_Exotic_Content_Round_Trip(string value)
    {
        var s = new Secret("X", value);
        Assert.Equal(value, s.Reveal());
        Assert.DoesNotContain(value, s.ToString());
    }

    [Fact]
    public void Equality_Is_Reference_Based()
    {
        var a = new Secret("Name", "value");
        var b = new Secret("Name", "value");
        Assert.NotEqual(a, b);  // Different instances; reference equality.
        Assert.Equal(a, a);
    }

    [Fact]
    public void Different_Instances_Have_Different_Hash_Codes_Typically()
    {
        // Reference-identity hash; this is *not* a guarantee, but in practice
        // two newly-allocated Secrets will have different hashes. If this test
        // ever flakes, it's a sign the hash impl changed — investigate.
        var a = new Secret("X", "v");
        var b = new Secret("X", "v");
        Assert.NotEqual(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void Many_Concurrent_Reveals_Return_Same_Value()
    {
        var s = new Secret("X", "consistent");
        var results = Enumerable.Range(0, 1000)
            .AsParallel()
            .Select(_ => s.Reveal())
            .ToList();
        Assert.All(results, r => Assert.Equal("consistent", r));
    }

    [Fact]
    public void Random_Secrets_ToString_Always_Returns_Exact_Redacted_Form()
    {
        // Stronger than DoesNotContain(value): the contract is that ToString
        // returns *exactly* `<Secret:Name>` regardless of the value, so
        // assert equality. (DoesNotContain has a false-positive trap when a
        // short random value happens to be a substring of the wrapper text
        // — e.g., value="c" appears in "<Secret:...".)
        for (var i = 0; i < 100; i++)
        {
            var name = Faker.Hacker.Verb() + Faker.Random.Number(99);
            var value = Faker.Random.AlphaNumeric(Faker.Random.Int(1, 64));
            var s = new Secret(name, value);
            Assert.Equal($"<Secret:{name}>", s.ToString());
        }
    }

    [Fact]
    public void Long_High_Entropy_Values_Never_Appear_In_ToString()
    {
        // Belt-and-braces value-leak check using values long enough that
        // collision with the wrapper text is statistically negligible.
        for (var i = 0; i < 100; i++)
        {
            var name = "X" + i;
            var value = Faker.Random.AlphaNumeric(64);
            var s = new Secret(name, value);
            Assert.DoesNotContain(value, s.ToString());
        }
    }
}
