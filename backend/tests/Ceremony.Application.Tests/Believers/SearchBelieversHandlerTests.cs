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
    public async Task PhoneCriterion_is_converted_to_narrow_before_repo()
    {
        // 電話寫入端一律 ToNarrow，條件若留全形永遠撈不到（2026-08-06 使用者指定「電話一律半形」）
        _repo.Setup(r => r.SearchAsync(It.IsAny<BelieverSearchQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        await CreateSut().HandleAsync(new BelieverSearchQuery(Phone: " ０９１２－３４５ "));

        _repo.Verify(r => r.SearchAsync(
            It.Is<BelieverSearchQuery>(q => q.Phone == "0912-345"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task NameCriterion_keeps_fullwidth_characters()
    {
        // 只有電話轉半形；姓名/堂號的全形字是資料本身
        _repo.Setup(r => r.SearchAsync(It.IsAny<BelieverSearchQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        await CreateSut().HandleAsync(new BelieverSearchQuery(Name: "Ａ陳"));

        _repo.Verify(r => r.SearchAsync(
            It.Is<BelieverSearchQuery>(q => q.Name == "Ａ陳"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SearchKeyOnly_passes_validation_and_reaches_repo_trimmed()
    {
        // 新增報名的信眾搜尋只給單一關鍵字（對齊舊 NewSignupForm txtQ），不應被「請輸入搜尋條件」擋下
        _repo.Setup(r => r.SearchAsync(It.IsAny<BelieverSearchQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        await CreateSut().HandleAsync(new BelieverSearchQuery(SearchKey: "  陳  "));

        _repo.Verify(r => r.SearchAsync(
            It.Is<BelieverSearchQuery>(q => q.SearchKey == "陳" && q.Name == null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SearchKeyWhitespace_only_still_throws_VALIDATION_REQUIRED()
    {
        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            CreateSut().HandleAsync(new BelieverSearchQuery(SearchKey: "   ")));
        ex.ErrorCode.Should().Be("VALIDATION_REQUIRED");
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
