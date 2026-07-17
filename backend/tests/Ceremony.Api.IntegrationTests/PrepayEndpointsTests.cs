using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Ceremony.Application.Auth;
using Ceremony.Application.Prepay;
using Ceremony.Application.Signups;
using FluentAssertions;

namespace Ceremony.Api.IntegrationTests;

public sealed class PrepayEndpointsTests(CeremonyApiFactory factory) : IClassFixture<CeremonyApiFactory>
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
    public async Task POST_load_without_token_returns_401()
    {
        var resp = await _factory.CreateClient().PostAsJsonAsync("/api/v1/prepay/load", new
        {
            sourceYear = 114,
            sourceCeremonyId = Guid.NewGuid(),
            targetYear = 115,
            targetCeremonyId = Guid.NewGuid(),
            believerGroup = 1,
        });
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task POST_load_zeroYear_returns_400_verbatim()
    {
        var client = await AuthedAsync();
        var resp = await client.PostAsJsonAsync("/api/v1/prepay/load", new
        {
            sourceYear = 114,
            sourceCeremonyId = Guid.NewGuid(),
            targetYear = 0,
            targetCeremonyId = Guid.NewGuid(),
            believerGroup = 1,
        });
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await resp.Content.ReadAsStringAsync();
        body.Should().Contain("VALIDATION_REQUIRED").And.Contain("請選擇年份");
    }

    [Fact]
    public async Task POST_load_emptyCeremony_returns_400_verbatim()
    {
        var client = await AuthedAsync();
        var resp = await client.PostAsJsonAsync("/api/v1/prepay/load", new
        {
            sourceYear = 114,
            sourceCeremonyId = Guid.NewGuid(),
            targetYear = 115,
            targetCeremonyId = Guid.Empty,
            believerGroup = 1,
        });
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await resp.Content.ReadAsStringAsync();
        body.Should().Contain("請選擇法會");
    }

    [Fact]
    public async Task POST_load_invalidGroup_returns_400()
    {
        var client = await AuthedAsync();
        var resp = await client.PostAsJsonAsync("/api/v1/prepay/load", new
        {
            sourceYear = 114,
            sourceCeremonyId = Guid.Parse("18927907-dcad-42b2-8f2a-635c2e0fa98d"),
            targetYear = 115,
            targetCeremonyId = Guid.Parse("18927907-dcad-42b2-8f2a-635c2e0fa98d"),
            believerGroup = 99,
        });
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await resp.Content.ReadAsStringAsync();
        body.Should().Contain("無效的信眾類別");
    }

    [Fact]
    public async Task POST_load_unknownTargetCategory_returns_404()
    {
        var client = await AuthedAsync();
        var resp = await client.PostAsJsonAsync("/api/v1/prepay/load", new
        {
            sourceYear = 114,
            sourceCeremonyId = Guid.NewGuid(),
            targetYear = 115,
            targetCeremonyId = Guid.NewGuid(),  // 隨機，不存在於 DB
            believerGroup = 1,
        });
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var body = await resp.Content.ReadAsStringAsync();
        body.Should().Contain("CATEGORY_NOT_FOUND");
    }

    [Fact]
    public async Task POST_load_existingTargetCategory_noSources_returns_200_zeroLoaded()
    {
        var client = await AuthedAsync();
        // 春季 → 春季，未來年份不太可能有預繳資料 → 0 loaded
        var spring = Guid.Parse("18927907-dcad-42b2-8f2a-635c2e0fa98d");
        var resp = await client.PostAsJsonAsync("/api/v1/prepay/load", new
        {
            sourceYear = 800,    // 不存在的源年份
            sourceCeremonyId = spring,
            targetYear = 801,    // 不存在的目標年份
            targetCeremonyId = spring,
            believerGroup = 1,
        });
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadFromJsonAsync<PrepayLoadResponse>();
        body!.Loaded.Should().Be(0);
        body.Skipped.Should().Be(0);
    }

    [Fact]
    public async Task POST_load_withRealSource_inserts_signup_and_log_then_rerun_skips()
    {
        var client = await AuthedAsync();
        var spring = Guid.Parse("18927907-dcad-42b2-8f2a-635c2e0fa98d");
        const int sourceYear = 880;   // 專用年份，避開真實資料
        const int targetYear = 881;

        // 1. 臨時 Believer（employeeType=1 → 對應 believerGroup=1「非員工一般」）
        var believerName = $"itest_prepay_{DateTime.UtcNow:yyMMddHHmmssfff}";
        var believerResp = await client.PostAsJsonAsync("/api/v1/believers", new
        {
            employeeType = 1,
            name = believerName,
            mailAddress = "整合測試地址",
        });
        believerResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var believer = await believerResp.Content.ReadFromJsonAsync<Application.Believers.BelieverListItem>();

        // 2. 源報名（帶預繳 targetYear + 春季）
        var srcResp = await client.PostAsJsonAsync("/api/v1/signups", new
        {
            year = sourceYear,
            ceremonyCategoryId = spring,
            signupType = 1,
            believerId = believer!.Id,
            name = believerName,
            mailAddress = "整合測試地址",
            prepayYear = targetYear,
            prepayCeremonyCategoryId = spring,
        });
        srcResp.StatusCode.Should().Be(HttpStatusCode.Created);

        // 3. 載入預繳 → 真正走到 insert Signup + SignupLog 的路徑
        //    （回歸鎖：SignupLogs.Name 為 NOT NULL，log 快照必須帶信眾姓名，不能沿用 Signup 的 null）
        object LoadBody() => new
        {
            sourceYear,
            sourceCeremonyId = spring,
            targetYear,
            targetCeremonyId = spring,
            believerGroup = 1,
        };
        var loadResp = await client.PostAsJsonAsync("/api/v1/prepay/load", LoadBody());
        loadResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var load = await loadResp.Content.ReadFromJsonAsync<PrepayLoadResponse>();
        load!.Loaded.Should().Be(1);
        load.Skipped.Should().Be(0);

        // 4. 目標年應有這位信眾的新 Signup（Name 對齊舊系統留 null，列表顯示名靠 Believer join）
        var listResp = await client.GetAsync(
            $"/api/v1/signups?year={targetYear}&ceremonyCategoryId={spring}&signupType=1");
        listResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var list = await listResp.Content.ReadFromJsonAsync<SignupListResponse>();
        var created = list!.Items.Single(i => i.BelieverId == believer.Id);
        created.Number.Should().BeGreaterThan(0);

        // 5. 對應 SignupLog 存在，Name = 信眾姓名快照
        var logsResp = await client.GetAsync($"/api/v1/signups/{created.Id}/logs");
        logsResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var logs = await logsResp.Content.ReadFromJsonAsync<SignupLogListResponse>();
        logs!.Total.Should().Be(1);
        logs.Items[0].Name.Should().Be(believerName);
        logs.Items[0].Phone.Should().BeNull();

        // 6. 重跑 → idempotent：同信眾不重複載入
        var rerunResp = await client.PostAsJsonAsync("/api/v1/prepay/load", LoadBody());
        rerunResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var rerun = await rerunResp.Content.ReadFromJsonAsync<PrepayLoadResponse>();
        rerun!.Loaded.Should().Be(0);
        rerun.Skipped.Should().Be(1);
    }
}
