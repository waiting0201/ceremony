using Ceremony.Application.Reports;
using Ceremony.Application.Signups;
using Ceremony.Domain.Exceptions;
using FluentAssertions;
using Moq;

namespace Ceremony.Application.Tests.Reports;

/// <summary>
/// 批次列印的「選取」：驗證、查詢、檔名。渲染與合併不在這裡（見 <see cref="BatchReportComposerTests"/>）。
/// </summary>
/// <remarks>
/// 2026-08-02 起 <see cref="BatchReportHandler"/> 只剩 ResolveAsync——同步版 HandleAsync 隨
/// <c>POST /reports/batch</c> 端點一起移除，前端只走 job 版。
/// </remarks>
public sealed class BatchReportHandlerTests
{
    private readonly Mock<ISignupRepository> _repo = new();

    private BatchReportHandler Sut() => new(_repo.Object);

    private static SignupListItem Make(int number, int signupType = 1) => new(
        Id: Guid.NewGuid(),
        Year: 115,
        CeremonyCategoryId: Guid.NewGuid(),
        CeremonyTitle: "春季",
        SignupType: signupType,
        NumberTitle: signupType == 4 ? "普" : "No",
        Number: number,
        Fee: 600,
        Employee: null,
        BelieverId: null,
        Name: "黃耀章",
        HallName: "甲",
        Phone: "0912345678",
        IsFixedNumber: false,
        LivingNames: ["子甲", null, null, null, null, null],
        DeadNames: ["陳大明", null, null, null, null, null],
        MailCity: "台北市", MailZone: "信義區", MailZipcode: "110", MailAddress: "市府路 1 號",
        TextCity: "台北市", TextZone: "信義區", TextZipcode: "110", TextAddress: "市府路 1 號",
        PrepayYear: null, PrepayCeremonyCategoryId: null, PrepayCeremonyTitle: null,
        Remark: null, AdminName: "Administrator", CreateDate: DateTime.UtcNow);

    [Fact]
    public async Task Invalid_range_throws_VALIDATION_INVALID()
    {
        var act = () => Sut().ResolveAsync(new BatchReportRequest("datacard", NumberStart: 50, NumberEnd: 10));
        await act.Should().ThrowAsync<DomainException>()
            .Where(e => e.ErrorCode == "VALIDATION_INVALID" && e.Message == "編號錯誤");
    }

    [Theory]
    [InlineData("")]
    [InlineData("invoice")]
    [InlineData("foo")]
    public async Task Invalid_reportType_throws_VALIDATION_INVALID(string type)
    {
        var act = () => Sut().ResolveAsync(new BatchReportRequest(type, 1, 10));
        await act.Should().ThrowAsync<DomainException>()
            .Where(e => e.ErrorCode == "VALIDATION_INVALID" && e.Message == "報表類型錯誤");
    }

    [Fact]
    public async Task No_signups_match_throws_BATCH_NO_SIGNUPS()
    {
        _repo.Setup(r => r.SearchByNumberRangeAsync(It.IsAny<SignupRangeQuery>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(Array.Empty<SignupListItem>());

        var act = () => Sut().ResolveAsync(new BatchReportRequest("datacard", 1, 10));
        await act.Should().ThrowAsync<DomainException>()
            .Where(e => e.ErrorCode == "BATCH_NO_SIGNUPS" && e.Message == "查無符合條件的報名資料");
    }

    [Fact]
    public async Task Range_selection_returns_all_matches_with_range_filename()
    {
        var signups = new[] { Make(1), Make(2), Make(3) };
        _repo.Setup(r => r.SearchByNumberRangeAsync(It.IsAny<SignupRangeQuery>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(signups);

        var plan = await Sut().ResolveAsync(new BatchReportRequest("datacard", 1, 50));

        plan.ReportType.Should().Be("datacard");
        plan.Signups.Should().HaveCount(3);
        plan.FileName.Should().Be("batch-datacard-1-50.pdf");
    }

    [Fact]
    public async Task Worship_passes_caller_signupType_through_unchanged()
    {
        _repo.Setup(r => r.SearchByNumberRangeAsync(
                It.Is<SignupRangeQuery>(q => q.SignupType == 1),
                It.IsAny<CancellationToken>()))
             .ReturnsAsync([Make(1, signupType: 1)]);

        // 2026-07-18 解鎖：普桌不再強制 SignupType=4，跟隨呼叫端篩選（對齊舊系統批次 case 5）
        var plan = await Sut().ResolveAsync(new BatchReportRequest("worship", 1, 10, SignupType: 1));
        plan.Signups.Should().HaveCount(1);

        _repo.Verify(r => r.SearchByNumberRangeAsync(
            It.Is<SignupRangeQuery>(q => q.SignupType == 1),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReportType_is_case_insensitive_and_trimmed()
    {
        _repo.Setup(r => r.SearchByNumberRangeAsync(It.IsAny<SignupRangeQuery>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync([Make(1)]);

        var plan = await Sut().ResolveAsync(new BatchReportRequest("  TABLET ", 1, 10));

        plan.ReportType.Should().Be("tablet");
        plan.FileName.Should().Be("batch-tablet-1-10.pdf");
    }

    [Fact]
    public async Task Forwards_filters_to_repository()
    {
        SignupRangeQuery? captured = null;
        _repo.Setup(r => r.SearchByNumberRangeAsync(It.IsAny<SignupRangeQuery>(), It.IsAny<CancellationToken>()))
             .Callback<SignupRangeQuery, CancellationToken>((q, _) => captured = q)
             .ReturnsAsync([Make(5)]);

        var cid = Guid.NewGuid();
        await Sut().ResolveAsync(new BatchReportRequest("receipt", 1, 100,
            Year: 115, YearGte: true, CeremonyCategoryId: cid, SignupType: 2));

        captured.Should().NotBeNull();
        captured!.NumberStart.Should().Be(1);
        captured.NumberEnd.Should().Be(100);
        captured.Year.Should().Be(115);
        captured.YearGte.Should().BeTrue();
        captured.CeremonyCategoryId.Should().Be(cid);
        captured.SignupType.Should().Be(2);
    }

    /// <summary>
    /// 2026-07-31：起迄只填一端＝只印那一筆編號（另一端補同值）。兩端皆空才是「編號錯誤」。
    /// </summary>
    [Theory]
    [InlineData(42, null)]
    [InlineData(null, 42)]
    public async Task Single_ended_range_prints_that_one_number(int? start, int? end)
    {
        SignupRangeQuery? captured = null;
        _repo.Setup(r => r.SearchByNumberRangeAsync(It.IsAny<SignupRangeQuery>(), It.IsAny<CancellationToken>()))
             .Callback<SignupRangeQuery, CancellationToken>((q, _) => captured = q)
             .ReturnsAsync([Make(42)]);

        var plan = await Sut().ResolveAsync(
            new BatchReportRequest("receipt", NumberStart: start, NumberEnd: end));

        captured.Should().NotBeNull();
        captured!.NumberStart.Should().Be(42);
        captured.NumberEnd.Should().Be(42);
        plan.FileName.Should().Be("batch-receipt-42-42.pdf");
    }

    [Fact]
    public async Task Missing_both_ids_and_range_throws_VALIDATION_INVALID()
    {
        var act = () => Sut().ResolveAsync(new BatchReportRequest("datacard"));
        await act.Should().ThrowAsync<DomainException>()
            .Where(e => e.ErrorCode == "VALIDATION_INVALID" && e.Message == "編號錯誤");
    }

    [Fact]
    public async Task SignupIds_selects_exactly_those_ignoring_gaps()
    {
        var signups = new[] { Make(1), Make(9) };
        var ids = signups.Select(s => s.Id).ToList();
        _repo.Setup(r => r.SearchByIdsAsync(
                It.Is<IReadOnlyList<Guid>>(l => l.SequenceEqual(ids)),
                It.IsAny<CancellationToken>()))
             .ReturnsAsync(signups);

        var plan = await Sut().ResolveAsync(new BatchReportRequest("datacard", SignupIds: ids));

        plan.Signups.Should().HaveCount(2);
        plan.FileName.Should().Be("batch-datacard-selected-2.pdf");
        _repo.Verify(r => r.SearchByNumberRangeAsync(It.IsAny<SignupRangeQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SignupIds_takes_priority_over_range_when_both_provided()
    {
        var signups = new[] { Make(1) };
        var ids = signups.Select(s => s.Id).ToList();
        _repo.Setup(r => r.SearchByIdsAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(signups);

        await Sut().ResolveAsync(new BatchReportRequest("datacard", NumberStart: 1, NumberEnd: 10, SignupIds: ids));

        _repo.Verify(r => r.SearchByNumberRangeAsync(It.IsAny<SignupRangeQuery>(), It.IsAny<CancellationToken>()), Times.Never);
        _repo.Verify(r => r.SearchByIdsAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SignupIds_worship_selects_all_regardless_of_type()
    {
        // 2026-07-18 解鎖：混選非普桌不再過濾，選什麼印什麼（對齊舊系統 tsmiPrintWorship）
        var signups = new[] { Make(1, signupType: 1), Make(2, signupType: 4) };
        var ids = signups.Select(s => s.Id).ToList();
        _repo.Setup(r => r.SearchByIdsAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(signups);

        var plan = await Sut().ResolveAsync(new BatchReportRequest("worship", SignupIds: ids));

        plan.Signups.Should().HaveCount(2);
    }

    [Fact]
    public async Task SignupIds_no_match_throws_BATCH_NO_SIGNUPS()
    {
        _repo.Setup(r => r.SearchByIdsAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync([]);

        var act = () => Sut().ResolveAsync(new BatchReportRequest("worship", SignupIds: [Guid.NewGuid()]));
        await act.Should().ThrowAsync<DomainException>()
            .Where(e => e.ErrorCode == "BATCH_NO_SIGNUPS" && e.Message == "查無符合條件的報名資料");
    }
}
