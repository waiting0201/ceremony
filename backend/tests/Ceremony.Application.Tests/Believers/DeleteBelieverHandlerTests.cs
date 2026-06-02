using Ceremony.Application.Believers;
using Ceremony.Domain.Exceptions;
using FluentAssertions;
using Moq;

namespace Ceremony.Application.Tests.Believers;

public sealed class DeleteBelieverHandlerTests
{
    private readonly Mock<IBelieverRepository> _repo = new();
    private DeleteBelieverHandler CreateSut() => new(_repo.Object);

    [Fact]
    public async Task NotFound_throws_BELIEVER_NOT_FOUND()
    {
        _repo.Setup(r => r.GetNameAsync(It.IsAny<Guid>(), default)).ReturnsAsync((string?)null);

        var act = () => CreateSut().HandleAsync(Guid.NewGuid());
        await act.Should().ThrowAsync<DomainException>()
            .Where(e => e.ErrorCode == "BELIEVER_NOT_FOUND");
        _repo.Verify(r => r.DeleteAsync(It.IsAny<Guid>(), default), Times.Never);
    }

    [Fact]
    public async Task HasSignups_throws_BELIEVER_HAS_SIGNUPS_with_name_in_message()
    {
        var id = Guid.NewGuid();
        _repo.Setup(r => r.GetNameAsync(id, default)).ReturnsAsync("陳大明");
        _repo.Setup(r => r.HasSignupsAsync(id, default)).ReturnsAsync(true);

        var ex = await Assert.ThrowsAsync<DomainException>(() => CreateSut().HandleAsync(id));
        ex.ErrorCode.Should().Be("BELIEVER_HAS_SIGNUPS");
        ex.Message.Should().Be("陳大明 已有報名資料，不能刪除！");
        _repo.Verify(r => r.DeleteAsync(It.IsAny<Guid>(), default), Times.Never);
    }

    [Fact]
    public async Task Found_NoSignups_deletes()
    {
        var id = Guid.NewGuid();
        _repo.Setup(r => r.GetNameAsync(id, default)).ReturnsAsync("Alice");
        _repo.Setup(r => r.HasSignupsAsync(id, default)).ReturnsAsync(false);
        _repo.Setup(r => r.DeleteAsync(id, default)).ReturnsAsync(true);

        await CreateSut().HandleAsync(id);
        _repo.Verify(r => r.DeleteAsync(id, default), Times.Once);
    }
}
