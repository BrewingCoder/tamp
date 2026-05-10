using Xunit;

namespace Tamp.Core.Tests;

public sealed class ProcessRunnerTests
{
    [Fact]
    public void Print_Renders_Executable_And_Args()
    {
        var plan = new CommandPlan
        {
            Executable = "dotnet",
            Arguments = ["build", "--configuration", "Release"],
        };
        var sw = new StringWriter();
        ProcessRunner.Print(plan, "Compile", sourceModule: null, sw);
        var s = sw.ToString();
        Assert.Contains("Compile", s);
        Assert.Contains("dotnet build --configuration Release", s);
    }

    [Fact]
    public void Print_Includes_Module_Name_When_Provided()
    {
        var plan = new CommandPlan { Executable = "dotnet", Arguments = ["build"] };
        var sw = new StringWriter();
        ProcessRunner.Print(plan, "Compile", "Tamp.NetCli.V10", sw);
        Assert.Contains("(Tamp.NetCli.V10)", sw.ToString());
    }

    [Fact]
    public void Print_Quotes_Arguments_With_Spaces()
    {
        var plan = new CommandPlan
        {
            Executable = "tool",
            Arguments = ["arg with spaces"],
        };
        var sw = new StringWriter();
        ProcessRunner.Print(plan, "T", null, sw);
        Assert.Contains("\"arg with spaces\"", sw.ToString());
    }

    [Fact]
    public void Print_Escapes_Quotes_Inside_Arguments()
    {
        var plan = new CommandPlan
        {
            Executable = "tool",
            Arguments = ["he said \"hi\""],
        };
        var sw = new StringWriter();
        ProcessRunner.Print(plan, "T", null, sw);
        Assert.Contains("\"he said \\\"hi\\\"\"", sw.ToString());
    }

    [Fact]
    public void Print_Lists_Working_Directory_When_Set()
    {
        var plan = new CommandPlan
        {
            Executable = "tool",
            Arguments = [],
            WorkingDirectory = "/tmp/work",
        };
        var sw = new StringWriter();
        ProcessRunner.Print(plan, "T", null, sw);
        Assert.Contains("cwd: /tmp/work", sw.ToString());
    }

    [Fact]
    public void Print_Lists_Environment_Variables_When_Set()
    {
        var plan = new CommandPlan
        {
            Executable = "tool",
            Arguments = [],
            Environment = new Dictionary<string, string> { ["FOO"] = "1", ["BAR"] = "two" },
        };
        var sw = new StringWriter();
        ProcessRunner.Print(plan, "T", null, sw);
        var s = sw.ToString();
        Assert.Contains("env:", s);
        Assert.Contains("FOO=1", s);
        Assert.Contains("BAR=two", s);
    }

    [Fact]
    public void Print_Lists_Secret_Names_With_Redaction_Marker()
    {
        var plan = new CommandPlan
        {
            Executable = "tool",
            Arguments = [],
            Secrets = [new Secret("RegistryPassword", "should-not-appear")],
        };
        var sw = new StringWriter();
        ProcessRunner.Print(plan, "T", null, sw);
        var s = sw.ToString();
        Assert.Contains("RegistryPassword", s);
        Assert.Contains("redacted", s);
        Assert.DoesNotContain("should-not-appear", s);
    }

    [Fact]
    public void Print_Throws_On_Null_Plan()
    {
        Assert.Throws<ArgumentNullException>(() => ProcessRunner.Print(null!, "T", null, TextWriter.Null));
    }

    [Fact]
    public void Print_Throws_On_Null_Writer()
    {
        var plan = new CommandPlan { Executable = "tool", Arguments = [] };
        Assert.Throws<ArgumentNullException>(() => ProcessRunner.Print(plan, "T", null, null!));
    }

    [Fact]
    public void Execute_Throws_On_Null_Plan()
    {
        Assert.Throws<ArgumentNullException>(() => ProcessRunner.Execute(null!));
    }
}
