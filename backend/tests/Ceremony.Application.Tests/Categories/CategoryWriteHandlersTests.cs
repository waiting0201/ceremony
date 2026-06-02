using Ceremony.Application.Categories;
using Ceremony.Domain.Exceptions;
using FluentAssertions;
using Moq;

namespace Ceremony.Application.Tests.Categories;

public sealed class CreateCategoryHandlerTests
{
    private readonly Mock<ICategoryRepository> _repo = new();
    private CreateCategoryHandler CreateSut() => new(_repo.Object);

    [Fact]
    public async Task EmptyTitle_throws_REQUIRED_verbatim()
    {
        var act = () => CreateSut().HandleAsync(new CreateCategoryRequest("  ", 1));
        await act.Should().ThrowAsync<DomainException>()
            .Where(e => e.ErrorCode == "VALIDATION_REQUIRED" && e.Message == "請輸入名稱");
    }

    [Fact]
    public async Task TooLongTitle_throws_LENGTH()
    {
        var act = () => CreateSut().HandleAsync(new CreateCategoryRequest(new string('a', 51), 1));
        await act.Should().ThrowAsync<DomainException>()
            .Where(e => e.ErrorCode == "VALIDATION_LENGTH");
    }

    [Fact]
    public async Task UnknownParent_throws_NOT_FOUND()
    {
        var pid = Guid.NewGuid();
        _repo.Setup(r => r.GetByIdAsync(pid, default)).ReturnsAsync((CategoryRow?)null);

        var act = () => CreateSut().HandleAsync(new CreateCategoryRequest("X", 1, pid));
        await act.Should().ThrowAsync<DomainException>()
            .Where(e => e.ErrorCode == "CATEGORY_NOT_FOUND");
    }

    [Fact]
    public async Task ParentIsSecondLevel_throws_DEPTH_LIMIT()
    {
        var grandparent = Guid.NewGuid();
        var pid = Guid.NewGuid();
        _repo.Setup(r => r.GetByIdAsync(pid, default))
            .ReturnsAsync(new CategoryRow(pid, "child", grandparent, 1));

        var act = () => CreateSut().HandleAsync(new CreateCategoryRequest("X", 1, pid));
        await act.Should().ThrowAsync<DomainException>()
            .Where(e => e.ErrorCode == "CATEGORY_DEPTH_LIMIT" && e.Message == "第一層之下不可再新增");
    }

    [Fact]
    public async Task ValidRootCreate_returns_CategoryItem()
    {
        _repo.Setup(r => r.InsertAsync("法會 X", 5, null, default))
            .ReturnsAsync(Guid.Parse("11111111-1111-1111-1111-111111111111"));

        var result = await CreateSut().HandleAsync(new CreateCategoryRequest("  法會 X  ", 5));
        result.Title.Should().Be("法會 X");
        result.Sort.Should().Be(5);
        result.ParentId.Should().BeNull();
    }

    [Fact]
    public async Task ValidChildCreate_passes_parentId()
    {
        var pid = Guid.NewGuid();
        _repo.Setup(r => r.GetByIdAsync(pid, default))
            .ReturnsAsync(new CategoryRow(pid, "parent", null, 1));
        _repo.Setup(r => r.InsertAsync("梁皇", 2, pid, default))
            .ReturnsAsync(Guid.NewGuid());

        var result = await CreateSut().HandleAsync(new CreateCategoryRequest("梁皇", 2, pid));
        result.ParentId.Should().Be(pid);
    }
}

public sealed class DeleteCategoryHandlerTests
{
    private readonly Mock<ICategoryRepository> _repo = new();
    private DeleteCategoryHandler CreateSut() => new(_repo.Object);

    [Fact]
    public async Task NotFound_throws()
    {
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), default)).ReturnsAsync((CategoryRow?)null);
        var act = () => CreateSut().HandleAsync(Guid.NewGuid());
        await act.Should().ThrowAsync<DomainException>()
            .Where(e => e.ErrorCode == "CATEGORY_NOT_FOUND");
    }

    [Fact]
    public async Task HasDependency_throws_verbatim()
    {
        var id = Guid.NewGuid();
        _repo.Setup(r => r.GetByIdAsync(id, default)).ReturnsAsync(new CategoryRow(id, "X", null, 1));
        _repo.Setup(r => r.HasDependencyAsync(id, default)).ReturnsAsync(true);

        var ex = await Assert.ThrowsAsync<DomainException>(() => CreateSut().HandleAsync(id));
        ex.ErrorCode.Should().Be("CATEGORY_HAS_DEPENDENCY");
        ex.Message.Should().Be("已有報名或還有下層法會，無法刪除");
        _repo.Verify(r => r.DeleteAsync(It.IsAny<Guid>(), default), Times.Never);
    }

    [Fact]
    public async Task NoDependency_deletes()
    {
        var id = Guid.NewGuid();
        _repo.Setup(r => r.GetByIdAsync(id, default)).ReturnsAsync(new CategoryRow(id, "X", null, 1));
        _repo.Setup(r => r.HasDependencyAsync(id, default)).ReturnsAsync(false);
        _repo.Setup(r => r.DeleteAsync(id, default)).ReturnsAsync(true);

        await CreateSut().HandleAsync(id);
        _repo.Verify(r => r.DeleteAsync(id, default), Times.Once);
    }
}
