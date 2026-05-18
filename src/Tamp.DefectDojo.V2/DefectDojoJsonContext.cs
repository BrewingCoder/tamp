using System.Text.Json;
using System.Text.Json.Serialization;

namespace Tamp.DefectDojo.V2;

internal sealed record ImportScanResponse
{
    [JsonPropertyName("test")]
    public int? Test { get; init; }

    [JsonPropertyName("test_id")]
    public int? TestId { get; init; }

    [JsonPropertyName("statistics")]
    public ImportScanStatistics? Statistics { get; init; }
}

internal sealed record ImportScanStatistics
{
    [JsonPropertyName("after")]
    public ImportScanStatisticsBlock? After { get; init; }

    [JsonPropertyName("before")]
    public ImportScanStatisticsBlock? Before { get; init; }
}

internal sealed record ImportScanStatisticsBlock
{
    [JsonPropertyName("info")]
    public ImportScanStatisticsBucket? Info { get; init; }

    [JsonPropertyName("low")]
    public ImportScanStatisticsBucket? Low { get; init; }

    [JsonPropertyName("medium")]
    public ImportScanStatisticsBucket? Medium { get; init; }

    [JsonPropertyName("high")]
    public ImportScanStatisticsBucket? High { get; init; }

    [JsonPropertyName("critical")]
    public ImportScanStatisticsBucket? Critical { get; init; }

    [JsonPropertyName("total")]
    public ImportScanStatisticsBucket? Total { get; init; }
}

internal sealed record ImportScanStatisticsBucket
{
    [JsonPropertyName("created")]
    public int? Created { get; init; }

    [JsonPropertyName("closed")]
    public int? Closed { get; init; }
}

[JsonSourceGenerationOptions(
    PropertyNameCaseInsensitive = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(ImportScanResponse))]
internal sealed partial class DefectDojoJsonContext : JsonSerializerContext;
