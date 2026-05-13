using Xunit;

namespace Tamp.Core.Tests;

public sealed class ParameterBinderTests
{
    private sealed class StringBuild : TampBuild
    {
        [Parameter("Build configuration")]
        public string Configuration { get; set; } = "Debug";
    }

    private sealed class EnumBuild : TampBuild
    {
        [Parameter] public Configuration Config { get; set; } = Tamp.Configuration.Debug;
    }

    private sealed class IntBuild : TampBuild
    {
        [Parameter] public int Verbosity { get; set; } = 1;
    }

    private sealed class BoolBuild : TampBuild
    {
        [Parameter] public bool Quiet { get; set; }
    }

    private sealed class NullableIntBuild : TampBuild
    {
        [Parameter] public int? MaxParallelism { get; set; }
    }

    private sealed class CustomCliNameBuild : TampBuild
    {
        [Parameter(Name = "env")]
        public string Environment { get; set; } = "dev";
    }

    private sealed class CustomEnvNameBuild : TampBuild
    {
        [Parameter(EnvironmentVariable = "DEPLOY_ENVIRONMENT")]
        public string Environment { get; set; } = "dev";
    }

    private sealed class FieldBuild : TampBuild
    {
        [Parameter] public string Configuration = "Debug";
    }

    // TAM-167 — readonly fields must bind. HoldFast hit this on a pre-fix Tamp.Core
    // (the binder used to early-out on `f.IsInitOnly`). The current binder writes
    // readonly fields via reflection — supported as a first-class shape because the
    // canonical Tamp build script uses `[Parameter] readonly string Configuration;`.
#pragma warning disable CS0649 // never-assigned warning; the binder writes the value via reflection
    private sealed class ReadonlyFieldBuild : TampBuild
    {
        [Parameter] public readonly string? Configuration;
        public string? GetConfiguration() => Configuration;
    }

    private sealed class ReadonlyFieldWithDefaultBuild : TampBuild
    {
        [Parameter] public readonly string Configuration = "Debug";
        public string GetConfiguration() => Configuration;
    }

    private sealed class PrivateReadonlyFieldBuild : TampBuild
    {
        [Parameter] readonly string? Configuration;
        public string? GetConfiguration() => Configuration;
    }
#pragma warning restore CS0649

    private static string? NoEnv(string _) => null;

    // ---- CLI parsing ----

    [Theory]
    [InlineData(new[] { "--name", "value" }, "name", "value")]
    [InlineData(new[] { "--name=value" }, "name", "value")]
    [InlineData(new[] { "--Name=Value" }, "Name", "Value")]
    [InlineData(new[] { "--name=" }, "name", "")]
    public void ParseCli_Accepts_Common_Flag_Forms(string[] args, string expectedKey, string expectedValue)
    {
        var parsed = ParameterBinder.ParseCli(args);
        Assert.Equal(expectedValue, parsed[expectedKey]);
    }

    [Fact]
    public void ParseCli_Bare_Flag_Becomes_True()
    {
        var parsed = ParameterBinder.ParseCli(["--quiet"]);
        Assert.Equal("true", parsed["quiet"]);
    }

    [Fact]
    public void ParseCli_Bare_Flag_Followed_By_Another_Flag_Stays_True()
    {
        var parsed = ParameterBinder.ParseCli(["--quiet", "--verbose"]);
        Assert.Equal("true", parsed["quiet"]);
        Assert.Equal("true", parsed["verbose"]);
    }

    [Fact]
    public void ParseCli_Ignores_Non_Flag_Tokens()
    {
        var parsed = ParameterBinder.ParseCli(["target-name", "--flag", "value"]);
        Assert.Single(parsed);
        Assert.Equal("value", parsed["flag"]);
    }

    [Fact]
    public void ParseCli_Equal_Sign_With_Empty_Key_Is_Ignored()
    {
        var parsed = ParameterBinder.ParseCli(["--=value"]);
        Assert.Empty(parsed);
    }

    // ---- Naming conventions ----

    [Theory]
    [InlineData("Configuration", "configuration")]
    [InlineData("MyValue", "my-value")]
    [InlineData("HTTPSEndpoint", "https-endpoint")]  // Acronym followed by Pascal word splits at the boundary.
    [InlineData("URLPath", "url-path")]
    [InlineData("simple", "simple")]
    [InlineData("X", "x")]
    [InlineData("", "")]
    public void ToKebabCase_Converts_PascalCase(string input, string expected)
    {
        Assert.Equal(expected, ParameterBinder.ToKebabCase(input));
    }

    [Theory]
    [InlineData("Configuration", "CONFIGURATION")]
    [InlineData("MyValue", "MY_VALUE")]
    [InlineData("X", "X")]
    [InlineData("", "")]
    public void ToUpperSnakeCase_Converts_PascalCase(string input, string expected)
    {
        Assert.Equal(expected, ParameterBinder.ToUpperSnakeCase(input));
    }

    // ---- End-to-end binding ----

    [Fact]
    public void String_Property_Binds_From_Cli()
    {
        var b = new StringBuild();
        ParameterBinder.Bind(b, ["--configuration", "Release"], NoEnv);
        Assert.Equal("Release", b.Configuration);
    }

    [Fact]
    public void String_Property_Binds_From_Env_When_No_Cli()
    {
        var b = new StringBuild();
        ParameterBinder.Bind(b, [], k => k == "CONFIGURATION" ? "Release" : null);
        Assert.Equal("Release", b.Configuration);
    }

    [Fact]
    public void Cli_Wins_Over_Env()
    {
        var b = new StringBuild();
        ParameterBinder.Bind(b, ["--configuration", "Release"], k => k == "CONFIGURATION" ? "Debug" : null);
        Assert.Equal("Release", b.Configuration);
    }

    [Fact]
    public void Default_Value_Survives_When_No_Cli_Or_Env()
    {
        var b = new StringBuild();
        ParameterBinder.Bind(b, [], NoEnv);
        Assert.Equal("Debug", b.Configuration);
    }

    [Fact]
    public void Enum_Property_Binds_From_Cli_Case_Insensitive()
    {
        var b = new EnumBuild();
        ParameterBinder.Bind(b, ["--config", "release"], NoEnv);
        Assert.Equal(Tamp.Configuration.Release, b.Config);
    }

    [Fact]
    public void Int_Property_Binds_From_Cli()
    {
        var b = new IntBuild();
        ParameterBinder.Bind(b, ["--verbosity", "3"], NoEnv);
        Assert.Equal(3, b.Verbosity);
    }

    [Fact]
    public void Bool_Property_Binds_From_Bare_Flag()
    {
        var b = new BoolBuild();
        ParameterBinder.Bind(b, ["--quiet"], NoEnv);
        Assert.True(b.Quiet);
    }

    [Theory]
    [InlineData("true")]
    [InlineData("True")]
    [InlineData("TRUE")]
    [InlineData("1")]
    public void Bool_Property_Accepts_Truthy_Forms(string value)
    {
        var b = new BoolBuild();
        ParameterBinder.Bind(b, ["--quiet", value], NoEnv);
        Assert.True(b.Quiet);
    }

    [Theory]
    [InlineData("false")]
    [InlineData("False")]
    [InlineData("0")]
    public void Bool_Property_Accepts_Falsy_Forms(string value)
    {
        var b = new BoolBuild { Quiet = true };
        ParameterBinder.Bind(b, ["--quiet", value], NoEnv);
        Assert.False(b.Quiet);
    }

    [Fact]
    public void Bool_Property_Throws_On_Garbage_Value()
    {
        var b = new BoolBuild();
        Assert.Throws<InvalidOperationException>(
            () => ParameterBinder.Bind(b, ["--quiet", "maybe"], NoEnv));
    }

    [Fact]
    public void Nullable_Int_Property_Binds_From_Cli()
    {
        var b = new NullableIntBuild();
        ParameterBinder.Bind(b, ["--max-parallelism", "8"], NoEnv);
        Assert.Equal(8, b.MaxParallelism);
    }

    [Fact]
    public void Nullable_Int_Property_Stays_Null_When_No_Cli()
    {
        var b = new NullableIntBuild();
        ParameterBinder.Bind(b, [], NoEnv);
        Assert.Null(b.MaxParallelism);
    }

    [Fact]
    public void Custom_Cli_Name_Overrides_Default_Mapping()
    {
        var b = new CustomCliNameBuild();
        ParameterBinder.Bind(b, ["--env", "prod"], NoEnv);
        Assert.Equal("prod", b.Environment);
    }

    [Fact]
    public void Custom_Cli_Name_Means_Default_Is_Unrecognized()
    {
        var b = new CustomCliNameBuild();
        ParameterBinder.Bind(b, ["--environment", "prod"], NoEnv);
        Assert.Equal("dev", b.Environment);  // Default kept; --environment is a different key now.
    }

    [Fact]
    public void Custom_Env_Name_Overrides_Default_Mapping()
    {
        var b = new CustomEnvNameBuild();
        ParameterBinder.Bind(b, [], k => k == "DEPLOY_ENVIRONMENT" ? "prod" : null);
        Assert.Equal("prod", b.Environment);
    }

    [Fact]
    public void Field_With_Parameter_Binds()
    {
        var b = new FieldBuild();
        ParameterBinder.Bind(b, ["--configuration", "Release"], NoEnv);
        Assert.Equal("Release", b.Configuration);
    }

    // ─── TAM-167 — readonly field binding ────────────────────────────────

    [Fact]
    public void Readonly_Field_With_Parameter_Binds_From_Cli()
    {
        var b = new ReadonlyFieldBuild();
        ParameterBinder.Bind(b, ["--configuration", "Release"], NoEnv);
        Assert.Equal("Release", b.GetConfiguration());
    }

    [Fact]
    public void Readonly_Field_With_Parameter_Binds_From_Env()
    {
        var b = new ReadonlyFieldBuild();
        ParameterBinder.Bind(b, [], k => k == "CONFIGURATION" ? "Release" : null);
        Assert.Equal("Release", b.GetConfiguration());
    }

    [Fact]
    public void Readonly_Field_Keeps_Default_When_No_Override()
    {
        var b = new ReadonlyFieldWithDefaultBuild();
        ParameterBinder.Bind(b, [], NoEnv);
        Assert.Equal("Debug", b.GetConfiguration());
    }

    [Fact]
    public void Readonly_Field_Default_Overridden_By_Cli()
    {
        var b = new ReadonlyFieldWithDefaultBuild();
        ParameterBinder.Bind(b, ["--configuration", "Release"], NoEnv);
        Assert.Equal("Release", b.GetConfiguration());
    }

    [Fact]
    public void Private_Readonly_Field_Also_Binds()
    {
        // The build-script idiom is `[Parameter] readonly string X;` (implicit private).
        // BindingFlags.NonPublic covers it.
        var b = new PrivateReadonlyFieldBuild();
        ParameterBinder.Bind(b, ["--configuration", "Release"], NoEnv);
        Assert.Equal("Release", b.GetConfiguration());
    }

    [Fact]
    public void Empty_Env_Var_Treated_As_No_Value()
    {
        var b = new StringBuild();
        ParameterBinder.Bind(b, [], k => k == "CONFIGURATION" ? string.Empty : null);
        Assert.Equal("Debug", b.Configuration);  // Default kept.
    }

    [Fact]
    public void Bind_Throws_On_Null_Build()
    {
        Assert.Throws<ArgumentNullException>(
            () => ParameterBinder.Bind(null!, [], NoEnv));
    }
}
