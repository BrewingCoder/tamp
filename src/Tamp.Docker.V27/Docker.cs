namespace Tamp.Docker.V27;

/// <summary>
/// Tamp wrapper for the Docker 27.x CLI. Each method returns a
/// <see cref="CommandPlan"/> the runner dispatches or prints; nothing is
/// executed at call time.
/// </summary>
public static class Docker
{
    public static CommandPlan Login(Action<DockerLoginSettings> configure)
    {
        if (configure is null) throw new ArgumentNullException(nameof(configure));
        var s = new DockerLoginSettings();
        configure(s);
        return s.ToCommandPlan();
    }

    public static CommandPlan Logout(Action<DockerLogoutSettings>? configure = null)
    {
        var s = new DockerLogoutSettings();
        configure?.Invoke(s);
        return s.ToCommandPlan();
    }

    public static CommandPlan Build(Action<DockerBuildSettings> configure)
    {
        if (configure is null) throw new ArgumentNullException(nameof(configure));
        var s = new DockerBuildSettings();
        configure(s);
        return s.ToCommandPlan();
    }

    public static CommandPlan Tag(Action<DockerTagSettings> configure)
    {
        if (configure is null) throw new ArgumentNullException(nameof(configure));
        var s = new DockerTagSettings();
        configure(s);
        return s.ToCommandPlan();
    }

    public static CommandPlan Push(Action<DockerPushSettings> configure)
    {
        if (configure is null) throw new ArgumentNullException(nameof(configure));
        var s = new DockerPushSettings();
        configure(s);
        return s.ToCommandPlan();
    }

    public static CommandPlan Pull(Action<DockerPullSettings> configure)
    {
        if (configure is null) throw new ArgumentNullException(nameof(configure));
        var s = new DockerPullSettings();
        configure(s);
        return s.ToCommandPlan();
    }
}
