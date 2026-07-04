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
            NumberTitle: "No", Number: num, Fee: 100, Name: "來源姓名", Phone: "0912",
            LivingNames: [null, null, null, null, null, null],
            DeadNames: [null, null, null, null, null, null],
            MailZipcodeId: null, MailZipcode: null, MailAddress: "a",
            TextZipcodeId: null, TextZipcode: null, TextAddress: null,
            Remark: null,
            PrepayYear: prepayYear, PrepayCeremonyCategoryId: prepayCat,
            PrepayCeremonySort: prepaySort, PrepayCeremonyTitle: prepayTitle,
            IsFixedNumber: fixedNum, EmployeeType: 1);

    /// <summary>攔截傳給 repo 的候選清單並回傳 canned 結果。</summary>
    private (List<PrepayCandidate> fixedC, List<PrepayCandidate> nonFixedC) CaptureBatch(PrepayLoadResponse canned)
    {
        List<PrepayCandidate> fixedC = [];
        List<PrepayCandidate> nonFixedC = [];
        _repo.Setup(r => r.InsertPrepayBatchAsync(115, TargetCat, It.IsAny<int>(),
                It.IsAny<IReadOnlyList<PrepayCandidate>>(), It.IsAny<IReadOnlyList<PrepayCandidate>>(), default))
            .Callback<int, Guid, int, IReadOnlyList<PrepayCandidate>, IReadOnlyList<PrepayCandidate>, CancellationToken>(
                (_, _, _, f, nf, _) => { fixedC.AddRange(f); nonFixedC.AddRange(nf); })
            .ReturnsAsync(canned);
        return (fixedC, nonFixedC);
    }

    private static PrepayLoadResponse Canned(int loaded = 0, int skipped = 0) =>
        new(loaded, skipped, new PrepayLoadDetails(0, 0, 0, []));

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
    public async Task Passes_response_from_repo_through()
    {
        _repo.Setup(r => r.GetCeremonyCategoryAsync(TargetCat, default)).ReturnsAsync(("春季", 1));
        _repo.Setup(r => r.GetPrepaySourcesAsync(114, SourceCat, 1, 1, 115, 1, default)).ReturnsAsync([]);
        CaptureBatch(new PrepayLoadResponse(7, 2, new PrepayLoadDetails(3, 4, 1, [10, 12])));

        var result = await CreateSut().HandleAsync(Req(), _caller);

        result.Loaded.Should().Be(7);
        result.Skipped.Should().Be(2);
        result.Details.FilledGaps.Should().Equal(10, 12);
    }

    [Fact]
    public async Task Splits_fixed_and_nonFixed_and_leaves_Name_Phone_null()
    {
        _repo.Setup(r => r.GetCeremonyCategoryAsync(TargetCat, default)).ReturnsAsync(("春季", 1));
        var f = Src(num: 5, fixedNum: true, prepayYear: 115);
        var nf = Src(num: 9, fixedNum: false, prepayYear: 115);
        _repo.Setup(r => r.GetPrepaySourcesAsync(114, SourceCat, 1, 1, 115, 1, default)).ReturnsAsync([f, nf]);
        var (fixedC, nonFixedC) = CaptureBatch(Canned());

        await CreateSut().HandleAsync(Req(), _caller);

        fixedC.Should().HaveCount(1);
        nonFixedC.Should().HaveCount(1);

        fixedC[0].IsFixedNumber.Should().BeTrue();
        fixedC[0].PreservedNumber.Should().Be(5, because: "固定候選帶原號");
        nonFixedC[0].PreservedNumber.Should().BeNull(because: "非固定候選不保留來源號");

        // 對齊舊系統：預繳建立的 Signup / SignupLog 皆不帶 Name / Phone
        fixedC[0].Signup.Name.Should().BeNull();
        fixedC[0].Signup.Phone.Should().BeNull();
        fixedC[0].Log.Name.Should().BeNull();
        fixedC[0].Log.Phone.Should().BeNull();
        nonFixedC[0].Signup.Name.Should().BeNull();
        nonFixedC[0].Signup.Phone.Should().BeNull();
    }

    [Fact]
    public async Task FuturePrepayYear_marks_candidate_carriedForward()
    {
        _repo.Setup(r => r.GetCeremonyCategoryAsync(TargetCat, default)).ReturnsAsync(("春季", 1));
        var futureCat = Guid.NewGuid();
        var src = Src(fixedNum: false, prepayYear: 117, prepayCat: futureCat);  // > targetYear 115
        _repo.Setup(r => r.GetPrepaySourcesAsync(114, SourceCat, 1, 1, 115, 1, default)).ReturnsAsync([src]);
        var (_, nonFixedC) = CaptureBatch(Canned());

        await CreateSut().HandleAsync(Req(), _caller);

        nonFixedC[0].CarriedForward.Should().BeTrue();
        nonFixedC[0].Signup.PrepayYear.Should().Be(117);
        nonFixedC[0].Signup.PrepayCeremonyCategoryId.Should().Be(futureCat);
    }

    [Fact]
    public async Task SamePrepayYear_atOrBeforeTargetSort_doesNotCarryForward()
    {
        _repo.Setup(r => r.GetCeremonyCategoryAsync(TargetCat, default)).ReturnsAsync(("春季", 5));
        // PrepayYear == targetYear 115，prepaySort(5) == targetSort(5) → 已結算，不結轉
        var src = Src(fixedNum: false, prepayYear: 115, prepayCat: Guid.NewGuid(), prepaySort: 5);
        _repo.Setup(r => r.GetPrepaySourcesAsync(114, SourceCat, 1, 1, 115, 5, default)).ReturnsAsync([src]);
        var (_, nonFixedC) = CaptureBatch(Canned());

        await CreateSut().HandleAsync(Req(), _caller);

        nonFixedC[0].CarriedForward.Should().BeFalse();
        nonFixedC[0].Signup.PrepayYear.Should().BeNull();
        nonFixedC[0].Signup.PrepayCeremonyCategoryId.Should().BeNull();
    }
}
