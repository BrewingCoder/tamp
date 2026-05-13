using System;
using Xunit;

namespace Tamp.Tests;

public sealed class WrapperSettingsBaseTests
{
    // A derived class can reveal a secret via the inherited helper.
    private sealed class FakeApiVerbSettings : WrapperSettingsBase
    {
        public Secret? ApiKey { get; set; }
        public string? ApiKeyAsArg => ApiKey is null ? null : Reveal(ApiKey);
    }

    [Fact]
    public void Reveal_Returns_Cleartext_From_Derived_Class()
    {
        var settings = new FakeApiVerbSettings { ApiKey = new Secret("token", "abc123") };
        Assert.Equal("abc123", settings.ApiKeyAsArg);
    }

    [Fact]
    public void Reveal_Throws_On_Null_Secret()
    {
        // Calling Reveal(null!) from derived code should fail fast rather than NRE silently.
        // Static methods are not inherited on the derived Type via reflection; look up on base.
        var method = typeof(WrapperSettingsBase).GetMethod("Reveal",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        var ex = Record.Exception(() => method.Invoke(null, new object?[] { null }));
        Assert.IsType<ArgumentNullException>(ex?.InnerException);
    }

    [Fact]
    public void WrapperSettingsBase_Is_Abstract()
    {
        Assert.True(typeof(WrapperSettingsBase).IsAbstract);
    }

    [Fact]
    public void Reveal_Method_Is_Protected_Static()
    {
        var method = typeof(WrapperSettingsBase).GetMethod("Reveal",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(method);
        Assert.True(method!.IsFamily);   // C# 'protected' → IsFamily
        Assert.True(method.IsStatic);
    }
}
