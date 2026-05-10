using System.Reflection;

namespace Tamp;

/// <summary>
/// Base for attributes that auto-inject a computed value into a build's
/// property or field. Subclasses override <see cref="GetValue"/> to
/// produce the value at parameter-binding time.
/// </summary>
/// <remarks>
/// Distinct from <see cref="ParameterAttribute"/>: <c>[Parameter]</c>
/// reads from CLI / env / default; injection attributes compute the value
/// from the build context (the repository, the workspace, an external
/// system). The injection runs once at the start of <see cref="TampBuild.Execute{T}"/>.
/// </remarks>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
public abstract class ValueInjectionAttribute : Attribute
{
    /// <summary>
    /// Compute the value to inject into <paramref name="member"/>. Return
    /// null to leave the member at its declared default. Throw to fail
    /// fast at binding time.
    /// </summary>
    public abstract object? GetValue(MemberInfo member, Type memberType);
}
