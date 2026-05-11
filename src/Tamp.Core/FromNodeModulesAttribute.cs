using System.Reflection;

namespace Tamp;

/// <summary>
/// Auto-inject a <see cref="Tool"/> resolved from <c>&lt;projectRoot&gt;/node_modules/.bin/&lt;name&gt;</c>.
/// On Windows the resolver probes the <c>.cmd</c> shim first.
/// </summary>
/// <remarks>
/// <para>
/// Use for tools that are workspace devDeps rather than globally installed: <c>turbo</c>, <c>vite</c>, <c>vitest</c>,
/// <c>tsc</c>, <c>eslint</c>. The binary doesn't exist until <c>yarn install</c> (or <c>npm install</c>) runs, so
/// targets that consume this Tool should <c>DependsOn(nameof(YarnInstall))</c>.
/// </para>
/// <code>
/// [FromNodeModules("turbo")] readonly Tool Turbo = null!;
/// [FromNodeModules("vitest", Optional = true)] readonly Tool? Vitest = null;
/// </code>
/// <para>
/// By default <c>ProjectRoot</c> is the build's <see cref="TampBuild.RootDirectory"/>. Override via
/// <see cref="ProjectRoot"/> for nested workspaces (e.g. <c>"frontend"</c>).
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
public sealed class FromNodeModulesAttribute : ValueInjectionAttribute
{
    /// <summary>Executable name without extension. Required.</summary>
    public string Name { get; }

    /// <summary>
    /// Project root containing <c>node_modules/</c>, relative to <see cref="TampBuild.RootDirectory"/> or absolute.
    /// Defaults to <see cref="TampBuild.RootDirectory"/> itself.
    /// </summary>
    public string? ProjectRoot { get; set; }

    /// <summary>When true, missing executable injects <c>null</c> rather than throwing at binding time.</summary>
    public bool Optional { get; set; }

    public FromNodeModulesAttribute(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Name must not be null or whitespace.", nameof(name));
        Name = name;
    }

    public override object? GetValue(MemberInfo member, Type memberType)
    {
        var root = ResolveRoot();
        var tool = Tool.TryFromNodeModules(Name, root);
        if (tool is null && !Optional)
            throw new InvalidOperationException(
                $"[FromNodeModules(\"{Name}\")] on '{member.DeclaringType?.Name}.{member.Name}' — could not find '{Name}' under " +
                $"{root / "node_modules" / ".bin"}. Did you run `yarn install` (or `npm install`)? Mark the attribute as Optional = true to inject null instead.");
        return tool;
    }

    private AbsolutePath ResolveRoot()
    {
        if (string.IsNullOrEmpty(ProjectRoot)) return TampBuild.RootDirectory;
        return Path.IsPathRooted(ProjectRoot)
            ? AbsolutePath.Create(ProjectRoot)
            : TampBuild.RootDirectory / ProjectRoot;
    }
}
