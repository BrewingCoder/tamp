using Xunit;

namespace Tamp.NetCli.V9.Tests;

public sealed class SmokeTests
{
    [Fact]
    public void Assembly_Loads()
    {
        var assembly = typeof(Tamp.NetCli.V9.Placeholder).Assembly;
        Assert.Equal("Tamp.NetCli.V9", assembly.GetName().Name);
    }
}
