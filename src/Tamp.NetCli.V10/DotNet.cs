namespace Tamp.NetCli.V10;

/// <summary>
/// Tamp wrapper for the .NET 10 SDK CLI. Each method returns a
/// <see cref="CommandPlan"/> the runner dispatches or prints; nothing is
/// executed at call time.
/// </summary>
/// <remarks>
/// Settings classes are mutable while the configurer runs, then frozen
/// into the returned <see cref="CommandPlan"/>. Pass <c>null</c> as the
/// configurer to use the verb's defaults.
/// </remarks>
public static class DotNet
{
    public static CommandPlan Restore(Action<DotNetRestoreSettings>? configure = null)
    {
        var s = new DotNetRestoreSettings();
        configure?.Invoke(s);
        return s.ToCommandPlan();
    }

    public static CommandPlan Build(Action<DotNetBuildSettings>? configure = null)
    {
        var s = new DotNetBuildSettings();
        configure?.Invoke(s);
        return s.ToCommandPlan();
    }

    public static CommandPlan Test(Action<DotNetTestSettings>? configure = null)
    {
        var s = new DotNetTestSettings();
        configure?.Invoke(s);
        return s.ToCommandPlan();
    }

    public static CommandPlan Pack(Action<DotNetPackSettings>? configure = null)
    {
        var s = new DotNetPackSettings();
        configure?.Invoke(s);
        return s.ToCommandPlan();
    }

    public static CommandPlan Publish(Action<DotNetPublishSettings>? configure = null)
    {
        var s = new DotNetPublishSettings();
        configure?.Invoke(s);
        return s.ToCommandPlan();
    }

    /// <summary>
    /// <c>dotnet nuget push</c>. Pushes a <c>.nupkg</c> (or a glob of them)
    /// to a NuGet feed. Pass the API key as a typed <see cref="Secret"/> so
    /// it's registered with the runner's redaction table.
    /// </summary>
    public static CommandPlan NuGetPush(Action<DotNetNuGetPushSettings> configure)
    {
        if (configure is null) throw new ArgumentNullException(nameof(configure));
        var s = new DotNetNuGetPushSettings();
        configure(s);
        return s.ToCommandPlan();
    }
}
