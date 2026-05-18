using System.Net;
using Tamp.Sarif;
using Xunit;

namespace Tamp.DefectDojo.V2.Tests;

public class DefectDojoClientTests
{
    private static DefectDojoSettings DefaultSettings() => new()
    {
        BaseUrl = new Uri("https://dd.example.test"),
        Token = new Secret("dd-test-token", "dd-token-value-abcdef"),
    };

    private static (DefectDojoClient Client, RecordingHandler Handler) BuildClient(Action<RecordingHandler> routes)
    {
        var handler = new RecordingHandler();
        routes(handler);
        var http = new HttpClient(handler);
        var client = new DefectDojoClient(DefaultSettings(), http, ownsHttp: true);
        return (client, handler);
    }

    [Fact]
    public async Task ImportScan_Sends_Post_To_Import_Scan_Endpoint()
    {
        var (client, handler) = BuildClient(h => h.When(
            HttpMethod.Post, "/api/v2/import-scan/", HttpStatusCode.Created,
            """{ "test_id": 42, "statistics": { "after": { "total": { "created": 7, "closed": 0 } } } }"""));

        using var _ = client;
        var result = await client.ImportScanAsync(DefectDojoScanType.Sarif, engagementId: 99, scanPayload: """{ "version": "2.1.0", "runs": [] }""");

        Assert.Equal(42, result.TestId);
        Assert.Equal(7, result.FindingsCreated);
        Assert.Equal(0, result.FindingsClosed);

        var recorded = Assert.Single(handler.Recorded);
        Assert.Equal(HttpMethod.Post, recorded.Method);
        Assert.EndsWith("/api/v2/import-scan/", recorded.Uri.AbsolutePath);
        Assert.StartsWith("multipart/form-data", recorded.ContentType);
    }

    [Fact]
    public async Task ReimportScan_Sends_Post_To_Reimport_Scan_Endpoint()
    {
        var (client, handler) = BuildClient(h => h.When(
            HttpMethod.Post, "/api/v2/reimport-scan/", HttpStatusCode.Created, """{ "test_id": 100 }"""));

        using var _ = client;
        var result = await client.ReimportScanAsync(DefectDojoScanType.Sarif, engagementId: 12, scanPayload: """{ "version": "2.1.0", "runs": [] }""");

        Assert.Equal(100, result.TestId);
        Assert.EndsWith("/api/v2/reimport-scan/", handler.Recorded[0].Uri.AbsolutePath);
    }

    [Fact]
    public async Task Authorization_Header_Uses_Token_Scheme_Not_Bearer()
    {
        var (client, handler) = BuildClient(h => h.When(
            HttpMethod.Post, "/api/v2/import-scan/", HttpStatusCode.Created, """{ "test_id": 1 }"""));

        using var _ = client;
        await client.ImportScanAsync(DefectDojoScanType.Sarif, 1, """{ "version": "2.1.0", "runs": [] }""");

        Assert.Equal("Token dd-token-value-abcdef", handler.Recorded[0].Authorization);
    }

    [Theory]
    [InlineData(DefectDojoScanType.Sarif, "SARIF")]
    [InlineData(DefectDojoScanType.DependencyTrackFpf, "Dependency Track Finding Packaging Format (FPF) Export")]
    public async Task Scan_Type_Wire_Value_Matches_DefectDojo_Convention(DefectDojoScanType scanType, string expectedWireValue)
    {
        var (client, handler) = BuildClient(h => h.When(
            HttpMethod.Post, "/api/v2/import-scan/", HttpStatusCode.Created, """{ "test_id": 1 }"""));

        using var _ = client;
        await client.ImportScanAsync(scanType, 1, """{ "anything": true }""");

        AssertHasFormField(handler.Recorded[0].BodyText!, "scan_type");
        Assert.Contains(expectedWireValue, handler.Recorded[0].BodyText);
    }

    private static void AssertHasFormField(string body, string fieldName)
    {
        // .NET's MultipartFormDataContent may or may not quote simple field names
        // depending on runtime version. Accept both forms.
        var quoted = $"name=\"{fieldName}\"";
        var unquoted = $"name={fieldName}";
        Assert.True(
            body.Contains(quoted, StringComparison.Ordinal) || body.Contains(unquoted, StringComparison.Ordinal),
            $"Multipart form field '{fieldName}' not found in body (looked for {quoted} or {unquoted}).\nBody:\n{body}");
    }

    [Fact]
    public async Task Multipart_Body_Contains_All_Required_Form_Fields()
    {
        var (client, handler) = BuildClient(h => h.When(
            HttpMethod.Post, "/api/v2/import-scan/", HttpStatusCode.Created, """{ "test_id": 1 }"""));

        using var _ = client;
        await client.ImportScanAsync(DefectDojoScanType.Sarif, engagementId: 314, scanPayload: """{ "version": "2.1.0", "runs": [] }""");

        var body = handler.Recorded[0].BodyText!;
        AssertHasFormField(body, "scan_type");
        AssertHasFormField(body, "engagement");
        Assert.Contains("314", body);
        AssertHasFormField(body, "active");
        AssertHasFormField(body, "verified");
        AssertHasFormField(body, "close_old_findings");
        AssertHasFormField(body, "file");
        Assert.Contains("scan.sarif", body);
    }

    [Fact]
    public async Task Options_Override_Default_Form_Values_When_Provided()
    {
        var (client, handler) = BuildClient(h => h.When(
            HttpMethod.Post, "/api/v2/import-scan/", HttpStatusCode.Created, """{ "test_id": 1 }"""));

        using var _ = client;
        var options = new DefectDojoImportOptions
        {
            Active = false,
            Verified = true,
            CloseOldFindings = false,
            ScanDate = new DateOnly(2026, 5, 17),
            BuildId = "ci-run-12345",
        };

        await client.ImportScanAsync(DefectDojoScanType.Sarif, 1, """{ "anything": true }""", options);

        var body = handler.Recorded[0].BodyText!;
        Assert.Contains("2026-05-17", body);
        Assert.Contains("ci-run-12345", body);
        // Per-field value placement: each form part's value follows the empty line after its Content-Disposition.
        // Match either quoted or unquoted name= form, and accept any whitespace between header end and value.
        Assert.Matches(@"name=""?active""?\s*\r?\n\s*\r?\n\s*false", body);
        Assert.Matches(@"name=""?verified""?\s*\r?\n\s*\r?\n\s*true", body);
        Assert.Matches(@"name=""?close_old_findings""?\s*\r?\n\s*\r?\n\s*false", body);
    }

    [Fact]
    public async Task Empty_Payload_Throws()
    {
        var (client, _) = BuildClient(_ => { });
        using var _2 = client;
        await Assert.ThrowsAsync<ArgumentException>(() => client.ImportScanAsync(DefectDojoScanType.Sarif, 1, ""));
    }

    [Fact]
    public async Task Http_Error_Throws_With_Body_Detail()
    {
        var (client, _) = BuildClient(h => h.When(
            HttpMethod.Post, "/api/v2/import-scan/", HttpStatusCode.BadRequest,
            """{ "scan_type": ["This field is required."] }"""));

        using var _2 = client;
        var ex = await Assert.ThrowsAsync<HttpRequestException>(() =>
            client.ImportScanAsync(DefectDojoScanType.Sarif, 1, """{ "version": "2.1.0", "runs": [] }"""));

        Assert.Contains("400", ex.Message);
        Assert.Contains("scan_type", ex.Message);
    }

    [Fact]
    public async Task Missing_Test_Id_In_Response_Throws()
    {
        var (client, _) = BuildClient(h => h.When(
            HttpMethod.Post, "/api/v2/import-scan/", HttpStatusCode.Created, """{ "statistics": {} }"""));

        using var _ = client;
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.ImportScanAsync(DefectDojoScanType.Sarif, 1, """{ "version": "2.1.0", "runs": [] }"""));
    }

    [Fact]
    public async Task Falls_Back_To_Test_Field_When_TestId_Not_Returned()
    {
        // DD versions vary: some return "test", some "test_id". Accept either.
        var (client, _) = BuildClient(h => h.When(
            HttpMethod.Post, "/api/v2/import-scan/", HttpStatusCode.Created, """{ "test": 77 }"""));

        using var _ = client;
        var result = await client.ImportScanAsync(DefectDojoScanType.Sarif, 1, """{ "version": "2.1.0", "runs": [] }""");

        Assert.Equal(77, result.TestId);
    }

    [Fact]
    public async Task ImportSarifAsync_Serialises_Log_And_Uses_Sarif_Scan_Type()
    {
        var (client, handler) = BuildClient(h => h.When(
            HttpMethod.Post, "/api/v2/import-scan/", HttpStatusCode.Created, """{ "test_id": 1 }"""));

        using var _ = client;
        var log = new SarifLog
        {
            Runs = [new SarifRun
            {
                Tool = new SarifTool { Driver = new SarifToolComponent { Name = "opengrep" } },
                Results = [new SarifResult { RuleId = "r1", Level = SarifLevel.Error, Message = new SarifMessage { Text = "issue" } }],
            }],
        };

        await client.ImportSarifAsync(engagementId: 50, log);

        var body = handler.Recorded[0].BodyText!;
        Assert.Contains("SARIF", body);
        Assert.Contains("opengrep", body);
        Assert.Contains("scan.sarif", body);
    }

    [Fact]
    public async Task ReimportSarifAsync_Hits_Reimport_Endpoint_With_Sarif_Body()
    {
        var (client, handler) = BuildClient(h => h.When(
            HttpMethod.Post, "/api/v2/reimport-scan/", HttpStatusCode.Created, """{ "test_id": 7 }"""));

        using var _ = client;
        var log = new SarifLog
        {
            Runs = [new SarifRun { Tool = new SarifTool { Driver = new SarifToolComponent { Name = "trivy" } } }],
        };

        var result = await client.ReimportSarifAsync(engagementId: 50, log);

        Assert.Equal(7, result.TestId);
        Assert.EndsWith("/api/v2/reimport-scan/", handler.Recorded[0].Uri.AbsolutePath);
        Assert.Contains("trivy", handler.Recorded[0].BodyText);
    }

    [Fact]
    public async Task Fpf_Passthrough_Preserves_Raw_Body_Verbatim()
    {
        // The whole point of the FPF passthrough: whatever DT exports goes to DD
        // unmodified so VEX-suppression rationale survives. Verify we don't
        // re-serialise / re-format the bytes.
        const string rawFpf = """{"version":"1.2","findings":[{"vulnerability":{"id":"CVE-2024-21907"},"analysis":{"state":"not_affected","detail":"verbatim text from DT"}}]}""";

        var (client, handler) = BuildClient(h => h.When(
            HttpMethod.Post, "/api/v2/reimport-scan/", HttpStatusCode.Created, """{ "test_id": 1 }"""));

        using var _ = client;
        await client.ReimportScanAsync(DefectDojoScanType.DependencyTrackFpf, 1, rawFpf);

        var body = handler.Recorded[0].BodyText!;
        Assert.Contains(rawFpf, body);
        Assert.Contains("findings.fpf.json", body);
        Assert.Contains("Dependency Track Finding Packaging Format (FPF) Export", body);
    }

    [Fact]
    public void Settings_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new DefectDojoClient(null!));
    }
}
