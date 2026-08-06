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

    /// <summary>
    /// 薦牌現場對位校正版：同一筆資料 + 1cm 刻度格線，檔名帶 -calibration 與正式版分得開。
    /// </summary>
    /// <remarks>
    /// 2026-08-06 客訴「四位往生者壓到預印的靈位」的量測工具。⚠️ 與 debugOverlay 不同，
    /// **debugGrid 不做 Development 阻擋**——它要在現場的 Windows 機器上印在實體薦牌紙上。
    /// 這裡只驗得到「Development 下可用」（工廠固定 Development，見 CeremonyApiFactory）；
    /// 「Production 也可用」靠 ReportsController 只對 debugOverlay 設 env 判斷這個結構保證。
    /// </remarks>
    [Fact]
    public async Task GET_tablet_debugGrid_returns_calibration_PDF()
    {
        var client = await AuthedAsync();
        var signupId = await FirstSignupIdAsync(client, signupType: 1);

        var plain = await client.GetAsync($"/api/v1/reports/tablet?signupId={signupId}");
        var grid = await client.GetAsync($"/api/v1/reports/tablet?signupId={signupId}&debugGrid=true");

        grid.StatusCode.Should().Be(HttpStatusCode.OK);
        grid.Content.Headers.ContentType?.MediaType.Should().Be("application/pdf");
        grid.Content.Headers.ContentDisposition?.FileName.Should().EndWith("-calibration.pdf");

        // 格線是 12 條垂直 + 26 條水平線再加刻度數字 → 一定比正式版大；
        // 相等就代表 debugGrid 沒接到 renderer（純粹多一個被忽略的 query 參數）。
        var gridBytes = await grid.Content.ReadAsByteArrayAsync();
        var plainBytes = await plain.Content.ReadAsByteArrayAsync();
        gridBytes.Length.Should().BeGreaterThan(plainBytes.Length);
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
    public async Task POST_batch_job_invalid_range_returns_400()
    {
        var client = await AuthedAsync();
        var resp = await client.PostAsJsonAsync("/api/v1/reports/batch/jobs",
            new BatchReportRequest("datacard", NumberStart: 50, NumberEnd: 10));
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await resp.Content.ReadAsStringAsync()).Should().Contain("編號錯誤");
    }

    [Fact]
    public async Task POST_batch_job_invalid_reportType_returns_400()
    {
        var client = await AuthedAsync();
        var resp = await client.PostAsJsonAsync("/api/v1/reports/batch/jobs",
            new BatchReportRequest("invoice", 1, 10));
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await resp.Content.ReadAsStringAsync()).Should().Contain("報表類型錯誤");
    }

    [Fact]
    public async Task POST_batch_job_missing_ids_and_range_returns_400()
    {
        var client = await AuthedAsync();
        var resp = await client.PostAsJsonAsync("/api/v1/reports/batch/jobs",
            new BatchReportRequest("datacard"));
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await resp.Content.ReadAsStringAsync()).Should().Contain("編號錯誤");
    }

    /// <summary>編號區間選取（非勾選 id）走到底：確認 SearchByNumberRangeAsync 這條路也印得出 PDF。</summary>
    [Fact]
    public async Task Batch_job_by_number_range_serves_merged_pdf()
    {
        var client = await AuthedAsync();

        var listResp = await client.GetAsync("/api/v1/signups?year=115&signupType=1");
        var list = await listResp.Content.ReadFromJsonAsync<SignupListResponse>();
        list!.Items.Should().NotBeEmpty();

        // 刻意只取最小編號那一小段：本測試要驗的是「編號區間這條選取路徑」，
        // 不是渲染吞吐量。整年份全撈會讓 job 跑掉整個輪詢時限。
        var minNumber = list.Items.Min(i => i.Number ?? int.MaxValue);

        var createResp = await client.PostAsJsonAsync("/api/v1/reports/batch/jobs",
            new BatchReportRequest("datacard", minNumber, minNumber + 1, Year: 115, SignupType: 1));
        createResp.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var created = (await createResp.Content.ReadFromJsonAsync<BatchPrintJobCreated>())!;
        created.FileName.Should().StartWith("batch-datacard-");

        (await PollUntilTerminalAsync(client, created.JobId)).Status.Should().Be("completed");

        var fileResp = await client.GetAsync($"/api/v1/reports/batch/jobs/{created.JobId}/file");
        fileResp.StatusCode.Should().Be(HttpStatusCode.OK);
        fileResp.Content.Headers.ContentType?.MediaType.Should().Be("application/pdf");
        fileResp.Headers.GetValues("X-Signup-Count").Single().Should().NotBeNullOrEmpty();

        var bytes = await fileResp.Content.ReadAsByteArrayAsync();
        bytes.Length.Should().BeGreaterThan(1000);
        bytes[0].Should().Be(0x25);  // %PDF
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

    private static async Task<Guid> FirstSignupIdAsync(HttpClient client, int signupType)
    {
        var listResp = await client.GetAsync($"/api/v1/signups?year=115&signupType={signupType}");
        var list = await listResp.Content.ReadFromJsonAsync<SignupListResponse>();
        list.Should().NotBeNull();
        list!.Items.Should().NotBeEmpty();
        return list.Items[0].Id;
    }

    private async Task AssertReportEndpoint(string endpoint, int signupType, string expectedPrefix)
    {
        var client = await AuthedAsync();
        var signupId = await FirstSignupIdAsync(client, signupType);

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
