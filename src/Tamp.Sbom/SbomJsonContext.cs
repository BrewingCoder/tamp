using System.Text.Json;
using System.Text.Json.Serialization;

namespace Tamp.Sbom;

/// <summary>
/// Source-generated <see cref="JsonSerializerContext"/> for the CycloneDX
/// type graph. Used by <see cref="SbomReader"/> and <see cref="SbomWriter"/>
/// so the package stays AOT/trimming-friendly.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = true)]
[JsonSerializable(typeof(CycloneDxBom))]
internal sealed partial class SbomJsonContext : JsonSerializerContext;
