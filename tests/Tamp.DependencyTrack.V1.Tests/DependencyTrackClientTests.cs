using System.Net;
using System.Text;
using System.Text.Json;
using Tamp.Sbom;
using Xunit;

namespace Tamp.DependencyTrack.V1.Tests;

public class DependencyTrackClientTests
{
    private static DependencyTrackSettings DefaultSettings() => new()
    {
        BaseUrl = new Uri("https://dt.example.test"),
        ApiKey = new Secret("dt-test-key", "test-api-key-value-12345"),
    };

    private static (DependencyTrackClient Client, RecordingHandler Handler) BuildClient(Action<RecordingHandler> routes)
    {
        var handler = new RecordingHandler();
        routes(handler);
        var http = new HttpClient(handler);
        var client = new DependencyTrackClient(DefaultSettings(), http, ownsHttp: true);
        return (client, handler);
    }

    [Fact]
    public async Task UploadBomAsync_Sends_Put_With_Project_And_Base64_Bom_And_Returns_Token()
    {
        var (client, handler) = BuildClient(h => h.When(
            HttpMethod.Put, "/api/v1/bom", HttpStatusCode.OK,
            """{ "token": "fc6c1f73-86cb-4d27-bb2c-1d3a1a9b3c5a" }"""));

        using var _ = client;
        var bom = new CycloneDxBom
        {
            Components = [new CycloneDxComponent { Name = "Newtonsoft.Json", Version = "13.0.3", Type = "library" }],
        };

        var projectUuid = new Guid("d2b9b234-3a92-4c3a-9d51-a4f30e8a6db0");
        var result = await client.UploadBomAsync(projectUuid, bom);

        Assert.Equal("fc6c1f73-86cb-4d27-bb2c-1d3a1a9b3c5a", result.Token);

        var recorded = Assert.Single(handler.Recorded);
        Assert.Equal(HttpMethod.Put, recorded.Method);
        Assert.EndsWith("/api/v1/bom", recorded.Uri.AbsolutePath);
        Assert.Equal("test-api-key-value-12345", recorded.ApiKey);

        // Body is JSON with the project UUID and a base64-encoded CycloneDX BOM.
        using var doc = JsonDocument.Parse(recorded.BodyText!);
        Assert.Equal("d2b9b234-3a92-4c3a-9d51-a4f30e8a6db0", doc.RootElement.GetProperty("project").GetString());
        var bomB64 = doc.RootElement.GetProperty("bom").GetString();
        var bomJson = Encoding.UTF8.GetString(Convert.FromBase64String(bomB64!));
        var roundtripped = SbomReader.Parse(bomJson);
        Assert.Equal("Newtonsoft.Json", roundtripped.Components![0].Name);
    }

    [Fact]
    public async Task UploadBomAsync_Throws_On_Http_Error()
    {
        var (client, _) = BuildClient(h => h.When(
            HttpMethod.Put, "/api/v1/bom", HttpStatusCode.Unauthorized, """{ "error": "bad key" }"""));

        using var _ = client;
        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => client.UploadBomAsync(Guid.NewGuid(), new CycloneDxBom()));
        Assert.Contains("401", ex.Message);
    }

    [Fact]
    public async Task UploadBomAsync_Throws_When_Response_Has_No_Token()
    {
        var (client, _) = BuildClient(h => h.When(
            HttpMethod.Put, "/api/v1/bom", HttpStatusCode.OK, """{ "token": "" }"""));

        using var _ = client;
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.UploadBomAsync(Guid.NewGuid(), new CycloneDxBom()));
    }

    [Fact]
    public async Task UploadBomAsync_Throws_ArgumentNull_For_Null_Bom()
    {
        var (client, _) = BuildClient(_ => { });
        using var _2 = client;
        await Assert.ThrowsAsync<ArgumentNullException>(() => client.UploadBomAsync(Guid.NewGuid(), null!));
    }

    [Fact]
    public async Task IsAnalysisCompleteAsync_Returns_True_When_Processing_False()
    {
        var (client, _) = BuildClient(h => h.When(
            HttpMethod.Get, "/api/v1/bom/token/", HttpStatusCode.OK, """{ "processing": false }"""));

        using var _ = client;
        Assert.True(await client.IsAnalysisCompleteAsync("some-token"));
    }

    [Fact]
    public async Task IsAnalysisCompleteAsync_Returns_False_When_Processing_True()
    {
        var (client, _) = BuildClient(h => h.When(
            HttpMethod.Get, "/api/v1/bom/token/", HttpStatusCode.OK, """{ "processing": true }"""));

        using var _ = client;
        Assert.False(await client.IsAnalysisCompleteAsync("some-token"));
    }

    [Fact]
    public async Task IsAnalysisCompleteAsync_Throws_On_Empty_Token()
    {
        var (client, _) = BuildClient(_ => { });
        using var _2 = client;
        await Assert.ThrowsAsync<ArgumentException>(() => client.IsAnalysisCompleteAsync(""));
    }

    [Fact]
    public async Task WaitForAnalysisCompleteAsync_Polls_Until_Processing_False()
    {
        var (client, handler) = BuildClient(h => h.WhenSequence(
            HttpMethod.Get, "/api/v1/bom/token/",
            (HttpStatusCode.OK, """{ "processing": true }"""),
            (HttpStatusCode.OK, """{ "processing": true }"""),
            (HttpStatusCode.OK, """{ "processing": false }""")));

        using var _ = client;
        var result = await client.WaitForAnalysisCompleteAsync(
            "tkn",
            timeout: TimeSpan.FromSeconds(5),
            backoff: Backoff.Constant(TimeSpan.FromMilliseconds(20)));

        Assert.True(result);
        Assert.Equal(3, handler.Recorded.Count);
        Assert.All(handler.Recorded, r => Assert.EndsWith("/api/v1/bom/token/tkn", r.Uri.AbsolutePath));
    }

    [Fact]
    public async Task WaitForAnalysisCompleteAsync_Returns_False_On_Timeout()
    {
        var (client, _) = BuildClient(h => h.When(
            HttpMethod.Get, "/api/v1/bom/token/", HttpStatusCode.OK, """{ "processing": true }"""));

        using var _ = client;
        var result = await client.WaitForAnalysisCompleteAsync(
            "tkn",
            timeout: TimeSpan.FromMilliseconds(100),
            backoff: Backoff.Constant(TimeSpan.FromMilliseconds(20)));

        Assert.False(result);
    }

    [Fact]
    public async Task ExportFindingsAsync_Returns_Raw_Fpf_Body()
    {
        const string rawFpf = """
        {
          "version": "1.2",
          "meta": { "application": "Dependency-Track", "version": "4.11.0" },
          "project": { "uuid": "abc", "name": "demo" },
          "findings": [{ "component": { "name": "log4j" }, "vulnerability": { "id": "CVE-2021-44228" } }]
        }
        """;

        var (client, handler) = BuildClient(h => h.When(
            HttpMethod.Get, "/api/v1/finding/project/", HttpStatusCode.OK, rawFpf));

        using var _ = client;
        var uuid = new Guid("11111111-2222-3333-4444-555555555555");
        var body = await client.ExportFindingsAsync(uuid);

        Assert.Equal(rawFpf, body);
        var recorded = Assert.Single(handler.Recorded);
        Assert.EndsWith($"/api/v1/finding/project/{uuid:D}/export", recorded.Uri.AbsolutePath);
    }

    [Fact]
    public async Task ExportFindingsAsync_Throws_On_Http_Error()
    {
        var (client, _) = BuildClient(h => h.When(
            HttpMethod.Get, "/api/v1/finding/project/", HttpStatusCode.NotFound, """{ "error": "project not found" }"""));

        using var _ = client;
        await Assert.ThrowsAsync<HttpRequestException>(() => client.ExportFindingsAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task X_Api_Key_Header_Is_Sent_On_Every_Request()
    {
        var (client, handler) = BuildClient(h => h
            .When(HttpMethod.Put, "/api/v1/bom", HttpStatusCode.OK, """{ "token": "t" }""")
            .When(HttpMethod.Get, "/api/v1/bom/token/", HttpStatusCode.OK, """{ "processing": false }""")
            .When(HttpMethod.Get, "/api/v1/finding/project/", HttpStatusCode.OK, "[]"));

        using var _ = client;
        await client.UploadBomAsync(Guid.NewGuid(), new CycloneDxBom());
        await client.IsAnalysisCompleteAsync("t");
        await client.ExportFindingsAsync(Guid.NewGuid());

        Assert.Equal(3, handler.Recorded.Count);
        Assert.All(handler.Recorded, r => Assert.Equal("test-api-key-value-12345", r.ApiKey));
    }

    [Fact]
    public void Settings_Requires_BaseUrl_And_ApiKey()
    {
        Assert.Throws<ArgumentNullException>(() => new DependencyTrackClient(null!));
    }

    [Fact]
    public async Task End_To_End_Chain_Produces_Findings_For_Downstream_Passthrough()
    {
        // Full chain in one test: upload BOM, poll analysis, export raw FPF.
        // This is the canonical Wave 1 use case — gate any future refactor on it staying green.
        var (client, handler) = BuildClient(h => h
            .When(HttpMethod.Put, "/api/v1/bom", HttpStatusCode.OK, """{ "token": "wave1-token" }""")
            .WhenSequence(HttpMethod.Get, "/api/v1/bom/token/",
                (HttpStatusCode.OK, """{ "processing": true }"""),
                (HttpStatusCode.OK, """{ "processing": false }"""))
            .When(HttpMethod.Get, "/api/v1/finding/project/", HttpStatusCode.OK, """{ "findings": [{ "vulnerability": { "id": "CVE-2024-21907" } }] }"""));

        using var _ = client;
        var projectUuid = Guid.NewGuid();
        var upload = await client.UploadBomAsync(projectUuid, new CycloneDxBom());
        Assert.Equal("wave1-token", upload.Token);

        var analyzed = await client.WaitForAnalysisCompleteAsync(
            upload.Token,
            timeout: TimeSpan.FromSeconds(5),
            backoff: Backoff.Constant(TimeSpan.FromMilliseconds(20)));
        Assert.True(analyzed);

        var fpf = await client.ExportFindingsAsync(projectUuid);
        Assert.Contains("CVE-2024-21907", fpf);
    }
}
