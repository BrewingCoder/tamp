using Xunit;

namespace Tamp.SonarScannerCli.V6.Tests;

public sealed class SmokeTests
{
    [Fact]
    public void Assembly_Loads()
    {
        var assembly = typeof(SonarScannerCli).Assembly;
        Assert.Equal("Tamp.SonarScannerCli.V6", assembly.GetName().Name);
    }
}
