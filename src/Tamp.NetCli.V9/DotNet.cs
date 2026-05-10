namespace Tamp.NetCli.V9;

/// <summary>
/// Tamp wrapper for the .NET 9 SDK CLI. Each method returns a
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
}
