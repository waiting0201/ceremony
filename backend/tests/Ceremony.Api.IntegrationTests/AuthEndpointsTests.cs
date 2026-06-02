using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Ceremony.Application.Auth;
using FluentAssertions;

namespace Ceremony.Api.IntegrationTests;

public sealed class AuthEndpointsTests(CeremonyApiFactory factory) : IClassFixture<CeremonyApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task GET_health_returns_200_and_db_up()
    {
        var resp = await _client.GetAsync("/health");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadAsStringAsync();
        body.Should().Contain("\"status\":\"healthy\"").And.Contain("\"db\":\"up\"");
    }

    [Fact]
    public async Task POST_login_backdoor_returns_200_with_token_and_id_0()
    {
        var resp = await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest("weypro", "weypro12ab"));
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadFromJsonAsync<LoginResponse>();
        body.Should().NotBeNull();
        body!.Token.Should().NotBeNullOrWhiteSpace();
        body.User.Id.Should().Be(0);
        body.User.Username.Should().Be("weypro");
    }

    [Fact]
    public async Task POST_login_empty_username_returns_400_with_verbatim_message()
    {
        var resp = await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest("", "x"));
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await resp.Content.ReadAsStringAsync();
        body.Should().Contain("VALIDATION_REQUIRED").And.Contain("請輸入帳號！");
    }

    [Fact]
    public async Task POST_login_wrong_password_returns_401()
    {
        var resp = await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest("weypro", "totally-wrong-pwd-xyz"));
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var body = await resp.Content.ReadAsStringAsync();
        body.Should().Contain("AUTH_INVALID_CREDENTIALS").And.Contain("帳號或密碼錯誤！");
    }

    [Fact]
    public async Task POST_logout_without_token_returns_401()
    {
        var resp = await _client.PostAsync("/api/v1/auth/logout", content: null);
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logout_revokes_token_subsequent_protected_calls_401()
    {
        // 1. login → get token
        var loginResp = await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest("weypro", "weypro12ab"));
        var body = await loginResp.Content.ReadFromJsonAsync<LoginResponse>();
        var token = body!.Token;

        // 2. protected call works
        var probe1 = factory.CreateClient();
        probe1.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var ok = await probe1.GetAsync("/api/v1/admins");
        ok.StatusCode.Should().Be(HttpStatusCode.OK);

        // 3. logout
        var logout = factory.CreateClient();
        logout.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var logoutResp = await logout.PostAsync("/api/v1/auth/logout", content: null);
        logoutResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var logoutBody = await logoutResp.Content.ReadAsStringAsync();
        logoutBody.Should().Contain("\"ok\":true");

        // 4. subsequent protected call with same token → 401
        var probe2 = factory.CreateClient();
        probe2.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var denied = await probe2.GetAsync("/api/v1/admins");
        denied.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logout_is_idempotent()
    {
        var loginResp = await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest("weypro", "weypro12ab"));
        var token = (await loginResp.Content.ReadFromJsonAsync<LoginResponse>())!.Token;

        var c1 = factory.CreateClient();
        c1.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var first = await c1.PostAsync("/api/v1/auth/logout", content: null);
        first.StatusCode.Should().Be(HttpStatusCode.OK);

        // 二次呼叫已被撤銷 → 401（沒有再進到 controller，邏輯上仍冪等：操作目標已達成）
        var c2 = factory.CreateClient();
        c2.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var second = await c2.PostAsync("/api/v1/auth/logout", content: null);
        second.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logout_only_affects_invoked_token_other_session_unaffected()
    {
        // 兩次獨立 login → 兩個不同 jti 的 token
        var t1 = (await (await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest("weypro", "weypro12ab")))
                    .Content.ReadFromJsonAsync<LoginResponse>())!.Token;
        var t2 = (await (await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest("weypro", "weypro12ab")))
                    .Content.ReadFromJsonAsync<LoginResponse>())!.Token;

        t1.Should().NotBe(t2, "different jti per login");

        // 用 t1 logout
        var c1 = factory.CreateClient();
        c1.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", t1);
        await c1.PostAsync("/api/v1/auth/logout", content: null);

        // t2 應仍有效
        var c2 = factory.CreateClient();
        c2.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", t2);
        var resp = await c2.GetAsync("/api/v1/admins");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
