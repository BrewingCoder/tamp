namespace Tamp.CycloneDx.V5;

/// <summary>Output format for the generated CycloneDX BOM.</summary>
public enum CycloneDxFormat
{
    /// <summary>JSON (the Wave 1 default — Dependency-Track consumes JSON).</summary>
    Json,

    /// <summary>XML (CycloneDX's original format; still widely supported).</summary>
    Xml,

    /// <summary>Emit both JSON and XML side by side.</summary>
    Both,
}
