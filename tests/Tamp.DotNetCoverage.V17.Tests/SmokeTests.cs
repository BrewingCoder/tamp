using Xunit;

namespace Tamp.DotNetCoverage.V17.Tests;

public sealed class SmokeTests
{
    [Fact]
    public void Assembly_Loads()
    {
        var assembly = typeof(DotNetCoverage).Assembly;
        Assert.Equal("Tamp.DotNetCoverage.V17", assembly.GetName().Name);
    }
}
