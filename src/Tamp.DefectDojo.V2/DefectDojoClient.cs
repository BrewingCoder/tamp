using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Tamp.Sarif;

namespace Tamp.DefectDojo.V2;

/// <summary>
/// REST client for DefectDojo API v2. Two operations cover the Wave 1
/// sink leg of the chain:
/// <list type="bullet">
///   <item><see cref="ImportScanAsync"/> — POST <c>/api/v2/import-scan/</c> (first push of a scan into an engagement).</item>
///   <item><see cref="ReimportScanAsync"/> — POST <c>/api/v2/reimport-scan/</c> (every subsequent push; reconciles against prior scan so triage notes survive).</item>
/// </list>
/// Auth is <c>Authorization: Token &lt;api-v2-key&gt;</c> (note the
/// <c>Token</c> scheme, not <c>Bearer</c>). Bodies are multipart-form
/// per DD's documented contract.
/// </summary>
public sealed class DefectDojoClient : IDisposable
{
    private readonly HttpClient _http;
    private readonly bool _ownsHttp;

    public DefectDojoClient(DefectDojoSettings settings)
        : this(settings, http: null, ownsHttp: true)
    {
    }

    internal DefectDojoClient(DefectDojoSettings settings, HttpClient? http, bool ownsHttp = false)
    {
        if (settings is null) throw new ArgumentNullException(nameof(settings));

        if (http is null)
        {
            _http = new HttpClient { BaseAddress = settings.BaseUrl };
            _ownsHttp = true;
        }
        else
        {
            _http = http;
            if (_http.BaseAddress is null) _http.BaseAddress = settings.BaseUrl;
            _ownsHttp = ownsHttp;
        }

        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Token", settings.RevealToken());
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    /// <summary>First push of a scan into an engagement. Use ReimportScanAsync for subsequent pushes to preserve triage continuity.</summary>
    public Task<DefectDojoScanResult> ImportScanAsync(
        DefectDojoScanType scanType,
        int engagementId,
        string scanPayload,
        DefectDojoImportOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        return PostScanAsync("api/v2/import-scan/", scanType, engagementId, scanPayload, options, cancellationToken);
    }

    /// <summary>Subsequent push. Marks resolved findings inactive, reactivates regressions, preserves triage notes.</summary>
    public Task<DefectDojoScanResult> ReimportScanAsync(
        DefectDojoScanType scanType,
        int engagementId,
        string scanPayload,
        DefectDojoImportOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        return PostScanAsync("api/v2/reimport-scan/", scanType, engagementId, scanPayload, options, cancellationToken);
    }

    /// <summary>Convenience: serialise a Tamp.Sarif <see cref="SarifLog"/> and import it.</summary>
    public Task<DefectDojoScanResult> ImportSarifAsync(int engagementId, SarifLog log, DefectDojoImportOptions? options = null, CancellationToken cancellationToken = default)
    {
        if (log is null) throw new ArgumentNullException(nameof(log));
        return ImportScanAsync(DefectDojoScanType.Sarif, engagementId, SarifWriter.Serialize(log), options, cancellationToken);
    }

    /// <summary>Convenience: serialise a Tamp.Sarif <see cref="SarifLog"/> and reimport it.</summary>
    public Task<DefectDojoScanResult> ReimportSarifAsync(int engagementId, SarifLog log, DefectDojoImportOptions? options = null, CancellationToken cancellationToken = default)
    {
        if (log is null) throw new ArgumentNullException(nameof(log));
        return ReimportScanAsync(DefectDojoScanType.Sarif, engagementId, SarifWriter.Serialize(log), options, cancellationToken);
    }

    private async Task<DefectDojoScanResult> PostScanAsync(
        string path,
        DefectDojoScanType scanType,
        int engagementId,
        string scanPayload,
        DefectDojoImportOptions? options,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(scanPayload)) throw new ArgumentException("Scan payload must be non-empty.", nameof(scanPayload));

        options ??= new DefectDojoImportOptions();

        using var form = new MultipartFormDataContent
        {
            { new StringContent(scanType.ToWireValue(), Encoding.UTF8), "scan_type" },
            { new StringContent(engagementId.ToString(CultureInfo.InvariantCulture), Encoding.UTF8), "engagement" },
            { new StringContent(options.Active ? "true" : "false", Encoding.UTF8), "active" },
            { new StringContent(options.Verified ? "true" : "false", Encoding.UTF8), "verified" },
            { new StringContent(options.CloseOldFindings ? "true" : "false", Encoding.UTF8), "close_old_findings" },
        };

        if (options.ScanDate is { } scanDate)
            form.Add(new StringContent(scanDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)), "scan_date");
        if (!string.IsNullOrEmpty(options.BuildId))
            form.Add(new StringContent(options.BuildId, Encoding.UTF8), "build_id");
        if (!string.IsNullOrEmpty(options.ProductName))
            form.Add(new StringContent(options.ProductName, Encoding.UTF8), "product_name");
        if (!string.IsNullOrEmpty(options.EngagementName))
            form.Add(new StringContent(options.EngagementName, Encoding.UTF8), "engagement_name");
        if (!string.IsNullOrEmpty(options.TestTitle))
            form.Add(new StringContent(options.TestTitle, Encoding.UTF8), "test_title");
        if (options.AutoCreateContext)
            form.Add(new StringContent("true", Encoding.UTF8), "auto_create_context");

        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes(scanPayload));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        form.Add(fileContent, "file", scanType.ToUploadFilename());

        using var response = await _http.PostAsync(path, form, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"DefectDojo {path} failed with HTTP {(int)response.StatusCode} {response.ReasonPhrase}. Body: {body}");
        }

        var parsed = JsonSerializer.Deserialize(body, DefectDojoJsonContext.Default.ImportScanResponse);
        var testId = parsed?.TestId ?? parsed?.Test ?? 0;
        if (testId == 0)
            throw new InvalidOperationException($"DefectDojo {path} returned no test id. Body: {body}");

        return new DefectDojoScanResult
        {
            TestId = testId,
            FindingsCreated = parsed?.Statistics?.After?.Total?.Created,
            FindingsClosed = parsed?.Statistics?.After?.Total?.Closed,
        };
    }

    public void Dispose()
    {
        if (_ownsHttp) _http.Dispose();
    }
}
