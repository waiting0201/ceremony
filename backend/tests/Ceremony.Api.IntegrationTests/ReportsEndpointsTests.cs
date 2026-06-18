using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Ceremony.Application.Auth;
using Ceremony.Application.Reports;
using Ceremony.Application.Signups;
using FluentAssertions;

namespace Ceremony.Api.IntegrationTests;

public sealed class ReportsEndpointsTests(CeremonyApiFactory factory) : IClassFixture<CeremonyApiFactory>
{
    private readonly CeremonyApiFactory _factory = factory;

    private async Task<HttpClient> AuthedAsync()
    {
        var c = _factory.CreateClient();
        var resp = await c.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest("sa@system.local", "Admin@123"));
        var body = await resp.Content.ReadFromJsonAsync<LoginResponse>();
        var x = _factory.CreateClient();
        x.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body!.Token);
        return x;
    }

    [Fact]
    public async Task GET_datacard_without_token_returns_401()
    {
        var resp = await _factory.CreateClient().GetAsync($"/api/v1/reports/datacard?signupId={Guid.NewGuid()}");
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GET_datacard_unknownId_returns_404()
    {
        var client = await AuthedAsync();
        var resp = await client.GetAsync($"/api/v1/reports/datacard?signupId={Guid.NewGuid()}");
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var body = await resp.Content.ReadAsStringAsync();
        body.Should().Contain("SIGNUP_NOT_FOUND");
    }

    [Fact]
    public async Task GET_datacard_realSignup_returns_PDF_with_correct_headers()
        => await AssertReportEndpoint("datacard", signupType: 1, expectedPrefix: "datacard-");

    [Fact]
    public async Task GET_receipt_realSignup_returns_PDF()
        => await AssertReportEndpoint("receipt", signupType: 1, expectedPrefix: "receipt-");

    [Fact]
    public async Task GET_tablet_realSignup_returns_PDF()
        => await AssertReportEndpoint("tablet", signupType: 1, expectedPrefix: "tablet-");

    [Fact]
    public async Task GET_text_realSignup_returns_PDF()
        => await AssertReportEndpoint("text", signupType: 1, expectedPrefix: "text-");

    [Fact]
    public async Task GET_worship_nonType4_returns_422()
    {
        var client = await AuthedAsync();
        var listResp = await client.GetAsync("/api/v1/signups?year=115&signupType=1");
        var list = await listResp.Content.ReadFromJsonAsync<SignupListResponse>();
        var signupId = list!.Items[0].Id;

        var resp = await client.GetAsync($"/api/v1/reports/worship?signupId={signupId}");
        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var body = await resp.Content.ReadAsStringAsync();
        body.Should().Contain("WORSHIP_ONLY_TYPE_4");
    }

    [Fact]
    public async Task POST_batch_without_token_returns_401()
    {
        var resp = await _factory.CreateClient().PostAsJsonAsync("/api/v1/reports/batch",
            new BatchReportRequest("datacard", 1, 10));
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task POST_batch_invalid_range_returns_400()
    {
        var client = await AuthedAsync();
        var resp = await client.PostAsJsonAsync("/api/v1/reports/batch",
            new BatchReportRequest("datacard", NumberStart: 50, NumberEnd: 10));
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await resp.Content.ReadAsStringAsync();
        body.Should().Contain("編號錯誤");
    }

    [Fact]
    public async Task POST_batch_invalid_reportType_returns_400()
    {
        var client = await AuthedAsync();
        var resp = await client.PostAsJsonAsync("/api/v1/reports/batch",
            new BatchReportRequest("invoice", 1, 10));
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await resp.Content.ReadAsStringAsync();
        body.Should().Contain("報表類型錯誤");
    }

    [Fact]
    public async Task POST_batch_no_signups_returns_404()
    {
        var client = await AuthedAsync();
        // 用 100 年 + 不存在範圍盡量保證 0 命中
        var resp = await client.PostAsJsonAsync("/api/v1/reports/batch",
            new BatchReportRequest("datacard", 999_990, 999_999, Year: 100));
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var body = await resp.Content.ReadAsStringAsync();
        body.Should().Contain("BATCH_NO_SIGNUPS");
    }

    [Fact]
    public async Task POST_batch_datacard_returns_merged_PDF_with_count_header()
    {
        var client = await AuthedAsync();

        // 找一個年份 + signupType=1 的範圍，預期至少 1 筆
        var listResp = await client.GetAsync("/api/v1/signups?year=115&signupType=1");
        var list = await listResp.Content.ReadFromJsonAsync<SignupListResponse>();
        list!.Items.Should().NotBeEmpty();

        var minNumber = list.Items.Min(i => i.Number ?? int.MaxValue);
        var maxNumber = list.Items.Max(i => i.Number ?? 0);

        var resp = await client.PostAsJsonAsync("/api/v1/reports/batch",
            new BatchReportRequest("datacard", minNumber, maxNumber, Year: 115, SignupType: 1));

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        resp.Content.Headers.ContentType?.MediaType.Should().Be("application/pdf");
        resp.Content.Headers.ContentDisposition?.FileName.Should().StartWith("batch-datacard-");
        resp.Headers.GetValues("X-Signup-Count").Single().Should().NotBeNullOrEmpty();

        var bytes = await resp.Content.ReadAsByteArrayAsync();
        bytes.Length.Should().BeGreaterThan(1000);
        bytes[0].Should().Be(0x25);  // %PDF
        bytes[1].Should().Be(0x50);
        bytes[2].Should().Be(0x44);
        bytes[3].Should().Be(0x46);
    }

    private async Task AssertReportEndpoint(string endpoint, int signupType, string expectedPrefix)
    {
        var client = await AuthedAsync();

        var listResp = await client.GetAsync($"/api/v1/signups?year=115&signupType={signupType}");
        var list = await listResp.Content.ReadFromJsonAsync<SignupListResponse>();
        list.Should().NotBeNull();
        list!.Items.Should().NotBeEmpty();
        var signupId = list.Items[0].Id;

        var resp = await client.GetAsync($"/api/v1/reports/{endpoint}?signupId={signupId}");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        resp.Content.Headers.ContentType?.MediaType.Should().Be("application/pdf");
        resp.Content.Headers.ContentDisposition?.FileName.Should().StartWith(expectedPrefix);

        var bytes = await resp.Content.ReadAsByteArrayAsync();
        bytes.Length.Should().BeGreaterThan(1000);
        bytes[0].Should().Be(0x25);  // %PDF magic
        bytes[1].Should().Be(0x50);
        bytes[2].Should().Be(0x44);
        bytes[3].Should().Be(0x46);
    }
}
