using System.Text.Json;
using System.Text.Json.Serialization;

namespace Tamp.Sarif;

/// <summary>
/// Source-generated <see cref="JsonSerializerContext"/> for the SARIF type
/// graph. Used by <see cref="SarifReader"/> and <see cref="SarifWriter"/>
/// so the package stays AOT/trimming-friendly — no reflection-based
/// serialisation at runtime.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = true)]
[JsonSerializable(typeof(SarifLog))]
internal sealed partial class SarifJsonContext : JsonSerializerContext;
