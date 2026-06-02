using Ceremony.Application.Believers;
using Ceremony.Domain.Exceptions;
using FluentAssertions;
using Moq;

namespace Ceremony.Application.Tests.Believers;

/// <summary>
/// SearchBelieversHandler 單元測試 — 對照 [get-believers.md 邊界 case 表](../../../../docs/blueprints/api-endpoints/get-believers.md)。
/// </summary>
public sealed class SearchBelieversHandlerTests
{
    private readonly Mock<IBelieverRepository> _repo = new();

    private SearchBelieversHandler CreateSut() => new(_repo.Object);

    [Fact]
    public async Task AllCriteriaEmpty_throws_VALIDATION_REQUIRED_with_verbatim_message()
    {
        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            CreateSut().HandleAsync(new BelieverSearchQuery()));
        ex.ErrorCode.Should().Be("VALIDATION_REQUIRED");
        ex.Message.Should().Be("請輸入搜尋條件");
    }

    [Fact]
    public async Task AllCriteriaWhitespace_throws_VALIDATION_REQUIRED()
    {
        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            CreateSut().HandleAsync(new BelieverSearchQuery(" ", "  ", "\t", "", null)));
        ex.ErrorCode.Should().Be("VALIDATION_REQUIRED");
    }

    [Fact]
    public async Task OneCriterion_calls_repo_with_trimmed_value()
    {
        _repo.Setup(r => r.SearchAsync(It.IsAny<BelieverSearchQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        await CreateSut().HandleAsync(new BelieverSearchQuery(Name: "  John  "));

        _repo.Verify(r => r.SearchAsync(
            It.Is<BelieverSearchQuery>(q => q.Name == "John" && q.Phone == null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task NoResults_returns_empty_response_with_total_0()
    {
        _repo.Setup(r => r.SearchAsync(It.IsAny<BelieverSearchQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var result = await CreateSut().HandleAsync(new BelieverSearchQuery(Name: "ghost"));

        result.Items.Should().BeEmpty();
        result.Total.Should().Be(0);
    }

    [Fact]
    public async Task WithResults_returns_items_and_correct_total()
    {
        var item = new BelieverListItem(
            Guid.NewGuid(), 1, "非員工", "堂號", "Alice", "0912345678", false,
            null, null, null, null, null, null, null, null,
            ["A", null, null, null, null, null],
            [null, null, null, null, null, null]);

        _repo.Setup(r => r.SearchAsync(It.IsAny<BelieverSearchQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([item]);

        var result = await CreateSut().HandleAsync(new BelieverSearchQuery(Name: "Alice"));

        result.Items.Should().HaveCount(1);
        result.Total.Should().Be(1);
        result.Items[0].Name.Should().Be("Alice");
    }
}
