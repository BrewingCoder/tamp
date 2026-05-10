using Xunit;

namespace Tamp.NetCli.V8.Tests;

public sealed class SmokeTests
{
    [Fact]
    public void Assembly_Loads()
    {
        var assembly = typeof(Tamp.NetCli.V8.DotNet).Assembly;
        Assert.Equal("Tamp.NetCli.V8", assembly.GetName().Name);
    }
}
