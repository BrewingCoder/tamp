using System.Text.Json;

namespace Tamp.Sarif;

/// <summary>
/// Loads SARIF 2.1.0 logs from JSON strings and files.
/// </summary>
public static class SarifReader
{
    /// <summary>Parse a SARIF log from a JSON string.</summary>
    /// <exception cref="InvalidDataException">The JSON is not a valid SARIF 2.1.0 log.</exception>
    public static SarifLog Parse(string json)
    {
        if (json is null) throw new ArgumentNullException(nameof(json));
        try
        {
            var log = JsonSerializer.Deserialize(json, SarifJsonContext.Default.SarifLog);
            if (log is null) throw new InvalidDataException("SARIF payload deserialised to null.");
            return log;
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"Not a valid SARIF 2.1.0 log: {ex.Message}", ex);
        }
    }

    /// <summary>Load a SARIF log from a file path.</summary>
    public static SarifLog LoadFromFile(AbsolutePath path)
    {
        if (path is null) throw new ArgumentNullException(nameof(path));
        return Parse(File.ReadAllText(path));
    }

    /// <summary>Load a SARIF log from a file path asynchronously.</summary>
    public static async Task<SarifLog> LoadFromFileAsync(AbsolutePath path, CancellationToken cancellationToken = default)
    {
        if (path is null) throw new ArgumentNullException(nameof(path));
        var json = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
        return Parse(json);
    }
}
