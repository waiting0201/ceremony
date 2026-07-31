using Ceremony.Application.Signups;
using FluentAssertions;
using Moq;

namespace Ceremony.Application.Tests.Signups;

/// <summary>
/// Handler 主要做 normalize；SQL 動態組合在 SignupRepository 內，由 integration test 覆蓋。
/// </summary>
public sealed class SearchSignupsHandlerTests
{
    private readonly Mock<ISignupRepository> _repo = new();
    private SearchSignupsHandler CreateSut() => new(_repo.Object);

    [Fact]
    public async Task GuidEmpty_normalized_to_null()
    {
        var captured = await CaptureAsync(new SignupSearchQuery(CeremonyCategoryId: Guid.Empty));

        captured.CeremonyCategoryId.Should().BeNull();
    }

    [Fact]
    public async Task SignupTypeMinusOne_normalized_to_null()
    {
        var captured = await CaptureAsync(new SignupSearchQuery(SignupType: -1));

        captured.SignupType.Should().BeNull();
    }

    [Fact]
    public async Task NumberZero_normalized_to_null()
    {
        var captured = await CaptureAsync(new SignupSearchQuery(NumberStart: 0, NumberEnd: 0));

        captured.NumberStart.Should().BeNull();
        captured.NumberEnd.Should().BeNull();
    }

    [Fact]
    public async Task NumberStartOnly_mirrors_to_end()
    {
        var captured = await CaptureAsync(new SignupSearchQuery(NumberStart: 42));

        captured.NumberStart.Should().Be(42);
        captured.NumberEnd.Should().Be(42, because: "只填起＝只查那一筆編號");
    }

    [Fact]
    public async Task NumberEndOnly_mirrors_to_start()
    {
        var captured = await CaptureAsync(new SignupSearchQuery(NumberEnd: 42));

        captured.NumberStart.Should().Be(42, because: "只填迄＝只查那一筆編號");
        captured.NumberEnd.Should().Be(42);
    }

    [Fact]
    public async Task NumberRange_preserved()
    {
        var captured = await CaptureAsync(new SignupSearchQuery(NumberStart: 10, NumberEnd: 20));

        captured.NumberStart.Should().Be(10);
        captured.NumberEnd.Should().Be(20);
    }

    [Fact]
    public async Task SearchKey_trimmed_or_nulled()
    {
        (await CaptureAsync(new SignupSearchQuery(SearchKey: "  Alice  "))).SearchKey.Should().Be("Alice");
        (await CaptureAsync(new SignupSearchQuery(SearchKey: "   "))).SearchKey.Should().BeNull();
    }

    [Fact]
    public async Task OtherConditions_preserved()
    {
        var captured = await CaptureAsync(
            new SignupSearchQuery(NumberStart: 42, SignupType: 1, Year: 115, IsScope: true));

        captured.NumberStart.Should().Be(42);
        captured.SignupType.Should().Be(1);
        captured.Year.Should().Be(115);
        captured.IsScope.Should().BeTrue();
    }

    /// <summary>跑一次 handler，回傳實際送進 repository 的（已 normalize 的）查詢條件。</summary>
    private async Task<SignupSearchQuery> CaptureAsync(SignupSearchQuery query)
    {
        SignupSearchQuery? captured = null;
        _repo.Setup(r => r.SearchAsync(It.IsAny<SignupSearchQuery>(), default))
            .Callback<SignupSearchQuery, CancellationToken>((q, _) => captured = q)
            .ReturnsAsync([]);

        await CreateSut().HandleAsync(query);

        return captured!;
    }
}
