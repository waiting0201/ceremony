using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Ceremony.Application.Auth;
using Ceremony.Application.Believers;
using FluentAssertions;

namespace Ceremony.Api.IntegrationTests;

public sealed class BelieversEndpointsTests(CeremonyApiFactory factory) : IClassFixture<CeremonyApiFactory>
{
    private readonly CeremonyApiFactory _factory = factory;

    private async Task<HttpClient> CreateAuthedClientAsync()
    {
        var loginClient = _factory.CreateClient();
        var resp = await loginClient.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest("sa@system.local", "Admin@123"));
        var body = await resp.Content.ReadFromJsonAsync<LoginResponse>();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body!.Token);
        return client;
    }

    [Fact]
    public async Task GET_believers_without_token_returns_401()
    {
        var client = _factory.CreateClient();
        var resp = await client.GetAsync("/api/v1/believers?name=test");
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GET_believers_with_no_criteria_returns_400_verbatim()
    {
        var client = await CreateAuthedClientAsync();
        var resp = await client.GetAsync("/api/v1/believers");
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await resp.Content.ReadAsStringAsync();
        body.Should().Contain("VALIDATION_REQUIRED").And.Contain("請輸入搜尋條件");
    }

    [Fact]
    public async Task GET_believers_with_name_criterion_returns_200_with_response_shape()
    {
        var client = await CreateAuthedClientAsync();
        // 用「不太可能命中真實信眾」的關鍵字避免結果太大；shape 驗證才是重點。
        var resp = await client.GetAsync("/api/v1/believers?name=%E6%B8%AC%E8%A9%A6%E4%B8%8D%E5%AD%98%E5%9C%A8%E7%9A%84%E9%97%9C%E9%8D%B5%E5%AD%97zzz");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await resp.Content.ReadFromJsonAsync<BelieverListResponse>();
        body.Should().NotBeNull();
        body!.Items.Should().NotBeNull();
        body.Total.Should().Be(body.Items.Count);
    }

    [Fact]
    public async Task GET_believer_by_id_unknownId_returns_404_verbatim()
    {
        var client = await CreateAuthedClientAsync();
        var resp = await client.GetAsync($"/api/v1/believers/{Guid.NewGuid()}");
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var body = await resp.Content.ReadAsStringAsync();
        body.Should().Contain("BELIEVER_NOT_FOUND").And.Contain("找不到信眾");
    }

    [Fact]
    public async Task POST_empty_name_returns_400_verbatim()
    {
        var client = await CreateAuthedClientAsync();
        var resp = await client.PostAsJsonAsync("/api/v1/believers", new BelieverUpsertRequest(
            EmployeeType: 1, Name: "", MailAddress: "addr"));
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await resp.Content.ReadAsStringAsync();
        body.Should().Contain("VALIDATION_REQUIRED").And.Contain("請輸入姓名");
    }

    [Fact]
    public async Task POST_empty_mailAddress_is_allowed()
    {
        // 地址非必填（2026-07-21 使用者指定）：空白地址不再回 400，改為照常建立、地址存空字串。
        var client = await CreateAuthedClientAsync();
        var uniqueName = $"itest_noaddr_{DateTime.UtcNow:yyMMddHHmmssfff}";
        var resp = await client.PostAsJsonAsync("/api/v1/believers", new BelieverUpsertRequest(
            EmployeeType: 1, Name: uniqueName, MailAddress: ""));
        resp.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await resp.Content.ReadFromJsonAsync<BelieverListItem>();
        created.Should().NotBeNull();
        created!.Id.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task GET_believers_searchKey_finds_believer_with_no_signup_by_name_or_deadName()
    {
        // 新增報名的信眾搜尋改用 searchKey（14 欄 OR），重點是「從未報名過的信眾也要查得到」
        // ——/signups 只有報名紀錄，這條路徑正是舊 BelieverView LEFT JOIN 的等效補償。
        var client = await CreateAuthedClientAsync();
        var stamp = $"{DateTime.UtcNow:yyMMddHHmmssfff}";
        var uniqueName = $"itest_key_{stamp}";
        var uniqueDead = $"itest_dead_{stamp}";

        var createResp = await client.PostAsJsonAsync("/api/v1/believers", new BelieverUpsertRequest(
            EmployeeType: 1,
            Name: uniqueName,
            MailAddress: "台中市北區",
            DeadNames: [uniqueDead, null, null, null, null, null]));
        createResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResp.Content.ReadFromJsonAsync<BelieverListItem>();

        try
        {
            // 姓名命中（此信眾沒有任何報名）
            var byName = await client.GetFromJsonAsync<BelieverListResponse>(
                $"/api/v1/believers?searchKey={uniqueName}");
            byName!.Items.Should().ContainSingle(i => i.Id == created!.Id);

            // 往生名命中（同一把 searchKey OR 到 14 欄）
            var byDead = await client.GetFromJsonAsync<BelieverListResponse>(
                $"/api/v1/believers?searchKey={uniqueDead}");
            byDead!.Items.Should().ContainSingle(i => i.Id == created!.Id);
        }
        finally
        {
            await client.DeleteAsync($"/api/v1/believers/{created!.Id}");
        }
    }

    [Fact]
    public async Task Full_CRUD_lifecycle_create_read_update_delete()
    {
        var client = await CreateAuthedClientAsync();
        var uniqueName = $"itest_{DateTime.UtcNow:yyMMddHHmmssfff}";

        // CREATE
        var createReq = new BelieverUpsertRequest(
            EmployeeType: 1,
            Name: uniqueName,
            MailAddress: "台北市信義區市府路 1 號",
            HallName: "測試堂",
            Phone: "０９１２３４５６７８",                      // 全形數字 → 應轉半形
            MailZipcodeId: -1,                                // -1 應 normalize 為 null
            LivingNames: ["陽上一", null, "陽上三", null, null, null],
            DeadNames: [null, null, null, null, null, null]);

        var createResp = await client.PostAsJsonAsync("/api/v1/believers", createReq);
        createResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResp.Content.ReadFromJsonAsync<BelieverListItem>();
        created.Should().NotBeNull();
        created!.Id.Should().NotBe(Guid.Empty);
        created.Name.Should().Be(uniqueName);
        created.Phone.Should().Be("0912345678", because: "phone 應轉半形");
        created.MailZipcodeId.Should().BeNull(because: "-1 應 normalize 為 null");
        created.LivingNames[0].Should().Be("陽上一");

        var newId = created.Id;

        try
        {
            // READ
            var getResp = await client.GetAsync($"/api/v1/believers/{newId}");
            getResp.StatusCode.Should().Be(HttpStatusCode.OK);
            var got = await getResp.Content.ReadFromJsonAsync<BelieverListItem>();
            got!.Name.Should().Be(uniqueName);

            // UPDATE
            var updateReq = createReq with { Name = uniqueName + "_v2", HallName = null };
            var updateResp = await client.PutAsJsonAsync($"/api/v1/believers/{newId}", updateReq);
            updateResp.StatusCode.Should().Be(HttpStatusCode.OK);
            var updated = await updateResp.Content.ReadFromJsonAsync<BelieverListItem>();
            updated!.Name.Should().Be(uniqueName + "_v2");
            updated.HallName.Should().BeNull();

            // DELETE (no Signups so should succeed)
            var deleteResp = await client.DeleteAsync($"/api/v1/believers/{newId}");
            deleteResp.StatusCode.Should().Be(HttpStatusCode.NoContent);

            // READ after delete → 404
            var notFoundResp = await client.GetAsync($"/api/v1/believers/{newId}");
            notFoundResp.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }
        finally
        {
            // Safety net cleanup if anything fails mid-test (best-effort, ignore errors).
            await client.DeleteAsync($"/api/v1/believers/{newId}");
        }
    }

    [Fact]
    public async Task PUT_can_clear_existing_addresses_and_hallName()
    {
        // 2026-07-31 客訴：既有資料的寄件／文牒地址要能整段刪掉（含城市與郵遞區號），堂號同理。
        var client = await CreateAuthedClientAsync();
        var uniqueName = $"itest_clr_{DateTime.UtcNow:yyMMddHHmmssfff}";

        var cities = await (await client.GetAsync("/api/v1/zipcodes/cities"))
            .Content.ReadFromJsonAsync<Application.Zipcodes.ZipcodeCitiesResponse>();
        var city = cities!.Items.First();
        var areas = await (await client.GetAsync($"/api/v1/zipcodes?city={Uri.EscapeDataString(city)}"))
            .Content.ReadFromJsonAsync<Application.Zipcodes.ZipcodeAreasResponse>();
        var area = areas!.Items[0];

        var createReq = new BelieverUpsertRequest(
            EmployeeType: 1,
            Name: uniqueName,
            MailAddress: "市府路 1 號",
            HallName: "測試堂",
            MailZipcodeId: area.ZipcodeId,
            TextZipcodeId: area.ZipcodeId,
            TextAddress: "文牒路 2 號");
        var created = (await (await client.PostAsJsonAsync("/api/v1/believers", createReq))
            .Content.ReadFromJsonAsync<BelieverListItem>())!;
        created.MailCity.Should().NotBeNull();

        try
        {
            // 前端把三段（城市/區域/地址）都清空後送出的形狀
            var clearReq = createReq with
            {
                MailAddress = "",
                MailZipcodeId = null,
                TextAddress = "",
                TextZipcodeId = null,
                HallName = "",
            };
            var clearResp = await client.PutAsJsonAsync($"/api/v1/believers/{created.Id}", clearReq);
            clearResp.StatusCode.Should().Be(HttpStatusCode.OK);

            var cleared = (await (await client.GetAsync($"/api/v1/believers/{created.Id}"))
                .Content.ReadFromJsonAsync<BelieverListItem>())!;
            cleared.MailAddress.Should().BeNullOrEmpty();
            cleared.MailZipcodeId.Should().BeNull();
            cleared.MailCity.Should().BeNull(because: "城市/區域由 MailZipcodeID join 而來，區號清掉就整段消失");
            cleared.MailArea.Should().BeNull();
            cleared.TextAddress.Should().BeNullOrEmpty();
            cleared.TextZipcodeId.Should().BeNull();
            cleared.TextCity.Should().BeNull();
            cleared.TextArea.Should().BeNull();
            cleared.HallName.Should().BeNull();
        }
        finally
        {
            await client.DeleteAsync($"/api/v1/believers/{created.Id}");
        }
    }

    [Fact]
    public async Task PUT_unknownId_returns_404()
    {
        var client = await CreateAuthedClientAsync();
        var req = new BelieverUpsertRequest(1, "X", "addr");
        var resp = await client.PutAsJsonAsync($"/api/v1/believers/{Guid.NewGuid()}", req);
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DELETE_unknownId_returns_404()
    {
        var client = await CreateAuthedClientAsync();
        var resp = await client.DeleteAsync($"/api/v1/believers/{Guid.NewGuid()}");
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
