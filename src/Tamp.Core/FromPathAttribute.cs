using System.Reflection;

namespace Tamp;

/// <summary>
/// Auto-inject a <see cref="Tool"/> resolved from the operating system's <c>PATH</c>.
/// On Windows the resolver probes <c>.cmd</c>, <c>.exe</c>, <c>.bat</c>, <c>.ps1</c>, and the extension-less name.
/// </summary>
/// <remarks>
/// <para>
/// Pair with native-tool wrappers (<c>Tamp.Docker.V27</c>, <c>Tamp.Yarn.V4</c>, <c>Tamp.GitHubCli.V2</c>) that need a
/// real PATH lookup rather than a NuGet-installed .NET tool. For .NET tools, use <see cref="NuGetPackageAttribute"/> instead.
/// </para>
/// <code>
/// [FromPath("docker")] readonly Tool Docker = null!;
/// [FromPath("yarn", Optional = true)] readonly Tool? Yarn = null;
/// </code>
/// </remarks>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
public sealed class FromPathAttribute : ValueInjectionAttribute
{
    /// <summary>Executable name without extension. Required.</summary>
    public string Name { get; }

    /// <summary>When true, missing executable injects <c>null</c> rather than throwing at binding time.</summary>
    public bool Optional { get; set; }

    public FromPathAttribute(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Name must not be null or whitespace.", nameof(name));
        Name = name;
    }

    public override object? GetValue(MemberInfo member, Type memberType)
    {
        var tool = Tool.TryFromPath(Name);
        if (tool is null && !Optional)
            throw new InvalidOperationException(
                $"[FromPath(\"{Name}\")] on '{member.DeclaringType?.Name}.{member.Name}' — could not find '{Name}' on PATH. " +
                "Install it and ensure the install directory is on PATH, or mark the attribute as Optional = true.");
        return tool;
    }
}
