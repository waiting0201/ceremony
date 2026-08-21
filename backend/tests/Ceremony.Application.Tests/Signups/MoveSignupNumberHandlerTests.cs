using Ceremony.Application.Signups;
using Ceremony.Domain.Exceptions;
using FluentAssertions;
using Moq;

namespace Ceremony.Application.Tests.Signups;

/// <summary>
/// MoveSignupNumberHandler 單元測試 — 驗證目標編號必填 / 必須 > 0、合法時只呼叫一次 MoveNumberAsync。
/// 實際的區間讓位 UPDATE、範圍檢查與 applock 由 integration test（真實 MSSQL）覆蓋。
/// </summary>
public sealed class MoveSignupNumberHandlerTests
{
    private readonly Mock<ISignupRepository> _signupRepo = new();

    private static readonly Guid AnySignupId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid AnyCategoryId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private MoveSignupNumberHandler CreateSut() => new(_signupRepo.Object);

    private static SignupListItem AnyView(Guid id, int number) => new(
        id, 115, AnyCategoryId, "春季", 1, "No", number, null, "非員工", null, "Alice",
        null, null, false, [null, null, null, null, null, null], [null, null, null, null, null, null],
        null, null, null, "台北市信義區市府路 1 號",
        null, null, null, "台北市信義區市府路 1 號",
        null, null, null, null, "alice", DateTime.UtcNow);

    [Fact]
    public async Task NullTargetNumber_throws_REQUIRED_verbatim()
    {
        var act = () => CreateSut().HandleAsync(AnySignupId, new MoveSignupNumberRequest(null));
        await act.Should().ThrowAsync<DomainException>()
            .Where(e => e.ErrorCode == "VALIDATION_REQUIRED" && e.Message == "請輸入目標編號");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public async Task NonPositiveTargetNumber_throws_INVALID(int target)
    {
        var act = () => CreateSut().HandleAsync(AnySignupId, new MoveSignupNumberRequest(target));
        await act.Should().ThrowAsync<DomainException>()
            .Where(e => e.ErrorCode == "VALIDATION_INVALID" && e.Message == "目標編號必須大於 0");
    }

    [Fact]
    public async Task Invalid_target_never_touches_repository()
    {
        var act = () => CreateSut().HandleAsync(AnySignupId, new MoveSignupNumberRequest(0));
        await act.Should().ThrowAsync<DomainException>();
        _signupRepo.Verify(r => r.MoveNumberAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Valid_target_calls_MoveNumberAsync_once_and_returns_reloaded_row()
    {
        _signupRepo.Setup(r => r.GetByIdAsync(AnySignupId, default)).ReturnsAsync(AnyView(AnySignupId, 2));

        var result = await CreateSut().HandleAsync(AnySignupId, new MoveSignupNumberRequest(2));

        result.Number.Should().Be(2);
        _signupRepo.Verify(r => r.MoveNumberAsync(AnySignupId, 2, default), Times.Once);
    }

    /// <summary>
    /// 移動不做編號重複檢查（同 insert-shift）：目標位置本來就被佔用，那正是要讓位的對象。
    /// </summary>
    [Fact]
    public async Task Does_not_check_number_conflict()
    {
        _signupRepo.Setup(r => r.GetByIdAsync(AnySignupId, default)).ReturnsAsync(AnyView(AnySignupId, 2));

        await CreateSut().HandleAsync(AnySignupId, new MoveSignupNumberRequest(2));

        _signupRepo.Verify(r => r.NumberExistsAsync(It.IsAny<int>(), It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        _signupRepo.Verify(r => r.NumberExistsExcludingAsync(It.IsAny<int>(), It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Reload_failure_throws_INTERNAL_ERROR()
    {
        _signupRepo.Setup(r => r.GetByIdAsync(AnySignupId, default)).ReturnsAsync((SignupListItem?)null);

        var act = () => CreateSut().HandleAsync(AnySignupId, new MoveSignupNumberRequest(2));
        await act.Should().ThrowAsync<DomainException>().Where(e => e.ErrorCode == "INTERNAL_ERROR");
    }
}
