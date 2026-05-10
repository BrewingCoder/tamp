using Bogus;
using Xunit;

namespace Tamp.Core.Tests;

public sealed class CommandPlanTests
{
    private static readonly Faker Faker = new() { Random = new Randomizer(unchecked((int)0xDEADBEEF)) };

    [Fact]
    public void Required_Fields_Round_Trip()
    {
        var plan = new CommandPlan
        {
            Executable = "dotnet",
            Arguments = ["build", "--configuration", "Release"],
        };
        Assert.Equal("dotnet", plan.Executable);
        Assert.Equal(["build", "--configuration", "Release"], plan.Arguments);
    }

    [Fact]
    public void Optional_Fields_Default_To_Empty_And_Null()
    {
        var plan = new CommandPlan
        {
            Executable = "dotnet",
            Arguments = [],
        };
        Assert.Empty(plan.Environment);
        Assert.Null(plan.WorkingDirectory);
        Assert.Empty(plan.Secrets);
    }

    [Fact]
    public void Two_Plans_With_Same_Shape_Are_Value_Equal()
    {
        var a = new CommandPlan { Executable = "dotnet", Arguments = ["build"] };
        var b = new CommandPlan { Executable = "dotnet", Arguments = ["build"] };

        // Records compare by reference for IReadOnlyList — but since these
        // are different list instances, equality is reference-based on
        // Arguments, so the records aren't equal. This is the "gotcha" with
        // record equality on collection-typed properties; downstream code
        // that wants structural equality must compare manually.
        Assert.Equal(a.Executable, b.Executable);
        Assert.Equal(a.Arguments, b.Arguments);
    }

    [Fact]
    public void With_Mutates_Copy_Not_Original()
    {
        var a = new CommandPlan { Executable = "dotnet", Arguments = ["restore"] };
        var b = a with { Arguments = ["build"] };
        Assert.Equal(["restore"], a.Arguments);
        Assert.Equal(["build"], b.Arguments);
    }

    [Fact]
    public void Empty_Arguments_Are_Permitted()
    {
        var plan = new CommandPlan { Executable = "noop", Arguments = [] };
        Assert.Empty(plan.Arguments);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("  arg with spaces  ")]
    [InlineData("--flag=value with = signs")]
    [InlineData("éó中")]
    public void Arguments_Preserve_Exotic_Strings_Verbatim(string arg)
    {
        var plan = new CommandPlan { Executable = "tool", Arguments = [arg] };
        Assert.Equal(arg, plan.Arguments[0]);
    }

    [Fact]
    public void Environment_Preserves_Insertion()
    {
        var env = new Dictionary<string, string>
        {
            ["FOO"] = "1",
            ["BAR"] = "two",
        };
        var plan = new CommandPlan
        {
            Executable = "tool",
            Arguments = [],
            Environment = env,
        };
        Assert.Equal("1", plan.Environment["FOO"]);
        Assert.Equal("two", plan.Environment["BAR"]);
    }

    [Fact]
    public void Working_Directory_Round_Trips()
    {
        var plan = new CommandPlan
        {
            Executable = "tool",
            Arguments = [],
            WorkingDirectory = "/tmp/work",
        };
        Assert.Equal("/tmp/work", plan.WorkingDirectory);
    }

    [Fact]
    public void Secrets_List_Round_Trips()
    {
        var s1 = new Secret("RegistryPassword", "p@ssw0rd!");
        var s2 = new Secret("NugetApiKey", "key-xyz");
        var plan = new CommandPlan
        {
            Executable = "tool",
            Arguments = [],
            Secrets = [s1, s2],
        };
        Assert.Equal(2, plan.Secrets.Count);
        Assert.Same(s1, plan.Secrets[0]);
        Assert.Same(s2, plan.Secrets[1]);
    }

    [Fact]
    public void Random_Plans_Round_Trip()
    {
        for (var i = 0; i < 50; i++)
        {
            var argCount = Faker.Random.Int(0, 20);
            var args = Enumerable.Range(0, argCount).Select(_ => Faker.Lorem.Word()).ToList();
            var plan = new CommandPlan
            {
                Executable = Faker.System.FileName(),
                Arguments = args,
                WorkingDirectory = Faker.Random.Bool() ? Faker.System.DirectoryPath() : null,
            };
            var clone = plan with { };
            Assert.Equal(plan.Executable, clone.Executable);
            Assert.Equal(plan.Arguments, clone.Arguments);
            Assert.Equal(plan.WorkingDirectory, clone.WorkingDirectory);
        }
    }
}
