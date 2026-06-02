using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Ceremony.Application.Admins;
using Ceremony.Application.Auth;
using FluentAssertions;

namespace Ceremony.Api.IntegrationTests;

public sealed class AdminsEndpointsTests(CeremonyApiFactory factory) : IClassFixture<CeremonyApiFactory>
{
    private readonly CeremonyApiFactory _factory = factory;

    private async Task<string> GetBackdoorTokenAsync()
    {
        var client = _factory.CreateClient();
        var resp = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest("weypro", "weypro12ab"));
        var body = await resp.Content.ReadFromJsonAsync<LoginResponse>();
        return body!.Token;
    }

    [Fact]
    public async Task GET_admins_without_token_returns_401()
    {
        var client = _factory.CreateClient();
        var resp = await client.GetAsync("/api/v1/admins");
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GET_admins_with_token_returns_200_with_items()
    {
        var token = await GetBackdoorTokenAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var resp = await client.GetAsync("/api/v1/admins");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var raw = await resp.Content.ReadAsStringAsync();

        // Password 絕不該出現在 response payload。
        raw.Should().NotContainEquivalentOf("password");

        var body = System.Text.Json.JsonSerializer.Deserialize<AdminListResponse>(raw,
            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        body.Should().NotBeNull();
        body!.Items.Should().NotBeEmpty("(local) Ceremony DB has seeded admins");
        body.Total.Should().Be(body.Items.Count);
    }

    [Fact]
    public async Task POST_admins_creates_and_then_DuplicateRequest_returns_409()
    {
        // 用時間戳避免測試之間 / 真實資料衝突；無 cleanup（DB 凍結，軟刪除策略）。
        var unique = $"itest_{DateTime.UtcNow:yyMMddHHmmssfff}";

        var token = await GetBackdoorTokenAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var firstReq = new CreateAdminRequest(unique, "testpwd", "Integration Test");
        var first = await client.PostAsJsonAsync("/api/v1/admins", firstReq);
        first.StatusCode.Should().Be(HttpStatusCode.Created);
        first.Headers.Location.Should().NotBeNull();

        var createdRaw = await first.Content.ReadAsStringAsync();
        createdRaw.Should().NotContainEquivalentOf("password");
        var created = System.Text.Json.JsonSerializer.Deserialize<AdminListItem>(createdRaw,
            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        created!.Username.Should().Be(unique);
        created.Id.Should().BeGreaterThan(0);

        // 重複 username → 409
        var dup = await client.PostAsJsonAsync("/api/v1/admins", firstReq);
        dup.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var dupBody = await dup.Content.ReadAsStringAsync();
        dupBody.Should().Contain("ADMIN_DUPLICATE_USERNAME").And.Contain("帳號重複，請重新確認！");
    }

    [Fact]
    public async Task POST_admins_empty_username_returns_400_with_verbatim_message()
    {
        var token = await GetBackdoorTokenAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var resp = await client.PostAsJsonAsync("/api/v1/admins", new CreateAdminRequest("", "pwd", null));
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await resp.Content.ReadAsStringAsync();
        body.Should().Contain("VALIDATION_REQUIRED").And.Contain("請輸入帳號");
    }
}
