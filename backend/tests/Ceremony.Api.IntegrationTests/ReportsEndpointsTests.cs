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
    public async Task GET_tablet_sample_returns_5dead5living_PDF_in_development()
    {
        var client = await AuthedAsync();

        var resp = await client.GetAsync("/api/v1/reports/tablet/sample?debugOverlay=true");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        resp.Content.Headers.ContentType?.MediaType.Should().Be("application/pdf");
        resp.Content.Headers.ContentDisposition?.FileName.Should().Be("tablet-sample-5dead-5living.pdf");

        var bytes = await resp.Content.ReadAsByteArrayAsync();
        bytes.Length.Should().BeGreaterThan(1000);
        bytes[0].Should().Be(0x25);  // %PDF magic
        bytes[1].Should().Be(0x50);
        bytes[2].Should().Be(0x44);
        bytes[3].Should().Be(0x46);
    }

    [Fact]
    public async Task GET_worship_nonType4_returns_pdf()
    {
        // 2026-07-18 解鎖：普桌不再限 SignupType=4（對齊舊系統選什麼印什麼），非普桌也回 200 PDF
        var client = await AuthedAsync();
        var listResp = await client.GetAsync("/api/v1/signups?year=115&signupType=1");
        var list = await listResp.Content.ReadFromJsonAsync<SignupListResponse>();
        var signupId = list!.Items[0].Id;

        var resp = await client.GetAsync($"/api/v1/reports/worship?signupId={signupId}");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        resp.Content.Headers.ContentType?.MediaType.Should().Be("application/pdf");
        var bytes = await resp.Content.ReadAsByteArrayAsync();
        bytes.Length.Should().BeGreaterThan(1000);
        bytes[0].Should().Be(0x25);  // %PDF magic
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

    [Fact]
    public async Task POST_batch_ids_returns_merged_PDF_for_exact_selection_regardless_of_gaps()
    {
        var client = await AuthedAsync();

        var listResp = await client.GetAsync("/api/v1/signups?year=115&signupType=1");
        var list = await listResp.Content.ReadFromJsonAsync<SignupListResponse>();
        list!.Items.Should().HaveCountGreaterThanOrEqualTo(2);

        var ordered = list.Items.OrderBy(i => i.Number).ToList();
        var picked = new[] { ordered[0].Id, ordered[^1].Id };

        var resp = await client.PostAsJsonAsync("/api/v1/reports/batch",
            new BatchReportRequest("datacard", SignupIds: picked));

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        resp.Content.Headers.ContentDisposition?.FileName.Should().Be("batch-datacard-selected-2.pdf");
        resp.Headers.GetValues("X-Signup-Count").Single().Should().Be("2");

        var bytes = await resp.Content.ReadAsByteArrayAsync();
        bytes.Length.Should().BeGreaterThan(1000);
        bytes[0].Should().Be(0x25);
    }

    [Fact]
    public async Task POST_batch_missing_ids_and_range_returns_400()
    {
        var client = await AuthedAsync();
        var resp = await client.PostAsJsonAsync("/api/v1/reports/batch", new BatchReportRequest("datacard"));
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await resp.Content.ReadAsStringAsync();
        body.Should().Contain("編號錯誤");
    }

    // ── 批次列印 job 版（有進度回報與取消）─────────────────────────────
    // Blueprint: docs/blueprints/api-endpoints/post-reports-batch-jobs.md

    [Fact]
    public async Task Batch_job_endpoints_without_token_return_401()
    {
        var anon = _factory.CreateClient();
        var id = Guid.NewGuid();

        (await anon.PostAsJsonAsync("/api/v1/reports/batch/jobs", new BatchReportRequest("datacard", 1, 10)))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await anon.GetAsync($"/api/v1/reports/batch/jobs/{id}"))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await anon.GetAsync($"/api/v1/reports/batch/jobs/{id}/file"))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await anon.DeleteAsync($"/api/v1/reports/batch/jobs/{id}"))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task POST_batch_job_invalid_range_returns_400_same_as_sync_version()
    {
        var client = await AuthedAsync();
        var resp = await client.PostAsJsonAsync("/api/v1/reports/batch/jobs",
            new BatchReportRequest("datacard", NumberStart: 50, NumberEnd: 10));
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await resp.Content.ReadAsStringAsync()).Should().Contain("編號錯誤");
    }

    [Fact]
    public async Task POST_batch_job_no_signups_returns_404_before_job_is_created()
    {
        var client = await AuthedAsync();
        var resp = await client.PostAsJsonAsync("/api/v1/reports/batch/jobs",
            new BatchReportRequest("datacard", 999_990, 999_999, Year: 100));
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await resp.Content.ReadAsStringAsync()).Should().Contain("BATCH_NO_SIGNUPS");
    }

    [Fact]
    public async Task Batch_job_happy_path_reports_progress_then_serves_pdf_once()
    {
        var client = await AuthedAsync();
        var picked = await PickTwoSignupIdsAsync(client);

        var createResp = await client.PostAsJsonAsync("/api/v1/reports/batch/jobs",
            new BatchReportRequest("datacard", SignupIds: picked));
        createResp.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var created = await createResp.Content.ReadFromJsonAsync<BatchPrintJobCreated>();
        created.Should().NotBeNull();
        created!.Total.Should().Be(2);
        created.FileName.Should().Be("batch-datacard-selected-2.pdf");

        var final = await PollUntilTerminalAsync(client, created.JobId);
        final.Status.Should().Be("completed");
        final.Completed.Should().Be(final.Total);

        var fileResp = await client.GetAsync($"/api/v1/reports/batch/jobs/{created.JobId}/file");
        fileResp.StatusCode.Should().Be(HttpStatusCode.OK);
        fileResp.Content.Headers.ContentType?.MediaType.Should().Be("application/pdf");
        fileResp.Content.Headers.ContentDisposition?.FileName.Should().Be("batch-datacard-selected-2.pdf");
        fileResp.Headers.GetValues("X-Signup-Count").Single().Should().Be("2");

        var bytes = await fileResp.Content.ReadAsByteArrayAsync();
        bytes.Length.Should().BeGreaterThan(1000);
        bytes[0].Should().Be(0x25);  // %PDF
        bytes[1].Should().Be(0x50);
        bytes[2].Should().Be(0x44);
        bytes[3].Should().Be(0x46);

        // one-shot：取過就釋放
        (await client.GetAsync($"/api/v1/reports/batch/jobs/{created.JobId}/file"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await client.GetAsync($"/api/v1/reports/batch/jobs/{created.JobId}"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GET_batch_job_unknown_id_returns_404()
    {
        var client = await AuthedAsync();
        var resp = await client.GetAsync($"/api/v1/reports/batch/jobs/{Guid.NewGuid()}");
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await resp.Content.ReadAsStringAsync()).Should().Contain("BATCH_JOB_NOT_FOUND");
    }

    [Fact]
    public async Task DELETE_batch_job_is_idempotent_for_unknown_id()
    {
        var client = await AuthedAsync();
        var resp = await client.DeleteAsync($"/api/v1/reports/batch/jobs/{Guid.NewGuid()}");
        resp.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DELETE_batch_job_stops_it_and_file_is_not_available()
    {
        var client = await AuthedAsync();
        var picked = await PickTwoSignupIdsAsync(client);

        var createResp = await client.PostAsJsonAsync("/api/v1/reports/batch/jobs",
            new BatchReportRequest("datacard", SignupIds: picked));
        var created = (await createResp.Content.ReadFromJsonAsync<BatchPrintJobCreated>())!;

        (await client.DeleteAsync($"/api/v1/reports/batch/jobs/{created.JobId}"))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        // 取消後：要嘛已進 canceled，要嘛剛好完成（競態）。無論哪種，/file 都不該回沒被要求的東西。
        var final = await PollUntilTerminalAsync(client, created.JobId);
        final.Status.Should().BeOneOf("canceled", "completed");

        if (final.Status == "canceled")
        {
            (await client.GetAsync($"/api/v1/reports/batch/jobs/{created.JobId}/file"))
                .StatusCode.Should().Be(HttpStatusCode.NotFound);
        }
    }

    private static async Task<Guid[]> PickTwoSignupIdsAsync(HttpClient client)
    {
        var listResp = await client.GetAsync("/api/v1/signups?year=115&signupType=1");
        var list = await listResp.Content.ReadFromJsonAsync<SignupListResponse>();
        list!.Items.Should().HaveCountGreaterThanOrEqualTo(2);
        var ordered = list.Items.OrderBy(i => i.Number).ToList();
        return [ordered[0].Id, ordered[^1].Id];
    }

    private static async Task<BatchPrintJobState> PollUntilTerminalAsync(HttpClient client, Guid jobId)
    {
        var deadline = DateTime.UtcNow.AddSeconds(60);
        while (DateTime.UtcNow < deadline)
        {
            var resp = await client.GetAsync($"/api/v1/reports/batch/jobs/{jobId}");
            resp.StatusCode.Should().Be(HttpStatusCode.OK);
            var state = (await resp.Content.ReadFromJsonAsync<BatchPrintJobState>())!;
            if (state.Status != "running") return state;
            await Task.Delay(50);
        }
        throw new TimeoutException("批次列印 job 未在時限內結束");
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
