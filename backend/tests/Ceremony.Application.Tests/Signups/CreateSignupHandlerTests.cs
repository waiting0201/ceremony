using Ceremony.Application.Auth;
using Ceremony.Application.Believers;
using Ceremony.Application.Signups;
using Ceremony.Domain.Exceptions;
using FluentAssertions;
using Moq;

namespace Ceremony.Application.Tests.Signups;

/// <summary>
/// CreateSignupHandler 單元測試 — 驗證 path + normalize；UPDLOCK 行為由 integration test 覆蓋。
/// </summary>
public sealed class CreateSignupHandlerTests
{
    private readonly Mock<ISignupRepository> _signupRepo = new();
    private readonly Mock<IBelieverRepository> _believerRepo = new();
    private readonly CallerContext _caller = new(AdminId: 1, AdminName: "alice");

    private static readonly Guid AnyBelieverId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid AnyCategoryId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private CreateSignupHandler CreateSut() => new(_signupRepo.Object, _believerRepo.Object);

    private static CreateSignupRequest ValidReq() => new(
        Year: 115,
        CeremonyCategoryId: AnyCategoryId,
        SignupType: 1,
        BelieverId: AnyBelieverId,
        Name: "Alice",
        MailAddress: "台北市信義區市府路 1 號");

    [Fact]
    public async Task InvalidSignupType_throws_VALIDATION_INVALID_from_NumberTitleResolver()
    {
        var act = () => CreateSut().HandleAsync(ValidReq() with { SignupType = 99 }, _caller);
        await act.Should().ThrowAsync<DomainException>()
            .Where(e => e.ErrorCode == "VALIDATION_INVALID" && e.Message == "報名類型錯誤");
    }

    [Fact]
    public async Task EmptyName_throws_VALIDATION_REQUIRED()
    {
        var act = () => CreateSut().HandleAsync(ValidReq() with { Name = "" }, _caller);
        await act.Should().ThrowAsync<DomainException>()
            .Where(e => e.ErrorCode == "VALIDATION_REQUIRED" && e.Message == "請輸入姓名");
    }

    [Fact]
    public async Task EmptyMailAddress_allowed_and_stored_as_empty_string()
    {
        // 地址非必填（2026-07-21 使用者指定）：空白地址不再擋下，normalize 為空字串後照常寫入。
        _believerRepo.Setup(r => r.GetNameAsync(AnyBelieverId, default)).ReturnsAsync("陳大明");
        _signupRepo.Setup(r => r.GetCeremonyCategoryTitleAsync(AnyCategoryId, default)).ReturnsAsync("春季");

        var capturedSignup = (SignupWriteModel?)null;
        _signupRepo
            .Setup(r => r.InsertWithLogAsync(It.IsAny<SignupWriteModel>(), It.IsAny<SignupLogWriteModel>(), null, default))
            .Callback<SignupWriteModel, SignupLogWriteModel, int?, CancellationToken>((s, _, _, _) => capturedSignup = s);
        _signupRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), default))
            .ReturnsAsync((Guid id, CancellationToken _) => new SignupListItem(
                id, 115, AnyCategoryId, "春季", 1, "No", 1, null, "非員工", null, "Alice",
                null, null, false, [null, null, null, null, null, null], [null, null, null, null, null, null],
                null, null, null, "",
                null, null, null, "",
                null, null, null, null, "alice", DateTime.UtcNow));

        var result = await CreateSut().HandleAsync(ValidReq() with { MailAddress = "  " }, _caller);

        result.Should().NotBeNull();
        capturedSignup.Should().NotBeNull();
        capturedSignup!.MailAddress.Should().Be("");
    }

    [Fact]
    public async Task KeepNumber_emptyCustom_throws_REQUIRED_verbatim()
    {
        var act = () => CreateSut().HandleAsync(ValidReq() with { KeepNumber = true, CustomNumber = null }, _caller);
        await act.Should().ThrowAsync<DomainException>()
            .Where(e => e.ErrorCode == "VALIDATION_REQUIRED" && e.Message == "請輸入編號");
    }

    [Fact]
    public async Task KeepNumber_duplicate_throws_CONFLICT_with_full_message()
    {
        _signupRepo.Setup(r => r.NumberExistsAsync(115, AnyCategoryId, 1, 42, default)).ReturnsAsync(true);
        _signupRepo.Setup(r => r.GetCeremonyCategoryTitleAsync(AnyCategoryId, default)).ReturnsAsync("春季");

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            CreateSut().HandleAsync(ValidReq() with { KeepNumber = true, CustomNumber = 42 }, _caller));
        ex.ErrorCode.Should().Be("SIGNUP_NUMBER_CONFLICT");
        ex.Message.Should().Be("115 春季 一般 編號重複，請重新確認！");
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
    public async Task ValidRequest_inserts_with_normalized_data_and_returns_view()
    {
        _believerRepo.Setup(r => r.GetNameAsync(AnyBelieverId, default)).ReturnsAsync("陳大明");
        _signupRepo.Setup(r => r.GetCeremonyCategoryTitleAsync(AnyCategoryId, default)).ReturnsAsync("春季");

        var capturedSignup = (SignupWriteModel?)null;
        var capturedLog = (SignupLogWriteModel?)null;
        _signupRepo
            .Setup(r => r.InsertWithLogAsync(It.IsAny<SignupWriteModel>(), It.IsAny<SignupLogWriteModel>(), null, default))
            .Callback<SignupWriteModel, SignupLogWriteModel, int?, CancellationToken>((s, l, _, _) =>
            {
                capturedSignup = s;
                capturedLog = l;
            });
        _signupRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), default))
            .ReturnsAsync((Guid id, CancellationToken _) => new SignupListItem(
                id, 115, AnyCategoryId, "春季", 1, "No", 1, null, "非員工", null, "Alice",
                null, null, false, [null, null, null, null, null, null], [null, null, null, null, null, null],
                null, null, null, "台北市信義區市府路 1 號",
                null, null, null, "台北市信義區市府路 1 號",
                null, null, null, null, "alice", DateTime.UtcNow));

        var req = ValidReq() with
        {
            Phone = "０９１２", // 全形 → 應轉半形
            TextAddress = "", // 空 → fallback 至 mailAddress
            MailZipcodeId = 155,
            LivingNames = ["陽上一", "", null, "  ", "陽上五", ""],
        };
        var result = await CreateSut().HandleAsync(req, _caller);

        result.Should().NotBeNull();
        capturedSignup.Should().NotBeNull();
        capturedSignup!.NumberTitle.Should().Be("No");
        capturedSignup.Phone.Should().Be("0912", because: "全形數字應轉半形");
        capturedSignup.TextAddress.Should().Be("台北市信義區市府路 1 號", because: "空 textAddress 應 fallback 至 mailAddress");
        capturedSignup.LivingNames.Should().Equal(["陽上一", null, null, null, "陽上五", null], because: "空值或空白應 normalize 為 null");

        capturedLog!.CeremonyCategoryTitle.Should().Be("春季");
        capturedLog.Admin.Should().Be("alice");
    }

    [Fact]
    public async Task Names_preserve_leading_fullwidth_space_for_layout()
    {
        // 使用者用開頭全形空格（U+3000）把名字往下推作直書排版 → 寫入端不可 trim 掉（純空白才轉 null）
        _believerRepo.Setup(r => r.GetNameAsync(AnyBelieverId, default)).ReturnsAsync("陳大明");
        _signupRepo.Setup(r => r.GetCeremonyCategoryTitleAsync(AnyCategoryId, default)).ReturnsAsync("春季");

        var capturedSignup = (SignupWriteModel?)null;
        _signupRepo
            .Setup(r => r.InsertWithLogAsync(It.IsAny<SignupWriteModel>(), It.IsAny<SignupLogWriteModel>(), null, default))
            .Callback<SignupWriteModel, SignupLogWriteModel, int?, CancellationToken>((s, _, _, _) => capturedSignup = s);
        _signupRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), default))
            .ReturnsAsync((Guid id, CancellationToken _) => new SignupListItem(
                id, 115, AnyCategoryId, "春季", 1, "No", 1, null, "非員工", null, "Alice",
                null, null, false, [null, null, null, null, null, null], [null, null, null, null, null, null],
                null, null, null, "台北市信義區市府路 1 號",
                null, null, null, "台北市信義區市府路 1 號",
                null, null, null, null, "alice", DateTime.UtcNow));

        var req = ValidReq() with
        {
            DeadNames = ["蔡姓歷代祖先", "　蔡炎城", "　蔡黃龎", "蔡貴仁", "　", null],
        };
        await CreateSut().HandleAsync(req, _caller);

        capturedSignup.Should().NotBeNull();
        capturedSignup!.DeadNames.Should().Equal(
            ["蔡姓歷代祖先", "　蔡炎城", "　蔡黃龎", "蔡貴仁", null, null],
            because: "開頭全形空格須保留（排版用）；純空白「　」才 normalize 為 null");
    }

    [Fact]
    public async Task TextZipcodeId_null_and_emptyText_fallbacks_to_mailZipcode()
    {
        _believerRepo.Setup(r => r.GetNameAsync(AnyBelieverId, default)).ReturnsAsync("X");
        _signupRepo.Setup(r => r.GetCeremonyCategoryTitleAsync(AnyCategoryId, default)).ReturnsAsync("春季");
        SignupWriteModel? captured = null;
        _signupRepo
            .Setup(r => r.InsertWithLogAsync(It.IsAny<SignupWriteModel>(), It.IsAny<SignupLogWriteModel>(), null, default))
            .Callback<SignupWriteModel, SignupLogWriteModel, int?, CancellationToken>((s, _, _, _) => captured = s);
        _signupRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), default))
            .ReturnsAsync(new SignupListItem(
                Guid.NewGuid(), 115, AnyCategoryId, "春季", 1, "No", 1, null, null, null, "X",
                null, null, false, [null, null, null, null, null, null], [null, null, null, null, null, null],
                null, null, null, "addr",
                null, null, null, "addr",
                null, null, null, null, "alice", DateTime.UtcNow));

        await CreateSut().HandleAsync(ValidReq() with { MailZipcodeId = 155, TextZipcodeId = null, TextAddress = null }, _caller);

        captured!.TextZipcodeId.Should().Be(155);
    }
}
