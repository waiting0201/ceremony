using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Ceremony.Application.Auth;
using Ceremony.Application.Prepay;
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
}
