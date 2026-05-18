using System.Net;
using System.Net.Http;
using System.Text;

namespace Tamp.DefectDojo.V2.Tests;

/// <summary>
/// Test double for <see cref="HttpClient"/>: scripts responses by
/// (method, path) match and records every request so the test can assert
/// what the client sent. Copy of the same shape in Tamp.DependencyTrack.V1.Tests
/// — kept local rather than extracted to a shared test-util package since
/// these two are the only consumers today (per "no premature abstraction").
/// </summary>
internal sealed class RecordingHandler : HttpMessageHandler
{
    private readonly List<(Func<HttpRequestMessage, bool> Match, Func<HttpRequestMessage, HttpResponseMessage> Build)> _routes = new();
    private readonly List<RecordedRequest> _recorded = new();

    public IReadOnlyList<RecordedRequest> Recorded => _recorded;

    public RecordingHandler When(HttpMethod method, string pathContains, HttpStatusCode status, string body, string contentType = "application/json")
    {
        _routes.Add((
            req => req.Method == method && (req.RequestUri?.AbsolutePath.Contains(pathContains, StringComparison.Ordinal) ?? false),
            _ => new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, contentType) }));
        return this;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var bodyBytes = request.Content is null ? null : await request.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        var bodyText = bodyBytes is null ? null : Encoding.UTF8.GetString(bodyBytes);

        var auth = request.Headers.Authorization is { } a ? $"{a.Scheme} {a.Parameter}" : null;

        _recorded.Add(new RecordedRequest(
            request.Method,
            request.RequestUri!,
            auth,
            request.Content?.Headers.ContentType?.MediaType,
            bodyText));

        foreach (var (match, build) in _routes)
        {
            if (match(request)) return build(request);
        }

        return new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent($"No route registered for {request.Method} {request.RequestUri}"),
        };
    }
}

internal sealed record RecordedRequest(HttpMethod Method, Uri Uri, string? Authorization, string? ContentType, string? BodyText);
