namespace Tamp;

/// <summary>
/// Marker attribute that registers an <see cref="IBuildReporter"/>-typed
/// field or property on a build script as an additional event sink for the
/// build's executor. The framework collects every <c>[BuildReporter]</c>-
/// marked member after parameter binding and composes them with the
/// CLI-selected default reporter (Noop or Json) via a
/// <see cref="CompositeBuildReporter"/>, so user-supplied reporters
/// (Telegram, Slack, Discord, custom) fire alongside the built-in ones.
/// </summary>
/// <remarks>
/// <para>
/// Unlike <see cref="ValueInjectionAttribute"/> subclasses, this is a pure
/// marker — the framework reads the member's current value rather than
/// computing one. Construct the reporter however you want in the field
/// initializer:
/// </para>
/// <code>
/// [BuildReporter] readonly IBuildReporter TelegramNotify =
///     TelegramBuildReporter.FromEnvironment();
/// </code>
/// <para>
/// Members whose value is <see langword="null"/> at collection time are
/// silently skipped — handy for "only when configured" reporters that
/// the adopter constructs from optional env vars.
/// </para>
/// </remarks>
[System.AttributeUsage(System.AttributeTargets.Field | System.AttributeTargets.Property, AllowMultiple = false)]
public sealed class BuildReporterAttribute : System.Attribute
{
}
