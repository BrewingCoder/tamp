using System.Text.Json;
using System.Text.Json.Serialization;

namespace Tamp.DependencyTrack.V1;

/// <summary>
/// Internal DTOs for the wire-level JSON shapes DT expects/returns.
/// Adopters don't see these — they get the typed
/// <see cref="DependencyTrackUploadResult"/> back from the client.
/// </summary>
internal sealed record BomUploadRequest
{
    [JsonPropertyName("project")]
    public string Project { get; init; } = "";

    /// <summary>Base64-encoded CycloneDX BOM bytes.</summary>
    [JsonPropertyName("bom")]
    public string Bom { get; init; } = "";
}

internal sealed record BomUploadResponse
{
    [JsonPropertyName("token")]
    public string Token { get; init; } = "";
}

internal sealed record BomTokenResponse
{
    [JsonPropertyName("processing")]
    public bool Processing { get; init; }
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(BomUploadRequest))]
[JsonSerializable(typeof(BomUploadResponse))]
[JsonSerializable(typeof(BomTokenResponse))]
internal sealed partial class DependencyTrackJsonContext : JsonSerializerContext;
