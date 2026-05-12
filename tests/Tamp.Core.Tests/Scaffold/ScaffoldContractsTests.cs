using Tamp.Scaffold;
using Xunit;

namespace Tamp.Core.Tests.Scaffold;

/// <summary>
/// Contract surface tests for the scaffold types that live in Tamp.Core
/// (so external template-package authors can implement them with a single
/// dependency). Pure-shape tests — no filesystem, no CLI dispatch.
/// </summary>
public sealed class ScaffoldContractsTests
{
    [Fact]
    public void FileSpec_Executable_Defaults_False()
    {
        var spec = new FileSpec(AbsolutePath.Create("/tmp/foo.txt"), "x", WriteMode.Create);
        Assert.False(spec.Executable);
    }

    [Fact]
    public void FileSpec_Supports_Init_Only_Executable_Flag()
    {
        var spec = new FileSpec(AbsolutePath.Create("/tmp/foo.sh"), "#!/bin/sh", WriteMode.SkipIfExists)
        {
            Executable = true,
        };
        Assert.True(spec.Executable);
    }

    [Fact]
    public void FileSpec_With_Operator_Preserves_Executable_When_Other_Props_Change()
    {
        // Record `with` should carry the init-only Executable forward — verifies it's a real init prop.
        var a = new FileSpec(AbsolutePath.Create("/tmp/a.sh"), "#!/bin/sh", WriteMode.Create)
        {
            Executable = true,
        };
        var b = a with { Content = "#!/bin/bash" };
        Assert.True(b.Executable);
        Assert.Equal("#!/bin/bash", b.Content);
    }

    [Fact]
    public void ScaffoldContextBuilder_Build_Freezes_ProbeData_Into_Readonly()
    {
        var builder = new ScaffoldContextBuilder
        {
            RepoRoot = AbsolutePath.Create("/tmp/x"),
            TampCoreVersion = "1.4.0",
        };
        builder.Set("k1", "v1");
        builder.Set("k2", "v2");

        var ctx = builder.Build();

        Assert.Equal("v1", ctx.ProbeData["k1"]);
        Assert.Equal("v2", ctx.ProbeData["k2"]);

        // Subsequent mutations to the builder don't leak into the frozen context.
        builder.Set("k3", "v3");
        Assert.False(ctx.ProbeData.ContainsKey("k3"));
    }

    [Fact]
    public void ScaffoldContextBuilder_Set_Overwrites_Existing_Key()
    {
        var builder = new ScaffoldContextBuilder
        {
            RepoRoot = AbsolutePath.Create("/tmp/x"),
            TampCoreVersion = "1.4.0",
        };
        builder.Set("k", "first");
        builder.Set("k", "second");
        var ctx = builder.Build();
        Assert.Equal("second", ctx.ProbeData["k"]);
    }

    [Fact]
    public void ScaffoldContext_Defaults_Empty_ProbeData_When_Not_Set()
    {
        var ctx = new ScaffoldContext
        {
            RepoRoot = AbsolutePath.Create("/tmp/x"),
            TampCoreVersion = "1.4.0",
        };
        Assert.Empty(ctx.ProbeData);
    }

    private sealed class StubTemplate : IScaffoldTemplate
    {
        public string Name => "stub";
        public string Description => "test stub";
        public string MinimumTampCoreVersion => "1.4.0";
        public System.Collections.Generic.IEnumerable<FileSpec> Render(ScaffoldContext ctx)
            => System.Array.Empty<FileSpec>();
    }

    private sealed class StubProvider : IScaffoldTemplateProvider
    {
        public IScaffoldTemplate GetTemplate() => new StubTemplate();
    }

    [Fact]
    public void External_Templates_Can_Be_Authored_Against_The_Contracts()
    {
        // This is the integration shape NuGet template packages use (v0.2.0+):
        // implement IScaffoldTemplate, expose via IScaffoldTemplateProvider.
        // Test: the contracts compile and round-trip a template instance.
        var provider = new StubProvider();
        var template = provider.GetTemplate();
        Assert.Equal("stub", template.Name);
        Assert.Equal("1.4.0", template.MinimumTampCoreVersion);
    }
}
