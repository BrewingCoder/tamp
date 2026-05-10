using Xunit;

namespace Tamp.SonarScanner.V10.Tests;

public sealed class SmokeTests
{
    [Fact]
    public void Assembly_Loads()
    {
        var assembly = typeof(SonarScanner).Assembly;
        Assert.Equal("Tamp.SonarScanner.V10", assembly.GetName().Name);
    }
}
