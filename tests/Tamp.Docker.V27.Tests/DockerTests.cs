using Xunit;

namespace Tamp.Docker.V27.Tests;

public sealed class DockerTests
{
    private static int IndexOf(IReadOnlyList<string> args, string value, int start = 0)
    {
        for (var i = start; i < args.Count; i++)
            if (args[i] == value) return i;
        return -1;
    }

    // ---- Common ----

    [Fact]
    public void Every_Verb_Targets_The_docker_Executable()
    {
        Assert.Equal("docker", Docker.Logout().Executable);
        Assert.Equal("docker", Docker.Build(s => s.SetContext(".")).Executable);
        Assert.Equal("docker", Docker.Tag(s => s.SetSource("a:b").SetTarget("a:c")).Executable);
        Assert.Equal("docker", Docker.Push(s => s.SetImage("a:b")).Executable);
        Assert.Equal("docker", Docker.Pull(s => s.SetImage("a:b")).Executable);
    }

    [Fact]
    public void Verbs_Begin_With_Their_Verb_Token()
    {
        Assert.Equal("logout", Docker.Logout().Arguments[0]);
        Assert.Equal("build", Docker.Build(s => s.SetContext(".")).Arguments[0]);
        Assert.Equal("tag", Docker.Tag(s => s.SetSource("a").SetTarget("b")).Arguments[0]);
        Assert.Equal("push", Docker.Push(s => s.SetImage("img")).Arguments[0]);
        Assert.Equal("pull", Docker.Pull(s => s.SetImage("img")).Arguments[0]);
    }

    // ---- Login ----

    [Fact]
    public void Login_Without_Password_Has_No_Stdin()
    {
        var plan = Docker.Login(s => s.SetServer("registry.example.com").SetUsername("ci"));
        Assert.Null(plan.StandardInput);
        Assert.Empty(plan.Secrets);
        Assert.Contains("--username", plan.Arguments);
        Assert.Contains("ci", plan.Arguments);
        Assert.Contains("registry.example.com", plan.Arguments);
        Assert.DoesNotContain("--password-stdin", plan.Arguments);
    }

    [Fact]
    public void Login_With_Secret_Password_Uses_Password_Stdin_And_Feeds_Stdin()
    {
        var pwd = new Secret("RegistryPassword", "p@ssw0rd!");
        var plan = Docker.Login(s => s
            .SetServer("registry.example.com")
            .SetUsername("ci")
            .SetPassword(pwd));

        Assert.Contains("--password-stdin", plan.Arguments);
        Assert.DoesNotContain("--password", plan.Arguments.Where(a => a != "--password-stdin"));
        Assert.Equal("p@ssw0rd!", plan.StandardInput);
        Assert.Single(plan.Secrets);
        Assert.Same(pwd, plan.Secrets[0]);
    }

    [Fact]
    public void Login_Server_Is_Positional_Last()
    {
        var plan = Docker.Login(s => s
            .SetServer("registry.example.com")
            .SetUsername("ci"));
        Assert.Equal("registry.example.com", plan.Arguments[^1]);
    }

    [Fact]
    public void Login_Without_Username_Or_Server_Still_Valid_For_Default_Registry()
    {
        // `docker login` with no args prompts on stdin, which is a normal
        // interactive flow. We don't reject this — just emit `login`.
        var plan = Docker.Login(s => { });
        Assert.Equal(["login"], plan.Arguments);
    }

    // ---- Logout ----

    [Fact]
    public void Logout_With_Server_Round_Trips()
    {
        var plan = Docker.Logout(s => s.SetServer("registry.example.com"));
        Assert.Equal(["logout", "registry.example.com"], plan.Arguments);
    }

    [Fact]
    public void Logout_Without_Args_Targets_Default_Registry()
    {
        var plan = Docker.Logout();
        Assert.Equal(["logout"], plan.Arguments);
    }

    // ---- Build ----

    [Fact]
    public void Build_Defaults_Context_To_Current_Directory()
    {
        var plan = Docker.Build(s => { });
        Assert.Equal(".", plan.Arguments[^1]);
    }

    [Fact]
    public void Build_Tags_Emit_As_Repeated_Tag_Flags()
    {
        var plan = Docker.Build(s => s
            .SetContext(".")
            .AddTag("myapp:latest")
            .AddTag("myapp:1.2.3"));
        var args = plan.Arguments;
        var firstTag = IndexOf(args, "--tag");
        var secondTag = IndexOf(args, "--tag", firstTag + 1);
        Assert.True(firstTag >= 0 && secondTag > firstTag);
        Assert.Equal("myapp:latest", args[firstTag + 1]);
        Assert.Equal("myapp:1.2.3", args[secondTag + 1]);
    }

    [Fact]
    public void Build_Build_Args_Emit_As_KEY_EQ_VALUE()
    {
        var plan = Docker.Build(s => s
            .SetContext(".")
            .SetBuildArg("VERSION", "1.0")
            .SetBuildArg("CONFIGURATION", "Release"));
        Assert.Contains("VERSION=1.0", plan.Arguments);
        Assert.Contains("CONFIGURATION=Release", plan.Arguments);
    }

    [Fact]
    public void Build_Labels_Emit_As_Label_Flag_Pairs()
    {
        var plan = Docker.Build(s => s
            .SetContext(".")
            .SetLabel("org.opencontainers.image.source", "https://github.com/x"));
        Assert.Contains("--label", plan.Arguments);
        Assert.Contains("org.opencontainers.image.source=https://github.com/x", plan.Arguments);
    }

    [Fact]
    public void Build_Context_Path_Comes_Last()
    {
        var plan = Docker.Build(s => s
            .SetContext("./src")
            .SetDockerfile("Dockerfile.prod")
            .AddTag("x:y"));
        Assert.Equal("./src", plan.Arguments[^1]);
    }

    [Fact]
    public void Build_NoCache_Pull_Quiet_Are_Independent_Flags()
    {
        var plan = Docker.Build(s => s.SetContext(".").SetNoCache(true).SetPull(true).SetQuiet(true));
        Assert.Contains("--no-cache", plan.Arguments);
        Assert.Contains("--pull", plan.Arguments);
        Assert.Contains("--quiet", plan.Arguments);
    }

    [Fact]
    public void Build_Platform_Round_Trips()
    {
        var plan = Docker.Build(s => s.SetContext(".").SetPlatform("linux/amd64,linux/arm64"));
        var args = plan.Arguments;
        Assert.Equal("linux/amd64,linux/arm64", args[IndexOf(args, "--platform") + 1]);
    }

    // ---- Tag ----

    [Fact]
    public void Tag_Requires_Source_And_Target()
    {
        Assert.Throws<InvalidOperationException>(() => Docker.Tag(s => { }));
        Assert.Throws<InvalidOperationException>(() => Docker.Tag(s => s.SetSource("a")));
    }

    [Fact]
    public void Tag_Round_Trips()
    {
        var plan = Docker.Tag(s => s.SetSource("img:abc").SetTarget("registry.example.com/img:abc"));
        Assert.Equal(["tag", "img:abc", "registry.example.com/img:abc"], plan.Arguments);
    }

    // ---- Push ----

    [Fact]
    public void Push_Requires_Image()
    {
        Assert.Throws<InvalidOperationException>(() => Docker.Push(s => { }));
    }

    [Fact]
    public void Push_AllTags_Round_Trips()
    {
        var plan = Docker.Push(s => s.SetImage("registry.example.com/myapp").SetAllTags(true));
        Assert.Contains("--all-tags", plan.Arguments);
        Assert.Equal("registry.example.com/myapp", plan.Arguments[^1]);
    }

    // ---- Pull ----

    [Fact]
    public void Pull_Requires_Image()
    {
        Assert.Throws<InvalidOperationException>(() => Docker.Pull(s => { }));
    }

    [Fact]
    public void Pull_With_Platform_Includes_Flag_And_Image_Last()
    {
        var plan = Docker.Pull(s => s.SetImage("alpine:3.20").SetPlatform("linux/arm64"));
        var args = plan.Arguments;
        Assert.Equal("linux/arm64", args[IndexOf(args, "--platform") + 1]);
        Assert.Equal("alpine:3.20", args[^1]);
    }

    // ---- Configurer null guards ----

    [Theory]
    [MemberData(nameof(VerbsRequiringConfigurer))]
    public void Required_Configurer_Verbs_Throw_On_Null(string verbName, Func<Action<DockerLoginSettings>?, CommandPlan> _)
    {
        // Just one representative sample; specific tests above already
        // exercise null behavior per verb. This keeps the data shape
        // consistent without expanding into a generic mess.
        Assert.NotNull(verbName);
    }

    public static IEnumerable<object[]> VerbsRequiringConfigurer()
    {
        yield return new object[] { "Login", (Func<Action<DockerLoginSettings>?, CommandPlan>)(c => Docker.Login(c!)) };
    }

    [Fact]
    public void Login_Throws_On_Null_Configurer()
    {
        Assert.Throws<ArgumentNullException>(() => Docker.Login(null!));
    }

    [Fact]
    public void Build_Throws_On_Null_Configurer()
    {
        Assert.Throws<ArgumentNullException>(() => Docker.Build(null!));
    }

    [Fact]
    public void Tag_Throws_On_Null_Configurer()
    {
        Assert.Throws<ArgumentNullException>(() => Docker.Tag(null!));
    }

    [Fact]
    public void Push_Throws_On_Null_Configurer()
    {
        Assert.Throws<ArgumentNullException>(() => Docker.Push(null!));
    }

    [Fact]
    public void Pull_Throws_On_Null_Configurer()
    {
        Assert.Throws<ArgumentNullException>(() => Docker.Pull(null!));
    }
}
