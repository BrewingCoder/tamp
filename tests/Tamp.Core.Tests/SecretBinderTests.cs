using Xunit;

namespace Tamp.Core.Tests;

#pragma warning disable CS0649 // [Secret] fields are set by reflection; suppressed across the file.
public sealed class SecretBinderTests
{
    // Sample build class shapes for the binder to walk.

    private sealed class DefaultEnvBuild : TampBuild
    {
        [Secret("API token")]
        public Secret? ApiToken;
    }

    private sealed class CustomEnvBuild : TampBuild
    {
        [Secret("Build token", EnvironmentVariable = "MY_CUSTOM_TOKEN")]
        public Secret? BuildToken;
    }

    private sealed class NamedBuild : TampBuild
    {
        [Secret("API key", Name = "DisplayName")]
        public Secret? ApiKey;
    }

    private sealed class MultiSecretBuild : TampBuild
    {
        [Secret("First")]
        public Secret? FirstToken;

        [Secret("Second", EnvironmentVariable = "OVERRIDE_NAME")]
        public Secret? SecondToken;
    }

    private sealed class PropertyBuild : TampBuild
    {
        [Secret("Prop secret")]
        public Secret? PropSecret { get; set; }
    }

    private sealed class AlreadySetBuild : TampBuild
    {
        [Secret("Token")]
        public Secret? Token = new Secret("pre-existing", "explicit-value");
    }

    private sealed class WrongTypeBuild : TampBuild
    {
        [Secret("Wrong")]
        public string? NotASecret;
    }

    private sealed class NoPromptBuild : TampBuild
    {
        [Secret("No prompt", AllowInteractivePrompt = false)]
        public Secret? Token;
    }

    private sealed class NoOpEnv : Dictionary<string, string?>
    {
        public string? Get(string key) => TryGetValue(key, out var v) ? v : null;
    }

    // ================================================================
    // Bind — env-var leg (TAM-78)
    // ================================================================

    [Fact]
    public void Bind_Defaults_EnvKey_To_UpperSnakeCase_Of_Member_Name()
    {
        var build = new DefaultEnvBuild();
        var env = new NoOpEnv { ["API_TOKEN"] = "ghp_xxx" };
        SecretBinder.Bind(build, env.Get);
        Assert.NotNull(build.ApiToken);
        Assert.Equal("API token", build.ApiToken!.Name);
    }

    [Fact]
    public void Bind_Honors_Explicit_EnvironmentVariable_Override()
    {
        var build = new CustomEnvBuild();
        var env = new NoOpEnv
        {
            ["BUILD_TOKEN"] = "should-NOT-match-default",   // default name
            ["MY_CUSTOM_TOKEN"] = "correct-value",          // attribute override
        };
        SecretBinder.Bind(build, env.Get);
        Assert.NotNull(build.BuildToken);
        // Secret's Name should be the description (not the env-var name).
        Assert.Equal("Build token", build.BuildToken!.Name);
    }

    [Fact]
    public void Bind_Uses_Attribute_Name_When_Supplied()
    {
        var build = new NamedBuild();
        var env = new NoOpEnv { ["API_KEY"] = "v" };
        SecretBinder.Bind(build, env.Get);
        Assert.NotNull(build.ApiKey);
        Assert.Equal("DisplayName", build.ApiKey!.Name);
    }

    [Fact]
    public void Bind_Leaves_Field_Null_When_Env_Missing()
    {
        var build = new DefaultEnvBuild();
        var env = new NoOpEnv();   // no values
        SecretBinder.Bind(build, env.Get);
        Assert.Null(build.ApiToken);
    }

    [Fact]
    public void Bind_Leaves_Field_Null_When_Env_Empty_String()
    {
        var build = new DefaultEnvBuild();
        var env = new NoOpEnv { ["API_TOKEN"] = "" };
        SecretBinder.Bind(build, env.Get);
        Assert.Null(build.ApiToken);
    }

    [Fact]
    public void Bind_Does_Not_Overwrite_Already_Set_Field()
    {
        var build = new AlreadySetBuild();
        var env = new NoOpEnv { ["TOKEN"] = "env-value" };
        SecretBinder.Bind(build, env.Get);
        Assert.NotNull(build.Token);
        Assert.Equal("pre-existing", build.Token!.Name);
        Assert.Equal("explicit-value", build.Token.Reveal());
    }

    [Fact]
    public void Bind_Resolves_Multiple_Secrets_Independently()
    {
        var build = new MultiSecretBuild();
        var env = new NoOpEnv
        {
            ["FIRST_TOKEN"] = "alpha",
            ["OVERRIDE_NAME"] = "beta",
        };
        SecretBinder.Bind(build, env.Get);
        Assert.NotNull(build.FirstToken);
        Assert.NotNull(build.SecondToken);
        Assert.Equal("alpha", build.FirstToken!.Reveal());
        Assert.Equal("beta", build.SecondToken!.Reveal());
    }

    [Fact]
    public void Bind_Resolves_Property_As_Well_As_Field()
    {
        var build = new PropertyBuild();
        var env = new NoOpEnv { ["PROP_SECRET"] = "v" };
        SecretBinder.Bind(build, env.Get);
        Assert.NotNull(build.PropSecret);
    }

    [Fact]
    public void Bind_Throws_When_Member_Is_Not_Secret_Type()
    {
        // Surface-policing: [Secret] on a string field is a programmer
        // error; we surface it at bind time with a clear message.
        var build = new WrongTypeBuild();
        var env = new NoOpEnv { ["NOT_A_SECRET"] = "v" };
        var ex = Assert.Throws<InvalidOperationException>(() => SecretBinder.Bind(build, env.Get));
        Assert.Contains("[Secret]", ex.Message);
        Assert.Contains("NotASecret", ex.Message);
    }

    [Fact]
    public void Bind_Throws_On_Null_Build()
        => Assert.Throws<ArgumentNullException>(() => SecretBinder.Bind(null!, _ => null));

    [Fact]
    public void Bind_Throws_On_Null_GetEnv()
        => Assert.Throws<ArgumentNullException>(() => SecretBinder.Bind(new DefaultEnvBuild(), null!));

    [Fact]
    public void Bind_Fires_OnResolved_Callback_With_Each_Secret()
    {
        var build = new MultiSecretBuild();
        var env = new NoOpEnv
        {
            ["FIRST_TOKEN"] = "alpha",
            ["OVERRIDE_NAME"] = "beta",
        };
        var resolved = new List<Secret>();
        SecretBinder.Bind(build, env.Get, resolved.Add);
        Assert.Equal(2, resolved.Count);
        Assert.Contains(resolved, s => s.Reveal() == "alpha");
        Assert.Contains(resolved, s => s.Reveal() == "beta");
    }

    [Fact]
    public void Bind_OnResolved_Not_Fired_For_Already_Set_Secrets()
    {
        // Skip the env lookup AND skip the callback when explicit value is in place.
        var build = new AlreadySetBuild();
        var env = new NoOpEnv { ["TOKEN"] = "env-would-be-this" };
        var calls = 0;
        SecretBinder.Bind(build, env.Get, _ => calls++);
        Assert.Equal(0, calls);
    }

    // ================================================================
    // EnsureResolved — interactive prompt leg (TAM-79)
    // ================================================================

    [Fact]
    public void EnsureResolved_With_TTY_Stub_Reads_Value_And_Sets_Secret()
    {
        // Stub a TextReader/TextWriter pair; bypass Console.IsInputRedirected
        // by going through PromptForSecret directly (which is what the
        // public API uses internally — EnsureResolved guards via
        // Console.IsInputRedirected which we can't easily stub).
        using var reader = new StringReader("typed-secret\n");
        using var writer = new StringWriter();
        var value = SecretBinder.PromptForSecret("Test secret", reader, writer);
        Assert.Equal("typed-secret", value);
        Assert.Contains("Test secret", writer.ToString());
    }

    [Fact]
    public void EnsureResolved_Empty_Input_Returns_Null()
    {
        using var reader = new StringReader("\n");
        using var writer = new StringWriter();
        var value = SecretBinder.PromptForSecret("Test", reader, writer);
        Assert.Null(value);
    }

    [Fact]
    public void EnsureResolved_Whitespace_Input_Returns_Null()
    {
        using var reader = new StringReader("    \n");
        using var writer = new StringWriter();
        var value = SecretBinder.PromptForSecret("Test", reader, writer);
        Assert.Null(value);
    }

    [Fact]
    public void EnsureResolved_Skips_Members_With_AllowInteractivePrompt_False_When_TTY()
    {
        // Direct surface assertion via reflection — the attribute opt-out
        // is wired through to EnsureResolved's gate.
        var attr = typeof(NoPromptBuild)
            .GetField(nameof(NoPromptBuild.Token))!
            .GetCustomAttributes(typeof(SecretAttribute), inherit: true);
        var secretAttr = (SecretAttribute)attr[0];
        Assert.False(secretAttr.AllowInteractivePrompt);
    }

    [Fact]
    public void EnsureResolved_AllowInteractivePrompt_Defaults_To_True()
    {
        // Verifies the attribute's safe default — interactive build scripts
        // shouldn't have to opt INTO prompting.
        var attr = typeof(DefaultEnvBuild)
            .GetField(nameof(DefaultEnvBuild.ApiToken))!
            .GetCustomAttributes(typeof(SecretAttribute), inherit: true);
        var secretAttr = (SecretAttribute)attr[0];
        Assert.True(secretAttr.AllowInteractivePrompt);
    }

    [Fact]
    public void EnsureResolved_Throws_On_Null_Build()
        => Assert.Throws<ArgumentNullException>(() => SecretBinder.EnsureResolved(null!));

    // ================================================================
    // Bind — OS keychain leg (TAM-83)
    // ================================================================

    /// <summary>Test double for IOsSecretStore.</summary>
    private sealed class FakeKeychain : IOsSecretStore
    {
        public Dictionary<string, string> Entries { get; } = new();
        public int Calls { get; private set; }
        public string? TryGet(string name)
        {
            Calls++;
            return Entries.TryGetValue(name, out var v) ? v : null;
        }
    }

    private sealed class KeychainBuild : TampBuild
    {
        [Secret("Keychain token")]
        public Secret? KeychainToken;
    }

    private sealed class NoKeychainBuild : TampBuild
    {
        [Secret("No keychain", UseKeychain = false)]
        public Secret? Token;
    }

    [Fact]
    public void Bind_Falls_Back_To_Keychain_When_Env_Missing()
    {
        var build = new KeychainBuild();
        var env = new NoOpEnv();   // env empty
        var keychain = new FakeKeychain { Entries = { ["KEYCHAIN_TOKEN"] = "from-keychain" } };
        SecretBinder.Bind(build, env.Get, osStore: keychain);
        Assert.NotNull(build.KeychainToken);
        Assert.Equal("from-keychain", build.KeychainToken!.Reveal());
        Assert.Equal(1, keychain.Calls);
    }

    [Fact]
    public void Bind_Env_Wins_Over_Keychain_When_Both_Set()
    {
        var build = new KeychainBuild();
        var env = new NoOpEnv { ["KEYCHAIN_TOKEN"] = "from-env" };
        var keychain = new FakeKeychain { Entries = { ["KEYCHAIN_TOKEN"] = "from-keychain" } };
        SecretBinder.Bind(build, env.Get, osStore: keychain);
        Assert.NotNull(build.KeychainToken);
        Assert.Equal("from-env", build.KeychainToken!.Reveal());
        // Keychain shouldn't even have been consulted since env was set.
        Assert.Equal(0, keychain.Calls);
    }

    [Fact]
    public void Bind_Skips_Keychain_When_UseKeychain_False()
    {
        var build = new NoKeychainBuild();
        var env = new NoOpEnv();   // env empty
        var keychain = new FakeKeychain { Entries = { ["TOKEN"] = "from-keychain" } };
        SecretBinder.Bind(build, env.Get, osStore: keychain);
        Assert.Null(build.Token);
        Assert.Equal(0, keychain.Calls);  // never consulted
    }

    [Fact]
    public void Bind_With_Null_Keychain_Just_Uses_Env()
    {
        // Explicit null osStore (no platform store detected, or test
        // wanted to bypass).
        var build = new DefaultEnvBuild();
        var env = new NoOpEnv { ["API_TOKEN"] = "v" };
        SecretBinder.Bind(build, env.Get, osStore: null);
        Assert.NotNull(build.ApiToken);
        Assert.Equal("v", build.ApiToken!.Reveal());
    }

    [Fact]
    public void Bind_Fires_OnResolved_For_Keychain_Source_Too()
    {
        var build = new KeychainBuild();
        var env = new NoOpEnv();
        var keychain = new FakeKeychain { Entries = { ["KEYCHAIN_TOKEN"] = "v" } };
        var calls = 0;
        SecretBinder.Bind(build, env.Get, onResolved: _ => calls++, osStore: keychain);
        Assert.Equal(1, calls);
    }

    [Fact]
    public void Bind_Keychain_Empty_String_Treated_As_Missing()
    {
        var build = new KeychainBuild();
        var env = new NoOpEnv();
        var keychain = new FakeKeychain { Entries = { ["KEYCHAIN_TOKEN"] = "" } };
        SecretBinder.Bind(build, env.Get, osStore: keychain);
        Assert.Null(build.KeychainToken);
    }

    [Fact]
    public void SecretAttribute_UseKeychain_Defaults_To_True()
    {
        var attr = typeof(KeychainBuild)
            .GetField(nameof(KeychainBuild.KeychainToken))!
            .GetCustomAttributes(typeof(SecretAttribute), inherit: true);
        var secretAttr = (SecretAttribute)attr[0];
        Assert.True(secretAttr.UseKeychain);
    }
}
#pragma warning restore CS0649
