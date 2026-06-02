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
        SignupSearchQuery? captured = null;
        _repo.Setup(r => r.SearchAsync(It.IsAny<SignupSearchQuery>(), default))
            .Callback<SignupSearchQuery, CancellationToken>((q, _) => captured = q)
            .ReturnsAsync([]);

        await CreateSut().HandleAsync(new SignupSearchQuery(CeremonyCategoryId: Guid.Empty));

        captured!.CeremonyCategoryId.Should().BeNull();
    }

    [Fact]
    public async Task SignupTypeMinusOne_normalized_to_null()
    {
        SignupSearchQuery? captured = null;
        _repo.Setup(r => r.SearchAsync(It.IsAny<SignupSearchQuery>(), default))
            .Callback<SignupSearchQuery, CancellationToken>((q, _) => captured = q)
            .ReturnsAsync([]);

        await CreateSut().HandleAsync(new SignupSearchQuery(SignupType: -1));

        captured!.SignupType.Should().BeNull();
    }

    [Fact]
    public async Task NumberZero_normalized_to_null()
    {
        SignupSearchQuery? captured = null;
        _repo.Setup(r => r.SearchAsync(It.IsAny<SignupSearchQuery>(), default))
            .Callback<SignupSearchQuery, CancellationToken>((q, _) => captured = q)
            .ReturnsAsync([]);

        await CreateSut().HandleAsync(new SignupSearchQuery(Number: 0));

        captured!.Number.Should().BeNull();
    }

    [Fact]
    public async Task SearchKey_trimmed_or_nulled()
    {
        SignupSearchQuery? captured = null;
        _repo.Setup(r => r.SearchAsync(It.IsAny<SignupSearchQuery>(), default))
            .Callback<SignupSearchQuery, CancellationToken>((q, _) => captured = q)
            .ReturnsAsync([]);

        await CreateSut().HandleAsync(new SignupSearchQuery(SearchKey: "  Alice  "));
        captured!.SearchKey.Should().Be("Alice");

        await CreateSut().HandleAsync(new SignupSearchQuery(SearchKey: "   "));
        captured!.SearchKey.Should().BeNull();
    }

    [Fact]
    public async Task NormalNumber_preserved()
    {
        SignupSearchQuery? captured = null;
        _repo.Setup(r => r.SearchAsync(It.IsAny<SignupSearchQuery>(), default))
            .Callback<SignupSearchQuery, CancellationToken>((q, _) => captured = q)
            .ReturnsAsync([]);

        await CreateSut().HandleAsync(new SignupSearchQuery(Number: 42, SignupType: 1, Year: 115, IsScope: true));

        captured!.Number.Should().Be(42);
        captured.SignupType.Should().Be(1);
        captured.Year.Should().Be(115);
        captured.IsScope.Should().BeTrue();
    }
}
