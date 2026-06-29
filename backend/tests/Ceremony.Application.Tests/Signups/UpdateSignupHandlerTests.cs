using Ceremony.Application.Auth;
using Ceremony.Application.Believers;
using Ceremony.Application.Signups;
using Ceremony.Domain.Exceptions;
using FluentAssertions;
using Moq;

namespace Ceremony.Application.Tests.Signups;

/// <summary>
/// UpdateSignupHandler 單元測試 — 重點：編輯報名「不回寫 Believer」。
/// 堂號/員工類型/固定編號為信眾層級屬性，僅於信眾維護頁修改；報名編輯回寫會連動同信眾全部報名
/// （legacy EditSignupForm 缺陷）。見 docs/blueprints/signup-hallname-isolation.md
/// </summary>
public sealed class UpdateSignupHandlerTests
{
    private readonly Mock<ISignupRepository> _signupRepo = new();
    private readonly Mock<IBelieverRepository> _believerRepo = new();
    private readonly CallerContext _caller = new(AdminId: 1, AdminName: "alice");

    private static readonly Guid AnySignupId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid AnyBelieverId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid AnyCategoryId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private UpdateSignupHandler CreateSut() => new(_signupRepo.Object, _believerRepo.Object);

    private static CreateSignupRequest ValidReq() => new(
        Year: 115,
        CeremonyCategoryId: AnyCategoryId,
        SignupType: 1,
        BelieverId: AnyBelieverId,
        Name: "Alice",
        MailAddress: "台北市信義區市府路 1 號",
        CustomNumber: 7,
        HallName: "慈光堂");

    private void SetupHappyPath()
    {
        _believerRepo.Setup(r => r.GetNameAsync(AnyBelieverId, default)).ReturnsAsync("陳大明");
        _signupRepo.Setup(r => r.NumberExistsExcludingAsync(115, AnyCategoryId, 1, 7, AnySignupId, default))
            .ReturnsAsync(false);
        _signupRepo.Setup(r => r.GetCeremonyCategoryTitleAsync(AnyCategoryId, default)).ReturnsAsync("春季");
        _signupRepo.Setup(r => r.UpdateWithLogAsync(
                It.IsAny<SignupWriteModel>(), It.IsAny<SignupLogWriteModel>(), It.IsAny<int>(), default))
            .ReturnsAsync(true);
        _signupRepo.Setup(r => r.GetByIdAsync(AnySignupId, default))
            .ReturnsAsync(new SignupListItem(
                AnySignupId, 115, AnyCategoryId, "春季", 1, "No", 7, null, "非員工", AnyBelieverId, "Alice",
                "慈光堂", null, false, [null, null, null, null, null, null], [null, null, null, null, null, null],
                null, null, null, "台北市信義區市府路 1 號",
                null, null, null, "台北市信義區市府路 1 號",
                null, null, null, null, "alice", DateTime.UtcNow));
    }

    [Fact]
    public async Task Edit_never_writes_back_to_Believer()
    {
        // 核心回歸測試：改報名（含改堂號）不可呼叫任何 Believer 寫入方法。
        SetupHappyPath();

        await CreateSut().HandleAsync(AnySignupId, ValidReq() with { HallName = "新堂號" }, _caller);

        _believerRepo.Verify(r => r.UpdateAsync(It.IsAny<Guid>(), It.IsAny<BelieverWriteModel>(), It.IsAny<CancellationToken>()), Times.Never);
        _believerRepo.Verify(r => r.InsertAsync(It.IsAny<BelieverWriteModel>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Edit_records_hallName_in_log_snapshot_only()
    {
        // 堂號仍寫入 SignupLog 快照（audit 記當下值），但只進 log、不進 Believer。
        SetupHappyPath();
        SignupLogWriteModel? capturedLog = null;
        _signupRepo.Setup(r => r.UpdateWithLogAsync(
                It.IsAny<SignupWriteModel>(), It.IsAny<SignupLogWriteModel>(), It.IsAny<int>(), default))
            .Callback<SignupWriteModel, SignupLogWriteModel, int, CancellationToken>((_, l, _, _) => capturedLog = l)
            .ReturnsAsync(true);

        await CreateSut().HandleAsync(AnySignupId, ValidReq() with { HallName = "慈光堂" }, _caller);

        capturedLog.Should().NotBeNull();
        capturedLog!.HallName.Should().Be("慈光堂");
    }

    [Fact]
    public async Task MissingNumber_throws_VALIDATION_REQUIRED()
    {
        _believerRepo.Setup(r => r.GetNameAsync(AnyBelieverId, default)).ReturnsAsync("陳大明");

        var act = () => CreateSut().HandleAsync(AnySignupId, ValidReq() with { CustomNumber = null }, _caller);
        await act.Should().ThrowAsync<DomainException>()
            .Where(e => e.ErrorCode == "VALIDATION_REQUIRED" && e.Message == "請輸入編號");
    }

    [Fact]
    public async Task DuplicateNumber_throws_CONFLICT()
    {
        _believerRepo.Setup(r => r.GetNameAsync(AnyBelieverId, default)).ReturnsAsync("陳大明");
        _signupRepo.Setup(r => r.NumberExistsExcludingAsync(115, AnyCategoryId, 1, 7, AnySignupId, default))
            .ReturnsAsync(true);

        var act = () => CreateSut().HandleAsync(AnySignupId, ValidReq(), _caller);
        await act.Should().ThrowAsync<DomainException>()
            .Where(e => e.ErrorCode == "SIGNUP_NUMBER_CONFLICT");
    }

    [Fact]
    public async Task UnknownBeliever_throws_BELIEVER_NOT_FOUND()
    {
        _believerRepo.Setup(r => r.GetNameAsync(AnyBelieverId, default)).ReturnsAsync((string?)null);
        _signupRepo.Setup(r => r.NumberExistsExcludingAsync(115, AnyCategoryId, 1, 7, AnySignupId, default))
            .ReturnsAsync(false);

        var act = () => CreateSut().HandleAsync(AnySignupId, ValidReq(), _caller);
        await act.Should().ThrowAsync<DomainException>()
            .Where(e => e.ErrorCode == "BELIEVER_NOT_FOUND");
    }

    [Fact]
    public async Task SignupNotFound_throws_SIGNUP_NOT_FOUND()
    {
        SetupHappyPath();
        _signupRepo.Setup(r => r.UpdateWithLogAsync(
                It.IsAny<SignupWriteModel>(), It.IsAny<SignupLogWriteModel>(), It.IsAny<int>(), default))
            .ReturnsAsync(false);

        var act = () => CreateSut().HandleAsync(AnySignupId, ValidReq(), _caller);
        await act.Should().ThrowAsync<DomainException>()
            .Where(e => e.ErrorCode == "SIGNUP_NOT_FOUND");
    }
}
