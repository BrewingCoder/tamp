namespace Tamp.CycloneDx.V6;

/// <summary>
/// Output format for the generated CycloneDX BOM. Maps to the
/// <c>--output-format</c> flag (dotnet-CycloneDX 6.x replaced the older
/// <c>--json</c> boolean with a typed enum).
/// </summary>
public enum CycloneDxFormat
{
    /// <summary>Tool auto-detection (XML by default in 6.x).</summary>
    Auto,

    /// <summary>JSON — the Wave 1 default (Dependency-Track consumes JSON).</summary>
    Json,

    /// <summary>JSON with relaxed escaping (6.x added this for environments that choke on the strict form).</summary>
    UnsafeJson,

    /// <summary>XML — CycloneDX's original format; still widely supported.</summary>
    Xml,
}
