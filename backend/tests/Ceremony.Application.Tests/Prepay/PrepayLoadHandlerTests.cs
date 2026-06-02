using Ceremony.Application.Prepay;
using Ceremony.Application.Signups;
using Ceremony.Domain.Exceptions;
using FluentAssertions;
using Moq;

namespace Ceremony.Application.Tests.Prepay;

public sealed class PrepayLoadHandlerTests
{
    private readonly Mock<IPrepayRepository> _repo = new();
    private readonly CallerContext _caller = new(AdminId: 1, AdminName: "alice");

    private static readonly Guid TargetCat = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid SourceCat = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private PrepayLoadHandler CreateSut() => new(_repo.Object);

    private static PrepayLoadRequest Req(int group = 1) => new(
        SourceYear: 114, SourceCeremonyId: SourceCat,
        TargetYear: 115, TargetCeremonyId: TargetCat,
        BelieverGroup: group);

    private static PrepaySourceRow Src(Guid? bid = null, int? num = null, bool fixedNum = false,
        int signupType = 1, int? prepayYear = null, Guid? prepayCat = null, int? prepaySort = null,
        string? prepayTitle = null) =>
        new(
            SignupId: Guid.NewGuid(),
            BelieverId: bid ?? Guid.NewGuid(),
            SignupType: signupType,
            NumberTitle: "No", Number: num, Fee: 100, Name: "X", Phone: null,
            LivingNames: [null, null, null, null, null, null],
            DeadNames: [null, null, null, null, null, null],
            MailZipcodeId: null, MailZipcode: null, MailAddress: "a",
            TextZipcodeId: null, TextZipcode: null, TextAddress: null,
            Remark: null,
            PrepayYear: prepayYear, PrepayCeremonyCategoryId: prepayCat,
            PrepayCeremonySort: prepaySort, PrepayCeremonyTitle: prepayTitle,
            IsFixedNumber: fixedNum, EmployeeType: 1);

    [Fact]
    public async Task TargetYearZero_throws_REQUIRED_verbatim()
    {
        var act = () => CreateSut().HandleAsync(Req() with { TargetYear = 0 }, _caller);
        await act.Should().ThrowAsync<DomainException>()
            .Where(e => e.ErrorCode == "VALIDATION_REQUIRED" && e.Message == "請選擇年份");
    }

    [Fact]
    public async Task TargetCeremonyEmpty_throws_REQUIRED_verbatim()
    {
        var act = () => CreateSut().HandleAsync(Req() with { TargetCeremonyId = Guid.Empty }, _caller);
        await act.Should().ThrowAsync<DomainException>()
            .Where(e => e.ErrorCode == "VALIDATION_REQUIRED" && e.Message == "請選擇法會");
    }

    [Fact]
    public async Task InvalidGroup_throws_INVALID()
    {
        var act = () => CreateSut().HandleAsync(Req(group: 99), _caller);
        await act.Should().ThrowAsync<DomainException>()
            .Where(e => e.ErrorCode == "VALIDATION_INVALID");
    }

    [Fact]
    public async Task CategoryNotFound_throws()
    {
        _repo.Setup(r => r.GetCeremonyCategoryAsync(TargetCat, default)).ReturnsAsync(((string, int)?)null);

        var act = () => CreateSut().HandleAsync(Req(), _caller);
        await act.Should().ThrowAsync<DomainException>()
            .Where(e => e.ErrorCode == "CATEGORY_NOT_FOUND");
    }

    [Fact]
    public async Task NoSources_returns_zero_loaded()
    {
        _repo.Setup(r => r.GetCeremonyCategoryAsync(TargetCat, default)).ReturnsAsync(("春季", 1));
        _repo.Setup(r => r.GetMaxNumberAsync(115, TargetCat, 1, default)).ReturnsAsync(0);
        _repo.Setup(r => r.GetPrepaySourcesAsync(114, SourceCat, 1, 1, 115, 1, default))
            .ReturnsAsync([]);

        var result = await CreateSut().HandleAsync(Req(), _caller);
        result.Loaded.Should().Be(0);
        result.Skipped.Should().Be(0);
    }

    [Fact]
    public async Task SingleNonFixed_allocates_maxPlus1()
    {
        _repo.Setup(r => r.GetCeremonyCategoryAsync(TargetCat, default)).ReturnsAsync(("春季", 1));
        _repo.Setup(r => r.GetMaxNumberAsync(115, TargetCat, 1, default)).ReturnsAsync(50);
        var src = Src(num: 10, fixedNum: false, prepayYear: 115);
        _repo.Setup(r => r.GetPrepaySourcesAsync(114, SourceCat, 1, 1, 115, 1, default))
            .ReturnsAsync([src]);
        _repo.Setup(r => r.SignupExistsAsync(115, TargetCat, 1, src.BelieverId, default)).ReturnsAsync(false);

        IReadOnlyList<(SignupWriteModel, SignupLogWriteModel, int)>? captured = null;
        _repo.Setup(r => r.InsertBatchAsync(It.IsAny<IReadOnlyList<(SignupWriteModel, SignupLogWriteModel, int)>>(), default))
            .Callback<IReadOnlyList<(SignupWriteModel, SignupLogWriteModel, int)>, CancellationToken>((b, _) => captured = b);

        var result = await CreateSut().HandleAsync(Req(), _caller);

        result.Loaded.Should().Be(1);
        result.Details.NonFixedLoaded.Should().Be(1);
        captured!.Should().HaveCount(1);
        captured[0].Item3.Should().Be(51, because: "max=50, next allocated = 51");
    }

    [Fact]
    public async Task SingleFixed_preserves_number()
    {
        _repo.Setup(r => r.GetCeremonyCategoryAsync(TargetCat, default)).ReturnsAsync(("春季", 1));
        _repo.Setup(r => r.GetMaxNumberAsync(115, TargetCat, 1, default)).ReturnsAsync(0);
        var src = Src(num: 7, fixedNum: true, prepayYear: 115);
        _repo.Setup(r => r.GetPrepaySourcesAsync(114, SourceCat, 1, 1, 115, 1, default))
            .ReturnsAsync([src]);
        _repo.Setup(r => r.SignupExistsAsync(115, TargetCat, 1, src.BelieverId, default)).ReturnsAsync(false);

        IReadOnlyList<(SignupWriteModel, SignupLogWriteModel, int)>? captured = null;
        _repo.Setup(r => r.InsertBatchAsync(It.IsAny<IReadOnlyList<(SignupWriteModel, SignupLogWriteModel, int)>>(), default))
            .Callback<IReadOnlyList<(SignupWriteModel, SignupLogWriteModel, int)>, CancellationToken>((b, _) => captured = b);

        var result = await CreateSut().HandleAsync(Req(), _caller);

        result.Details.FixedLoaded.Should().Be(1);
        captured![0].Item3.Should().Be(7, because: "fixed number preserved");
    }

    [Fact]
    public async Task FixedGap_filledBy_nonFixed()
    {
        // fixed numbers: 5, 8 → gaps [1,2,3,4,6,7]
        // non-fixed: 3 個 → 應拿到 1, 2, 3
        _repo.Setup(r => r.GetCeremonyCategoryAsync(TargetCat, default)).ReturnsAsync(("春季", 1));
        _repo.Setup(r => r.GetMaxNumberAsync(115, TargetCat, 1, default)).ReturnsAsync(0);

        var fixed5 = Src(num: 5, fixedNum: true, prepayYear: 115);
        var fixed8 = Src(num: 8, fixedNum: true, prepayYear: 115);
        var nf1 = Src(fixedNum: false, prepayYear: 115);
        var nf2 = Src(fixedNum: false, prepayYear: 115);
        var nf3 = Src(fixedNum: false, prepayYear: 115);
        _repo.Setup(r => r.GetPrepaySourcesAsync(114, SourceCat, 1, 1, 115, 1, default))
            .ReturnsAsync([fixed5, fixed8, nf1, nf2, nf3]);
        _repo.Setup(r => r.SignupExistsAsync(115, TargetCat, 1, It.IsAny<Guid>(), default)).ReturnsAsync(false);

        IReadOnlyList<(SignupWriteModel, SignupLogWriteModel, int)>? captured = null;
        _repo.Setup(r => r.InsertBatchAsync(It.IsAny<IReadOnlyList<(SignupWriteModel, SignupLogWriteModel, int)>>(), default))
            .Callback<IReadOnlyList<(SignupWriteModel, SignupLogWriteModel, int)>, CancellationToken>((b, _) => captured = b);

        var result = await CreateSut().HandleAsync(Req(), _caller);

        result.Details.FixedLoaded.Should().Be(2);
        result.Details.NonFixedLoaded.Should().Be(3);
        captured!.Should().HaveCount(5);
        var numbersInOrder = captured.Select(b => b.Item3).ToList();
        numbersInOrder.Should().BeEquivalentTo(new[] { 5, 8, 1, 2, 3 }, opts => opts.WithStrictOrdering(),
            because: "fixed 先寫入後保留編號，non-fixed 從 gaps [1,2,3,4,6,7] 中取前 3 個");
    }

    [Fact]
    public async Task AlreadyLoaded_signups_are_skipped()
    {
        _repo.Setup(r => r.GetCeremonyCategoryAsync(TargetCat, default)).ReturnsAsync(("春季", 1));
        _repo.Setup(r => r.GetMaxNumberAsync(115, TargetCat, 1, default)).ReturnsAsync(0);

        var bid = Guid.NewGuid();
        var src = Src(bid: bid, fixedNum: false, prepayYear: 115);
        _repo.Setup(r => r.GetPrepaySourcesAsync(114, SourceCat, 1, 1, 115, 1, default))
            .ReturnsAsync([src]);
        _repo.Setup(r => r.SignupExistsAsync(115, TargetCat, 1, bid, default)).ReturnsAsync(true);

        var result = await CreateSut().HandleAsync(Req(), _caller);

        result.Loaded.Should().Be(0);
        result.Skipped.Should().Be(1);
        _repo.Verify(r => r.InsertBatchAsync(It.IsAny<IReadOnlyList<(SignupWriteModel, SignupLogWriteModel, int)>>(), default),
            Times.Never, "no inserts when all skipped");
    }

    [Fact]
    public async Task FuturePrepayYear_carriesForward()
    {
        _repo.Setup(r => r.GetCeremonyCategoryAsync(TargetCat, default)).ReturnsAsync(("春季", 1));
        _repo.Setup(r => r.GetMaxNumberAsync(115, TargetCat, 1, default)).ReturnsAsync(0);

        var futurePrepayCat = Guid.NewGuid();
        var src = Src(fixedNum: false, prepayYear: 117, prepayCat: futurePrepayCat);  // > targetYear 115
        _repo.Setup(r => r.GetPrepaySourcesAsync(114, SourceCat, 1, 1, 115, 1, default))
            .ReturnsAsync([src]);
        _repo.Setup(r => r.SignupExistsAsync(115, TargetCat, 1, src.BelieverId, default)).ReturnsAsync(false);

        IReadOnlyList<(SignupWriteModel, SignupLogWriteModel, int)>? captured = null;
        _repo.Setup(r => r.InsertBatchAsync(It.IsAny<IReadOnlyList<(SignupWriteModel, SignupLogWriteModel, int)>>(), default))
            .Callback<IReadOnlyList<(SignupWriteModel, SignupLogWriteModel, int)>, CancellationToken>((b, _) => captured = b);

        var result = await CreateSut().HandleAsync(Req(), _caller);

        result.Details.CarriedForwardPrepay.Should().Be(1);
        captured![0].Item1.PrepayYear.Should().Be(117);
        captured[0].Item1.PrepayCeremonyCategoryId.Should().Be(futurePrepayCat);
    }
}
