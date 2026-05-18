namespace Tamp.OsvScanner.V2;

/// <summary>
/// Output format for <c>osv-scanner scan source --format</c>. The
/// Wave 1/2 chain consumes <see cref="Sarif"/> by default; CycloneDX
/// variants are useful when adopters want VEX-shaped output piped back
/// into Dependency-Track instead of (or in addition to) SARIF.
/// </summary>
public enum OsvScannerFormat
{
    /// <summary>SARIF 2.1.0 — the Wave 1 chain default.</summary>
    Sarif,

    /// <summary>OSV-Scanner's native JSON shape (richer than SARIF; not in the standard pipeline).</summary>
    Json,

    /// <summary>Human-readable table (the tool default when --format is omitted).</summary>
    Table,

    /// <summary>Markdown summary.</summary>
    Markdown,

    /// <summary>HTML report; pairs with --serve for browser display.</summary>
    Html,

    /// <summary>CycloneDX 1.4 with vulnerability section populated (no inline VEX prior to 1.5).</summary>
    CycloneDx14,

    /// <summary>CycloneDX 1.5 with inline VEX — pairs with DT/DD when round-tripping a vex-enriched BOM.</summary>
    CycloneDx15,

    /// <summary>SPDX 2.3 with vulnerability annotations.</summary>
    Spdx23,

    /// <summary>GitHub Actions annotation format (for inline PR annotations).</summary>
    GhAnnotations,
}
