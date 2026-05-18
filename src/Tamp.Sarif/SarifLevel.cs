using System.Text.Json;
using System.Text.Json.Serialization;

namespace Tamp.Sarif;

/// <summary>
/// SARIF 2.1.0 result severity (§3.27.10). Serialised as lowercase string
/// per the spec; deserialisation accepts the canonical forms and rejects
/// integer values (SARIF tooling never emits them).
/// </summary>
[JsonConverter(typeof(SarifLevelConverter))]
public enum SarifLevel
{
    None,
    Note,
    Warning,
    Error,
}

internal sealed class SarifLevelConverter()
    : JsonStringEnumConverter<SarifLevel>(JsonNamingPolicy.CamelCase, allowIntegerValues: false);
