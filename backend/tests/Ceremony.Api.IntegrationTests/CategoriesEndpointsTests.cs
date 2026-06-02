using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Ceremony.Application.Auth;
using Ceremony.Application.Categories;
using FluentAssertions;

namespace Ceremony.Api.IntegrationTests;

public sealed class CategoriesEndpointsTests(CeremonyApiFactory factory) : IClassFixture<CeremonyApiFactory>
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
    public async Task GET_categories_without_token_returns_401()
    {
        var resp = await _factory.CreateClient().GetAsync("/api/v1/categories");
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GET_categories_with_token_returns_tree()
    {
        var client = await AuthedAsync();
        var resp = await client.GetAsync("/api/v1/categories");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await resp.Content.ReadFromJsonAsync<CategoryListResponse>();
        body.Should().NotBeNull();
        body!.Items.Should().NotBeEmpty("(local) Ceremony DB has at least the 3 fixed roots");
        body.Total.Should().Be(body.Items.Count);

        // 預期含 3 個固定根 GUID（春季 / 中元 / 秋季）
        body.Items.Select(i => i.Id).Should().Contain(Guid.Parse("18927907-dcad-42b2-8f2a-635c2e0fa98d"));
        body.Items.Select(i => i.Id).Should().Contain(Guid.Parse("0c478f0e-787c-448e-ba7b-b1579f3f1fce"));
        body.Items.Select(i => i.Id).Should().Contain(Guid.Parse("3864e4dc-24db-4544-acb3-3351592f6dab"));

        // 每個根都應有 children 陣列（可能為空）
        body.Items.Should().AllSatisfy(i => i.Children.Should().NotBeNull());
    }

    [Fact]
    public async Task POST_empty_title_returns_400_verbatim()
    {
        var client = await AuthedAsync();
        var resp = await client.PostAsJsonAsync("/api/v1/categories", new { title = "  ", sort = 1 });
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await resp.Content.ReadAsStringAsync();
        body.Should().Contain("VALIDATION_REQUIRED").And.Contain("請輸入名稱");
    }

    [Fact]
    public async Task POST_unknownParent_returns_404()
    {
        var client = await AuthedAsync();
        var resp = await client.PostAsJsonAsync("/api/v1/categories", new
        {
            title = "test",
            sort = 1,
            parentId = Guid.NewGuid(),
        });
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DELETE_unknownId_returns_404()
    {
        var client = await AuthedAsync();
        var resp = await client.DeleteAsync($"/api/v1/categories/{Guid.NewGuid()}");
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DELETE_spring_root_returns_409_dependency()
    {
        var client = await AuthedAsync();
        var spring = Guid.Parse("18927907-dcad-42b2-8f2a-635c2e0fa98d");
        var resp = await client.DeleteAsync($"/api/v1/categories/{spring}");
        resp.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var body = await resp.Content.ReadAsStringAsync();
        body.Should().Contain("CATEGORY_HAS_DEPENDENCY").And.Contain("已有報名或還有下層法會，無法刪除");
    }

    [Fact]
    public async Task Full_category_CRUD_lifecycle()
    {
        var client = await AuthedAsync();
        var title = $"itest_cat_{DateTime.UtcNow:HHmmssfff}";

        var createResp = await client.PostAsJsonAsync("/api/v1/categories", new { title, sort = 99, parentId = (Guid?)null });
        createResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResp.Content.ReadFromJsonAsync<CategoryItem>();
        created!.Title.Should().Be(title);

        var id = created.Id;
        Guid? childId = null;
        try
        {
            var putResp = await client.PutAsJsonAsync($"/api/v1/categories/{id}", new { title = title + "_v2", sort = 100 });
            putResp.StatusCode.Should().Be(HttpStatusCode.OK);
            var updated = await putResp.Content.ReadFromJsonAsync<CategoryItem>();
            updated!.Title.Should().Be(title + "_v2");

            var childResp = await client.PostAsJsonAsync("/api/v1/categories", new { title = "child", sort = 1, parentId = id });
            childResp.StatusCode.Should().Be(HttpStatusCode.Created);
            var child = await childResp.Content.ReadFromJsonAsync<CategoryItem>();
            childId = child!.Id;

            // DELETE parent → 409 (has child)
            var dupResp = await client.DeleteAsync($"/api/v1/categories/{id}");
            dupResp.StatusCode.Should().Be(HttpStatusCode.Conflict);

            // DELETE child first → 204
            var delChild = await client.DeleteAsync($"/api/v1/categories/{childId}");
            delChild.StatusCode.Should().Be(HttpStatusCode.NoContent);
            childId = null;

            // DELETE parent → 204
            var delParent = await client.DeleteAsync($"/api/v1/categories/{id}");
            delParent.StatusCode.Should().Be(HttpStatusCode.NoContent);
        }
        finally
        {
            if (childId.HasValue) await client.DeleteAsync($"/api/v1/categories/{childId}");
            await client.DeleteAsync($"/api/v1/categories/{id}");
        }
    }

    [Fact]
    public async Task POST_child_of_second_level_returns_422_depth_limit()
    {
        var client = await AuthedAsync();
        var rootTitle = $"itest_root_{DateTime.UtcNow:HHmmssfff}";

        var root = await client.PostAsJsonAsync("/api/v1/categories", new { title = rootTitle, sort = 99, parentId = (Guid?)null });
        var rootItem = await root.Content.ReadFromJsonAsync<CategoryItem>();
        var rootId = rootItem!.Id;
        Guid? childId = null;

        try
        {
            var child = await client.PostAsJsonAsync("/api/v1/categories", new { title = "child", sort = 1, parentId = rootId });
            var childItem = await child.Content.ReadFromJsonAsync<CategoryItem>();
            childId = childItem!.Id;

            // 嘗試在第二層下再新增 → 422 DEPTH_LIMIT
            var grandchild = await client.PostAsJsonAsync("/api/v1/categories", new { title = "no!", sort = 1, parentId = childId });
            grandchild.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
            var body = await grandchild.Content.ReadAsStringAsync();
            body.Should().Contain("CATEGORY_DEPTH_LIMIT").And.Contain("第一層之下不可再新增");
        }
        finally
        {
            if (childId.HasValue) await client.DeleteAsync($"/api/v1/categories/{childId}");
            await client.DeleteAsync($"/api/v1/categories/{rootId}");
        }
    }
}
