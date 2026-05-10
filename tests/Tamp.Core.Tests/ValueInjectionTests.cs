using System.Reflection;
using Xunit;

namespace Tamp.Core.Tests;

public sealed class ValueInjectionTests
{
    private sealed class FixedInjectionAttribute : ValueInjectionAttribute
    {
        public string Value { get; }
        public FixedInjectionAttribute(string value) { Value = value; }
        public override object? GetValue(MemberInfo member, Type memberType) => Value;
    }

    private sealed class ThrowingInjectionAttribute : ValueInjectionAttribute
    {
        public override object? GetValue(MemberInfo member, Type memberType)
            => throw new InvalidOperationException("intentional");
    }

    private sealed class FixedInjectedBuild : TampBuild
    {
        [FixedInjection("hello")]
        public string Greeting { get; set; } = "default";
    }

    private sealed class FixedInjectedFieldBuild : TampBuild
    {
        [FixedInjection("from-field")]
        public string Greeting = "default";
    }

    private sealed class FixedInjectedReadonlyFieldBuild : TampBuild
    {
        [FixedInjection("from-readonly-field")]
        public readonly string Greeting = "default";
    }

    private sealed class ThrowingInjectedBuild : TampBuild
    {
        [ThrowingInjection]
        public string Broken { get; set; } = "default";
    }

    [Fact]
    public void ValueInjection_Sets_Property()
    {
        var build = new FixedInjectedBuild();
        ParameterBinder.Bind(build, [], _ => null);
        Assert.Equal("hello", build.Greeting);
    }

    [Fact]
    public void ValueInjection_Sets_Field()
    {
        var build = new FixedInjectedFieldBuild();
        ParameterBinder.Bind(build, [], _ => null);
        Assert.Equal("from-field", build.Greeting);
    }

    [Fact]
    public void ValueInjection_Sets_Readonly_Field()
    {
        // The NUKE-style idiom — `[Solution] readonly Solution Solution;`
        // — has to work, otherwise the standard build-script pattern is
        // broken. Reflection bypasses the readonly guarantee here.
        var build = new FixedInjectedReadonlyFieldBuild();
        ParameterBinder.Bind(build, [], _ => null);
        Assert.Equal("from-readonly-field", build.Greeting);
    }

    [Fact]
    public void ValueInjection_Throws_With_Helpful_Wrapper_When_Attribute_Throws()
    {
        var build = new ThrowingInjectedBuild();
        var ex = Assert.Throws<InvalidOperationException>(() => ParameterBinder.Bind(build, [], _ => null));
        Assert.Contains("Broken", ex.Message);
        Assert.Contains("ThrowingInjectionAttribute", ex.Message);
        Assert.NotNull(ex.InnerException);
        Assert.Equal("intentional", ex.InnerException!.Message);
    }
}
