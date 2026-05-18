using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Tamp.Sbom;

namespace Tamp.DependencyTrack.V1;

/// <summary>
/// REST client for OWASP Dependency-Track v4.x (API v1). Three operations
/// cover the full Wave 1 chain:
/// <list type="number">
///   <item><see cref="UploadBomAsync"/> — push a CycloneDX BOM, returns a processing token.</item>
///   <item><see cref="WaitForAnalysisCompleteAsync"/> — poll the token until DT finishes async vulnerability matching.</item>
///   <item><see cref="ExportFindingsAsync"/> — pull the resulting findings in raw FPF JSON for passthrough to DefectDojo.</item>
/// </list>
/// The FPF JSON is intentionally not deserialised — Wave 1's locked
/// decision is "no SARIF normalisation in the middle" so DT's VEX/
/// suppression rationale survives to DefectDojo unchanged.
/// </summary>
public sealed class DependencyTrackClient : IDisposable
{
    private readonly HttpClient _http;
    private readonly bool _ownsHttp;

    /// <summary>
    /// Production constructor. Creates an internal <see cref="HttpClient"/>
    /// pre-configured with <c>X-Api-Key</c> and the base address from
    /// <paramref name="settings"/>.
    /// </summary>
    public DependencyTrackClient(DependencyTrackSettings settings)
        : this(settings, http: null, ownsHttp: true)
    {
    }

    /// <summary>
    /// Test seam: supply your own <see cref="HttpClient"/> (usually backed
    /// by a recording handler). The client takes ownership of disposal
    /// only when it constructed the instance itself.
    /// </summary>
    internal DependencyTrackClient(DependencyTrackSettings settings, HttpClient? http, bool ownsHttp = false)
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

        _http.DefaultRequestHeaders.Remove("X-Api-Key");
        _http.DefaultRequestHeaders.Add("X-Api-Key", settings.RevealApiKey());
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    /// <summary>
    /// PUT the BOM to <c>/api/v1/bom</c>. The request body is JSON:
    /// <c>{ "project": "&lt;uuid&gt;", "bom": "&lt;base64-cdx-bytes&gt;" }</c>.
    /// </summary>
    public async Task<DependencyTrackUploadResult> UploadBomAsync(
        Guid projectUuid,
        CycloneDxBom bom,
        CancellationToken cancellationToken = default)
    {
        if (bom is null) throw new ArgumentNullException(nameof(bom));

        var bomJson = SbomWriter.Serialize(bom);
        var bomBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(bomJson));

        var requestBody = new BomUploadRequest { Project = projectUuid.ToString("D"), Bom = bomBase64 };
        var requestJson = JsonSerializer.Serialize(requestBody, DependencyTrackJsonContext.Default.BomUploadRequest);

        using var content = new StringContent(requestJson, Encoding.UTF8, "application/json");
        using var response = await _http.PutAsync("api/v1/bom", content, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, "BOM upload", cancellationToken).ConfigureAwait(false);

        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var parsed = JsonSerializer.Deserialize(body, DependencyTrackJsonContext.Default.BomUploadResponse);
        if (parsed is null || string.IsNullOrEmpty(parsed.Token))
            throw new InvalidOperationException($"Dependency-Track BOM upload returned no token. Body: {body}");

        return new DependencyTrackUploadResult { Token = parsed.Token };
    }

    /// <summary>
    /// GET <c>/api/v1/bom/token/{token}</c>. Returns true when DT's
    /// asynchronous analysis is finished and findings are stable.
    /// </summary>
    public async Task<bool> IsAnalysisCompleteAsync(string token, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token)) throw new ArgumentException("Token must be non-empty.", nameof(token));

        using var response = await _http.GetAsync($"api/v1/bom/token/{token}", cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, "analysis-status poll", cancellationToken).ConfigureAwait(false);

        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var parsed = JsonSerializer.Deserialize(body, DependencyTrackJsonContext.Default.BomTokenResponse);
        return parsed is { Processing: false };
    }

    /// <summary>
    /// Poll <see cref="IsAnalysisCompleteAsync"/> via
    /// <see cref="Polling.Until"/> until DT finishes analysing the BOM or
    /// the budget elapses.
    /// </summary>
    public Task<bool> WaitForAnalysisCompleteAsync(
        string token,
        TimeSpan timeout,
        Backoff? backoff = null,
        Logger? logger = null,
        CancellationToken cancellationToken = default)
    {
        return Polling.Until(
            condition: ct => IsAnalysisCompleteAsync(token, ct),
            timeout: timeout,
            backoff: backoff,
            logger: logger,
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// GET <c>/api/v1/finding/project/{uuid}/export</c>. Returns the
    /// findings as the raw Dependency-Track Finding Packaging Format JSON
    /// string — exactly what DefectDojo's <c>reimport-scan</c> endpoint
    /// expects under <c>scan_type="Dependency Track Finding Packaging
    /// Format (FPF) Export"</c>. Not deserialised on purpose
    /// (Wave 1 locked decision: no SARIF normalisation in the middle).
    /// </summary>
    public async Task<string> ExportFindingsAsync(Guid projectUuid, CancellationToken cancellationToken = default)
    {
        using var response = await _http.GetAsync(
            $"api/v1/finding/project/{projectUuid:D}/export",
            cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, "findings export", cancellationToken).ConfigureAwait(false);
        return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, string operation, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode) return;
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        throw new HttpRequestException(
            $"Dependency-Track {operation} failed with HTTP {(int)response.StatusCode} {response.ReasonPhrase}. Body: {body}");
    }

    public void Dispose()
    {
        if (_ownsHttp) _http.Dispose();
    }
}
