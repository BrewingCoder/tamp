using System.Net;
using System.Net.Http;
using System.Text;

namespace Tamp.DependencyTrack.V1.Tests;

/// <summary>
/// Test double for <see cref="HttpClient"/>: scripts responses by
/// (method, path) match and records every request so the test can assert
/// what the client sent. No external dependencies — keeps the test
/// surface tight and federal-air-gap-friendly.
/// </summary>
internal sealed class RecordingHandler : HttpMessageHandler
{
    private readonly List<(Func<HttpRequestMessage, bool> Match, Func<HttpRequestMessage, HttpResponseMessage> Build)> _routes = new();
    private readonly List<RecordedRequest> _recorded = new();

    public IReadOnlyList<RecordedRequest> Recorded => _recorded;

    public RecordingHandler When(HttpMethod method, string pathContains, HttpStatusCode status, string body, string? contentType = "application/json")
    {
        _routes.Add((
            req => req.Method == method && (req.RequestUri?.AbsolutePath.Contains(pathContains, StringComparison.Ordinal) ?? false),
            req => new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, contentType ?? "application/json") }));
        return this;
    }

    public RecordingHandler WhenSequence(HttpMethod method, string pathContains, params (HttpStatusCode Status, string Body)[] responses)
    {
        var index = 0;
        _routes.Add((
            req => req.Method == method && (req.RequestUri?.AbsolutePath.Contains(pathContains, StringComparison.Ordinal) ?? false),
            req =>
            {
                var (status, body) = responses[Math.Min(index, responses.Length - 1)];
                index++;
                return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
            }));
        return this;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var bodyText = request.Content is null
            ? null
            : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        _recorded.Add(new RecordedRequest(
            request.Method,
            request.RequestUri!,
            request.Headers.TryGetValues("X-Api-Key", out var apiKeys) ? apiKeys.FirstOrDefault() : null,
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

internal sealed record RecordedRequest(HttpMethod Method, Uri Uri, string? ApiKey, string? BodyText);
