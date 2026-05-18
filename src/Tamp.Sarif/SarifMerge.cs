namespace Tamp.Sarif;

/// <summary>
/// Combines multiple SARIF logs into one. Preserves run boundaries (each
/// input run survives as a distinct run in the output) so that the
/// originating tool of every finding remains identifiable downstream.
/// </summary>
public static class SarifMerge
{
    /// <summary>
    /// Combine the runs of every input log into a single output log. The
    /// output's <c>version</c> and <c>$schema</c> are taken from the first
    /// non-null/non-empty value encountered; defaults are used when none are
    /// present.
    /// </summary>
    /// <exception cref="ArgumentNullException">The input sequence is null.</exception>
    public static SarifLog Combine(IEnumerable<SarifLog> logs)
    {
        if (logs is null) throw new ArgumentNullException(nameof(logs));

        var materialised = logs.Where(static l => l is not null).ToList();
        var runs = materialised.SelectMany(static l => l.Runs).ToList();

        var version = materialised
            .Select(static l => l.Version)
            .FirstOrDefault(static v => !string.IsNullOrEmpty(v))
            ?? "2.1.0";

        var schema = materialised
            .Select(static l => l.Schema)
            .FirstOrDefault(static s => !string.IsNullOrEmpty(s))
            ?? "https://json.schemastore.org/sarif-2.1.0.json";

        return new SarifLog
        {
            Version = version,
            Schema = schema,
            Runs = runs,
        };
    }

    /// <summary>
    /// Combine the inputs (see <see cref="Combine"/>) and then collapse
    /// duplicate results via <see cref="SarifDedup.Distinct"/>. The
    /// common path when stitching together SARIF from sources that
    /// re-analyse the same source per TFM (Roslyn) or scan overlapping
    /// directories (multi-tool pipelines).
    /// </summary>
    /// <exception cref="ArgumentNullException">The input sequence is null.</exception>
    public static SarifLog CombineDistinct(IEnumerable<SarifLog> logs)
        => SarifDedup.Distinct(Combine(logs));
}
