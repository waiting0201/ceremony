using Ceremony.Application.Categories;
using FluentAssertions;
using Moq;

namespace Ceremony.Application.Tests.Categories;

public sealed class ListCategoriesHandlerTests
{
    private readonly Mock<ICategoryRepository> _repo = new();
    private ListCategoriesHandler CreateSut() => new(_repo.Object);

    [Fact]
    public async Task EmptyRepo_returns_empty_response()
    {
        _repo.Setup(r => r.GetAllAsync(default)).ReturnsAsync([]);

        var result = await CreateSut().HandleAsync();
        result.Items.Should().BeEmpty();
        result.Total.Should().Be(0);
    }

    [Fact]
    public async Task BuildsTwoLevelTree_ordered_by_sort()
    {
        var root1 = Guid.Parse("18927907-dcad-42b2-8f2a-635c2e0fa98d");
        var root2 = Guid.Parse("0c478f0e-787c-448e-ba7b-b1579f3f1fce");
        var child1A = Guid.NewGuid();
        var child1B = Guid.NewGuid();
        var child2A = Guid.NewGuid();

        _repo.Setup(r => r.GetAllAsync(default)).ReturnsAsync(new[]
        {
            new CategoryRow(root2, "中元", null, 2),
            new CategoryRow(root1, "春季", null, 1),
            new CategoryRow(child1B, "梁皇 B", root1, 2),
            new CategoryRow(child1A, "梁皇 A", root1, 1),
            new CategoryRow(child2A, "地藏經", root2, 1),
        });

        var result = await CreateSut().HandleAsync();

        result.Total.Should().Be(2);
        result.Items.Should().HaveCount(2);
        result.Items[0].Id.Should().Be(root1, "Sort=1 排第一");
        result.Items[0].Title.Should().Be("春季");
        result.Items[0].Children.Should().HaveCount(2);
        result.Items[0].Children[0].Id.Should().Be(child1A, "child Sort=1 排第一");
        result.Items[0].Children[0].Children.Should().BeEmpty();
        result.Items[1].Id.Should().Be(root2);
        result.Items[1].Children.Should().HaveCount(1);
    }

    [Fact]
    public async Task RootWithNoChildren_has_empty_children_array()
    {
        var root = Guid.NewGuid();
        _repo.Setup(r => r.GetAllAsync(default)).ReturnsAsync([
            new CategoryRow(root, "孤兒根", null, 1)
        ]);

        var result = await CreateSut().HandleAsync();
        result.Items[0].Children.Should().NotBeNull().And.BeEmpty();
    }
}
