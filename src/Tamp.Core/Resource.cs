namespace Tamp;

/// <summary>
/// Declarative resource consumption. Targets that read or write the same
/// <see cref="Resource"/> are scheduled with respect to each other based on
/// their <see cref="ConsumeMode"/>: shared consumers can run in parallel,
/// exclusive consumers serialize.
/// </summary>
/// <remarks>
/// This is the mechanism that prevents <c>dotnet build</c> and
/// <c>dotnet test --no-build</c> from racing each other on the
/// <see cref="BuildCache.Dotnet"/> resource even when the build author has
/// not added an explicit dependency between them.
/// </remarks>
public abstract record Resource(string Kind, string Identifier)
{
    public override string ToString() => $"{Kind}:{Identifier}";

    public static class BuildCache
    {
        public static readonly Resource Dotnet = new BuildCacheResource("Dotnet");
        public static readonly Resource Yarn = new BuildCacheResource("Yarn");
        public static readonly Resource Nuget = new BuildCacheResource("Nuget");
    }

    public static class Network
    {
        public static readonly Resource Internet = new NetworkResource("Internet");
        public static Resource Registry(string host) => new NetworkResource($"Registry:{host}");
    }

    public static class Process
    {
        public static readonly Resource Docker = new ProcessResource("Docker");
    }

    public static Resource Filesystem(string path) => new FilesystemResource(path);
}

internal sealed record BuildCacheResource(string Name) : Resource("BuildCache", Name);
internal sealed record NetworkResource(string Name) : Resource("Network", Name);
internal sealed record ProcessResource(string Name) : Resource("Process", Name);
internal sealed record FilesystemResource(string Path) : Resource("Filesystem", Path);

public enum ConsumeMode
{
    /// <summary>Multiple targets may hold the resource simultaneously.</summary>
    Shared,
    /// <summary>Only one target at a time may hold the resource.</summary>
    Exclusive,
}
