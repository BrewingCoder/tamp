using System.Text.Json;

namespace Tamp.Sbom;

/// <summary>Serialises CycloneDX BOMs to JSON strings and files.</summary>
public static class SbomWriter
{
    public static string Serialize(CycloneDxBom bom)
    {
        if (bom is null) throw new ArgumentNullException(nameof(bom));
        return JsonSerializer.Serialize(bom, SbomJsonContext.Default.CycloneDxBom);
    }

    public static void WriteToFile(CycloneDxBom bom, AbsolutePath path)
    {
        if (bom is null) throw new ArgumentNullException(nameof(bom));
        if (path is null) throw new ArgumentNullException(nameof(path));
        File.WriteAllText(path, Serialize(bom));
    }

    public static Task WriteToFileAsync(CycloneDxBom bom, AbsolutePath path, CancellationToken cancellationToken = default)
    {
        if (bom is null) throw new ArgumentNullException(nameof(bom));
        if (path is null) throw new ArgumentNullException(nameof(path));
        return File.WriteAllTextAsync(path, Serialize(bom), cancellationToken);
    }
}
