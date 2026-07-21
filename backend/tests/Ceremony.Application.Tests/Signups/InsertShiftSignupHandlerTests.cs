using Ceremony.Application.Auth;
using Ceremony.Application.Believers;
using Ceremony.Application.Signups;
using Ceremony.Domain.Exceptions;
using FluentAssertions;
using Moq;

namespace Ceremony.Application.Tests.Signups;

/// <summary>
/// InsertShiftSignupHandler 單元測試 — 驗證編號必填、不做重複檢查、走 InsertWithShiftAsync。
/// 實際的順移 UPDATE + applock 由 integration test（真實 MSSQL）覆蓋。
/// </summary>
public sealed class InsertShiftSignupHandlerTests
{
    private readonly Mock<ISignupRepository> _signupRepo = new();
    private readonly Mock<IBelieverRepository> _believerRepo = new();
    private readonly CallerContext _caller = new(AdminId: 1, AdminName: "alice");

    private static readonly Guid AnyBelieverId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid AnyCategoryId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private InsertShiftSignupHandler CreateSut() => new(_signupRepo.Object, _believerRepo.Object);

    private static CreateSignupRequest ValidReq() => new(
        Year: 115,
        CeremonyCategoryId: AnyCategoryId,
        SignupType: 1,
        BelieverId: AnyBelieverId,
        Name: "Alice",
        MailAddress: "台北市信義區市府路 1 號",
        KeepNumber: true,
        CustomNumber: 5);

    private static SignupListItem AnyView(Guid id) => new(
        id, 115, AnyCategoryId, "春季", 1, "No", 5, null, "非員工", null, "Alice",
        null, null, false, [null, null, null, null, null, null], [null, null, null, null, null, null],
        null, null, null, "台北市信義區市府路 1 號",
        null, null, null, "台北市信義區市府路 1 號",
        null, null, null, null, "alice", DateTime.UtcNow);

    [Fact]
    public async Task NullCustomNumber_throws_REQUIRED_verbatim()
    {
        var act = () => CreateSut().HandleAsync(ValidReq() with { CustomNumber = null }, _caller);
        await act.Should().ThrowAsync<DomainException>()
            .Where(e => e.ErrorCode == "VALIDATION_REQUIRED" && e.Message == "請輸入編號");
    }

    [Fact]
    public async Task NonPositiveCustomNumber_throws_REQUIRED()
    {
        var act = () => CreateSut().HandleAsync(ValidReq() with { CustomNumber = 0 }, _caller);
        await act.Should().ThrowAsync<DomainException>()
            .Where(e => e.ErrorCode == "VALIDATION_REQUIRED" && e.Message == "請輸入編號");
    }

    [Fact]
    public async Task EmptyName_throws_REQUIRED()
    {
        var act = () => CreateSut().HandleAsync(ValidReq() with { Name = "  " }, _caller);
        await act.Should().ThrowAsync<DomainException>()
            .Where(e => e.ErrorCode == "VALIDATION_REQUIRED" && e.Message == "請輸入姓名");
    }

    [Fact]
    public async Task UnknownBeliever_throws_BELIEVER_NOT_FOUND()
    {
        _believerRepo.Setup(r => r.GetNameAsync(AnyBelieverId, default)).ReturnsAsync((string?)null);

        var act = () => CreateSut().HandleAsync(ValidReq(), _caller);
        await act.Should().ThrowAsync<DomainException>()
            .Where(e => e.ErrorCode == "BELIEVER_NOT_FOUND");
    }

    [Fact]
    public async Task UnknownCategory_throws_CATEGORY_NOT_FOUND()
    {
        _believerRepo.Setup(r => r.GetNameAsync(AnyBelieverId, default)).ReturnsAsync("陳大明");
        _signupRepo.Setup(r => r.GetCeremonyCategoryTitleAsync(AnyCategoryId, default)).ReturnsAsync((string?)null);

        var act = () => CreateSut().HandleAsync(ValidReq(), _caller);
        await act.Should().ThrowAsync<DomainException>()
            .Where(e => e.ErrorCode == "CATEGORY_NOT_FOUND");
    }

    [Fact]
    public async Task Valid_callsInsertWithShift_withInsertNumber_and_skipsDuplicateCheck()
    {
        _believerRepo.Setup(r => r.GetNameAsync(AnyBelieverId, default)).ReturnsAsync("陳大明");
        _signupRepo.Setup(r => r.GetCeremonyCategoryTitleAsync(AnyCategoryId, default)).ReturnsAsync("春季");

        var capturedNumber = -1;
        SignupWriteModel? capturedSignup = null;
        _signupRepo
            .Setup(r => r.InsertWithShiftAsync(It.IsAny<SignupWriteModel>(), It.IsAny<SignupLogWriteModel>(), It.IsAny<int>(), default))
            .Callback<SignupWriteModel, SignupLogWriteModel, int, CancellationToken>((s, _, n, _) =>
            {
                capturedSignup = s;
                capturedNumber = n;
            });
        _signupRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), default))
            .ReturnsAsync((Guid id, CancellationToken _) => AnyView(id));

        var result = await CreateSut().HandleAsync(ValidReq() with { CustomNumber = 5 }, _caller);

        result.Should().NotBeNull();
        capturedNumber.Should().Be(5, because: "CustomNumber 即插入位置");
        capturedSignup!.NumberTitle.Should().Be("No");

        _signupRepo.Verify(r => r.InsertWithShiftAsync(It.IsAny<SignupWriteModel>(), It.IsAny<SignupLogWriteModel>(), 5, default), Times.Once);
        _signupRepo.Verify(r => r.NumberExistsAsync(It.IsAny<int>(), It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never,
            "插入模式刻意不做編號重複檢查");
        _signupRepo.Verify(r => r.InsertWithLogAsync(It.IsAny<SignupWriteModel>(), It.IsAny<SignupLogWriteModel>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Writes_per_signup_override_columns()
    {
        // per-signup 覆寫（2026-07-21）：堂號/員工類型/固定編號寫進 Signups write model。
        _believerRepo.Setup(r => r.GetNameAsync(AnyBelieverId, default)).ReturnsAsync("陳大明");
        _signupRepo.Setup(r => r.GetCeremonyCategoryTitleAsync(AnyCategoryId, default)).ReturnsAsync("春季");
        SignupWriteModel? capturedSignup = null;
        _signupRepo
            .Setup(r => r.InsertWithShiftAsync(It.IsAny<SignupWriteModel>(), It.IsAny<SignupLogWriteModel>(), It.IsAny<int>(), default))
            .Callback<SignupWriteModel, SignupLogWriteModel, int, CancellationToken>((s, _, _, _) => capturedSignup = s);
        _signupRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), default))
            .ReturnsAsync((Guid id, CancellationToken _) => AnyView(id));

        await CreateSut().HandleAsync(
            ValidReq() with { CustomNumber = 5, HallName = "慈光堂", EmployeeType = 3, IsFixedNumber = true },
            _caller);

        capturedSignup.Should().NotBeNull();
        capturedSignup!.HallName.Should().Be("慈光堂");
        capturedSignup.EmployeeType.Should().Be(3);
        capturedSignup.IsFixedNumber.Should().Be(true);
    }
}
