using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Ceremony.Application.Auth;
using Ceremony.Application.Signups;
using FluentAssertions;

namespace Ceremony.Api.IntegrationTests;

public sealed class SignupsEndpointsTests(CeremonyApiFactory factory) : IClassFixture<CeremonyApiFactory>
{
    private readonly CeremonyApiFactory _factory = factory;

    private async Task<HttpClient> AuthedAsync()
    {
        var c = _factory.CreateClient();
        var resp = await c.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest("weypro", "weypro12ab"));
        var body = await resp.Content.ReadFromJsonAsync<LoginResponse>();
        var x = _factory.CreateClient();
        x.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body!.Token);
        return x;
    }

    [Fact]
    public async Task GET_signups_without_token_returns_401()
    {
        var resp = await _factory.CreateClient().GetAsync("/api/v1/signups");
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GET_signups_with_year_filter_returns_200_with_shape()
    {
        var client = await AuthedAsync();
        var resp = await client.GetAsync("/api/v1/signups?year=110");  // 任挑一個歷史年份
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await resp.Content.ReadFromJsonAsync<SignupListResponse>();
        body.Should().NotBeNull();
        body!.Items.Should().NotBeNull();
        body.Total.Should().Be(body.Items.Count);
    }

    [Fact]
    public async Task GET_signups_with_searchKey_but_no_scope_returns_unfiltered_AND_set()
    {
        var client = await AuthedAsync();
        // 給 searchKey 但所有 scope* 都 false → OR 群組應略過，只有 AND 條件生效
        var resp = await client.GetAsync("/api/v1/signups?year=120&searchKey=test");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadFromJsonAsync<SignupListResponse>();
        body.Should().NotBeNull();
    }

    [Fact]
    public async Task GET_signups_with_sentinel_values_treated_as_unset()
    {
        var client = await AuthedAsync();
        // -1 SignupType + 0 Number + Guid.Empty CeremonyCategoryId 都應被 normalize 掉
        var resp = await client.GetAsync($"/api/v1/signups?year=115&signupType=-1&number=0&ceremonyCategoryId={Guid.Empty}");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadFromJsonAsync<SignupListResponse>();
        body.Should().NotBeNull();
    }

    [Fact]
    public async Task GET_signup_by_unknownId_returns_404_verbatim()
    {
        var client = await AuthedAsync();
        var resp = await client.GetAsync($"/api/v1/signups/{Guid.NewGuid()}");
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var body = await resp.Content.ReadAsStringAsync();
        body.Should().Contain("SIGNUP_NOT_FOUND").And.Contain("找不到報名");
    }

    [Fact]
    public async Task GET_signup_logs_unknownId_returns_200_empty()
    {
        var client = await AuthedAsync();
        var resp = await client.GetAsync($"/api/v1/signups/{Guid.NewGuid()}/logs");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadFromJsonAsync<SignupLogListResponse>();
        body.Should().NotBeNull();
        body!.Total.Should().Be(0);
        body.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task POST_signup_empty_name_returns_400()
    {
        var client = await AuthedAsync();
        var req = new
        {
            year = 115,
            ceremonyCategoryId = Guid.Parse("18927907-dcad-42b2-8f2a-635c2e0fa98d"),  // 春季 固定根
            signupType = 1,
            believerId = Guid.NewGuid(),
            name = "",
            mailAddress = "addr",
        };
        var resp = await client.PostAsJsonAsync("/api/v1/signups", req);
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await resp.Content.ReadAsStringAsync();
        body.Should().Contain("VALIDATION_REQUIRED").And.Contain("請輸入姓名");
    }

    [Fact]
    public async Task POST_signup_invalid_signupType_returns_400()
    {
        var client = await AuthedAsync();
        var req = new
        {
            year = 115,
            ceremonyCategoryId = Guid.Parse("18927907-dcad-42b2-8f2a-635c2e0fa98d"),
            signupType = 99,
            believerId = Guid.NewGuid(),
            name = "X",
            mailAddress = "addr",
        };
        var resp = await client.PostAsJsonAsync("/api/v1/signups", req);
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await resp.Content.ReadAsStringAsync();
        body.Should().Contain("報名類型錯誤");
    }

    [Fact]
    public async Task POST_signup_unknownBeliever_returns_404()
    {
        var client = await AuthedAsync();
        var req = new
        {
            year = 115,
            ceremonyCategoryId = Guid.Parse("18927907-dcad-42b2-8f2a-635c2e0fa98d"),
            signupType = 1,
            believerId = Guid.NewGuid(),
            name = "X",
            mailAddress = "addr",
        };
        var resp = await client.PostAsJsonAsync("/api/v1/signups", req);
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var body = await resp.Content.ReadAsStringAsync();
        body.Should().Contain("BELIEVER_NOT_FOUND");
    }

    [Fact]
    public async Task PUT_signup_unknownId_returns_404()
    {
        var client = await AuthedAsync();
        var req = new
        {
            year = 999,
            ceremonyCategoryId = Guid.Parse("18927907-dcad-42b2-8f2a-635c2e0fa98d"),
            signupType = 1,
            believerId = Guid.NewGuid(),
            name = "X",
            mailAddress = "addr",
            customNumber = 1,
        };
        var resp = await client.PutAsJsonAsync($"/api/v1/signups/{Guid.NewGuid()}", req);
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DELETE_signup_unknownId_returns_404()
    {
        var client = await AuthedAsync();
        var resp = await client.DeleteAsync($"/api/v1/signups/{Guid.NewGuid()}");
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task POST_export_returns_xlsx_attachment()
    {
        var client = await AuthedAsync();
        var resp = await client.PostAsJsonAsync("/api/v1/signups/export", new SignupSearchQuery(Year: 115, SignupType: 1));
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        resp.Content.Headers.ContentType?.MediaType.Should().Be("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        resp.Content.Headers.ContentDisposition?.FileName.Should().StartWith("signups-");

        var bytes = await resp.Content.ReadAsByteArrayAsync();
        bytes.Length.Should().BeGreaterThan(100, "xlsx 含 OPC zip header + sheet xml，最少數百 byte");
        // xlsx 是 ZIP 開頭 0x50 0x4B
        bytes[0].Should().Be(0x50);
        bytes[1].Should().Be(0x4B);
    }

    [Fact]
    public async Task Full_signup_lifecycle_create_read_and_logs()
    {
        var client = await AuthedAsync();

        // 1. 先建立一個臨時 Believer 給 signup 用
        var believerName = $"itest_b_{DateTime.UtcNow:yyMMddHHmmssfff}";
        var believerResp = await client.PostAsJsonAsync("/api/v1/believers", new
        {
            employeeType = 1,
            name = believerName,
            mailAddress = "整合測試地址",
        });
        believerResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var believer = await believerResp.Content.ReadFromJsonAsync<Application.Believers.BelieverListItem>();

        try
        {
            // 2. POST /signups — 用一個未來年份 + 春季 + 普桌(4)，最不太可能撞到真實資料
            var futureYear = 999;
            var signupType = 4;  // 普 → NumberTitle "普"
            var ceremonyId = Guid.Parse("18927907-dcad-42b2-8f2a-635c2e0fa98d"); // 春季
            var createReq = new
            {
                year = futureYear,
                ceremonyCategoryId = ceremonyId,
                signupType,
                believerId = believer!.Id,
                name = believerName,
                mailAddress = "整合測試地址",
                phone = "０９００－０００－０００",  // 全形 → 應轉半形
                fee = 1200,
                livingNames = new[] { "陽上一", "", "", "", "", "" },
                deadNames = new[] { "往生甲", "", "", "", "", "" },
            };
            var resp = await client.PostAsJsonAsync("/api/v1/signups", createReq);
            resp.StatusCode.Should().Be(HttpStatusCode.Created);
            var created = await resp.Content.ReadFromJsonAsync<SignupListItem>();
            created.Should().NotBeNull();
            created!.Year.Should().Be(futureYear);
            created.SignupType.Should().Be(signupType);
            created.NumberTitle.Should().Be("普", because: "SignupType=4 → 普");
            created.Number.Should().BeGreaterThan(0);
            created.Phone.Should().Be("0900-000-000", because: "phone 應轉半形");

            var signupId = created.Id;

            // 3. GET /signups/{id}
            var getResp = await client.GetAsync($"/api/v1/signups/{signupId}");
            getResp.StatusCode.Should().Be(HttpStatusCode.OK);
            var got = await getResp.Content.ReadFromJsonAsync<SignupListItem>();
            got!.Name.Should().Be(believerName);

            // 4. GET /signups/{id}/logs — 應該有一筆 SignupLog
            var logsResp = await client.GetAsync($"/api/v1/signups/{signupId}/logs");
            logsResp.StatusCode.Should().Be(HttpStatusCode.OK);
            var logs = await logsResp.Content.ReadFromJsonAsync<SignupLogListResponse>();
            logs!.Total.Should().Be(1);
            logs.Items[0].SignupId.Should().Be(signupId);
            logs.Items[0].Number.Should().Be(created.Number);
            logs.Items[0].NumberTitle.Should().Be("普");
            logs.Items[0].Admin.Should().Be("Administrator", because: "backdoor JWT 的 name claim 是 Administrator");
            logs.Items[0].CeremonyCategoryTitle.Should().Be("春季");

            // 5. POST 第二筆相同 keepNumber → 應 409
            var dupResp = await client.PostAsJsonAsync("/api/v1/signups", new
            {
                year = futureYear,
                ceremonyCategoryId = ceremonyId,
                signupType,
                believerId = believer.Id,
                name = "another",
                mailAddress = "addr",
                keepNumber = true,
                customNumber = created.Number,
            });
            dupResp.StatusCode.Should().Be(HttpStatusCode.Conflict);
            var dupBody = await dupResp.Content.ReadAsStringAsync();
            dupBody.Should().Contain("SIGNUP_NUMBER_CONFLICT").And.Contain("編號重複");

            // 6. POST 第二筆 auto-number → 應拿到 number+1
            var nextResp = await client.PostAsJsonAsync("/api/v1/signups", new
            {
                year = futureYear,
                ceremonyCategoryId = ceremonyId,
                signupType,
                believerId = believer.Id,
                name = "another",
                mailAddress = "addr",
            });
            nextResp.StatusCode.Should().Be(HttpStatusCode.Created);
            var next = await nextResp.Content.ReadFromJsonAsync<SignupListItem>();
            next!.Number.Should().Be(created.Number + 1, because: "UPDLOCK 應該分配下一個編號");
        }
        finally
        {
            // Best-effort cleanup (硬刪除 Believer 會失敗如果還有 Signup，先不清；
            // futureYear=999 確保不影響真實資料)
        }
    }
}
