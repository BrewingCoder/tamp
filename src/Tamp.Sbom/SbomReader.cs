using System.Text.Json;

namespace Tamp.Sbom;

/// <summary>Loads CycloneDX BOMs from JSON strings and files.</summary>
public static class SbomReader
{
    /// <summary>Parse a CycloneDX BOM from a JSON string.</summary>
    /// <exception cref="InvalidDataException">The JSON is not a valid CycloneDX BOM.</exception>
    public static CycloneDxBom Parse(string json)
    {
        if (json is null) throw new ArgumentNullException(nameof(json));
        try
        {
            var bom = JsonSerializer.Deserialize(json, SbomJsonContext.Default.CycloneDxBom);
            if (bom is null) throw new InvalidDataException("CycloneDX payload deserialised to null.");
            return bom;
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"Not a valid CycloneDX BOM: {ex.Message}", ex);
        }
    }

    public static CycloneDxBom LoadFromFile(AbsolutePath path)
    {
        if (path is null) throw new ArgumentNullException(nameof(path));
        return Parse(File.ReadAllText(path));
    }

    public static async Task<CycloneDxBom> LoadFromFileAsync(AbsolutePath path, CancellationToken cancellationToken = default)
    {
        if (path is null) throw new ArgumentNullException(nameof(path));
        var json = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
        return Parse(json);
    }
}
