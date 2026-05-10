using Xunit;

namespace Tamp.NetCli.V10.Tests;

public sealed class SmokeTests
{
    [Fact]
    public void Assembly_Loads()
    {
        var assembly = typeof(Tamp.NetCli.V10.Placeholder).Assembly;
        Assert.Equal("Tamp.NetCli.V10", assembly.GetName().Name);
    }
}
