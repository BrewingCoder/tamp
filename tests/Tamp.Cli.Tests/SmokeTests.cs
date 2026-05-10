using Xunit;

namespace Tamp.Cli.Tests;

public sealed class SmokeTests
{
    [Fact]
    public void Assembly_Loads()
    {
        var assembly = typeof(Tamp.Cli.Program).Assembly;
        Assert.Equal("Tamp.Cli", assembly.GetName().Name);
    }
}
