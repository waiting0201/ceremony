using Ceremony.Application.Prepay;
using Ceremony.Application.Signups;
using FluentAssertions;
using Moq;

namespace Ceremony.Application.Tests.Prepay;

public sealed class GetBelieverLatestPrepayHandlerTests
{
    private readonly Mock<ISignupRepository> _repo = new();
    private GetBelieverLatestPrepayHandler Sut() => new(_repo.Object);

    [Fact]
    public async Task Returns_repo_result_when_latest_signup_has_prepay()
    {
        var believer = Guid.NewGuid();
        var cat = Guid.NewGuid();
        _repo.Setup(r => r.GetLatestPrepayByBelieverAsync(believer, 115, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BelieverLatestPrepayResult(114, cat, "梁皇寶懺"));

        var result = await Sut().HandleAsync(believer, 115);

        result.PrepayYear.Should().Be(114);
        result.PrepayCeremonyCategoryId.Should().Be(cat);
        result.PrepayCeremonyCategoryTitle.Should().Be("梁皇寶懺");
    }

    [Fact]
    public async Task Returns_all_null_when_no_signup_found()
    {
        _repo.Setup(r => r.GetLatestPrepayByBelieverAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((BelieverLatestPrepayResult?)null);

        var result = await Sut().HandleAsync(Guid.NewGuid(), 115);

        result.Should().NotBeNull();
        result.PrepayYear.Should().BeNull();
        result.PrepayCeremonyCategoryId.Should().BeNull();
        result.PrepayCeremonyCategoryTitle.Should().BeNull();
    }

    [Fact]
    public async Task Forwards_believerId_and_year_to_repo()
    {
        var believer = Guid.NewGuid();
        _repo.Setup(r => r.GetLatestPrepayByBelieverAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BelieverLatestPrepayResult(null, null, null));

        await Sut().HandleAsync(believer, 113);

        _repo.Verify(r => r.GetLatestPrepayByBelieverAsync(believer, 113, It.IsAny<CancellationToken>()), Times.Once);
    }
}
