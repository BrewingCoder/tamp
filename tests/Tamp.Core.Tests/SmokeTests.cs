using Xunit;

namespace Tamp.Core.Tests;

public sealed class SmokeTests
{
    [Fact]
    public void Assembly_Loads()
    {
        var assembly = typeof(Tamp.HostProfile).Assembly;
        Assert.Equal("Tamp.Core", assembly.GetName().Name);
    }
}
