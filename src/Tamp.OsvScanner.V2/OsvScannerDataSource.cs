namespace Tamp.OsvScanner.V2;

/// <summary>
/// Source the scanner queries for package metadata. <see cref="DepsDev"/>
/// is the tool default (Google's hosted aggregator); <see cref="Native"/>
/// hits each ecosystem's own registry (slower but avoids the deps.dev
/// dependency for air-gapped or sovereign deployments).
/// </summary>
public enum OsvScannerDataSource
{
    DepsDev,
    Native,
}
