using Ceremony.Application.Reports;
using Ceremony.Application.Signups;
using Ceremony.Domain.Exceptions;
using FluentAssertions;
using Moq;

namespace Ceremony.Application.Tests.Reports;

public sealed class BatchReportHandlerTests
{
    private readonly Mock<ISignupRepository> _repo = new();
    private readonly Mock<IReportRenderer> _renderer = new();
    private readonly Mock<IPdfMerger> _merger = new();

    private BatchReportHandler Sut() => new(_repo.Object, _renderer.Object, _merger.Object);

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
        var act = () => Sut().HandleAsync(new BatchReportRequest("datacard", NumberStart: 50, NumberEnd: 10));
        await act.Should().ThrowAsync<DomainException>()
            .Where(e => e.ErrorCode == "VALIDATION_INVALID" && e.Message == "編號錯誤");
    }

    [Theory]
    [InlineData("")]
    [InlineData("invoice")]
    [InlineData("foo")]
    public async Task Invalid_reportType_throws_VALIDATION_INVALID(string type)
    {
        var act = () => Sut().HandleAsync(new BatchReportRequest(type, 1, 10));
        await act.Should().ThrowAsync<DomainException>()
            .Where(e => e.ErrorCode == "VALIDATION_INVALID" && e.Message == "報表類型錯誤");
    }

    [Fact]
    public async Task No_signups_match_throws_BATCH_NO_SIGNUPS()
    {
        _repo.Setup(r => r.SearchByNumberRangeAsync(It.IsAny<SignupRangeQuery>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(Array.Empty<SignupListItem>());

        var act = () => Sut().HandleAsync(new BatchReportRequest("datacard", 1, 10));
        await act.Should().ThrowAsync<DomainException>()
            .Where(e => e.ErrorCode == "BATCH_NO_SIGNUPS" && e.Message == "查無符合條件的報名資料");
    }

    [Fact]
    public async Task Datacard_renders_per_signup_and_merges()
    {
        var signups = new[] { Make(1), Make(2), Make(3) };
        _repo.Setup(r => r.SearchByNumberRangeAsync(It.IsAny<SignupRangeQuery>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(signups);
        _renderer.Setup(r => r.RenderDataCard(It.IsAny<DataCardModel>())).Returns(new byte[] { 1, 2, 3 });
        _merger.Setup(m => m.Merge(It.IsAny<IReadOnlyList<byte[]>>())).Returns(new byte[] { 9, 9, 9 });

        var (pdf, fileName, count) = await Sut().HandleAsync(new BatchReportRequest("datacard", 1, 50));

        count.Should().Be(3);
        fileName.Should().Be("batch-datacard-1-50.pdf");
        pdf.Should().Equal(9, 9, 9);
        _renderer.Verify(r => r.RenderDataCard(It.IsAny<DataCardModel>()), Times.Exactly(3));
        _merger.Verify(m => m.Merge(It.Is<IReadOnlyList<byte[]>>(l => l.Count == 3)), Times.Once);
    }

    [Fact]
    public async Task Worship_forces_signupType_4_filter()
    {
        _repo.Setup(r => r.SearchByNumberRangeAsync(
                It.Is<SignupRangeQuery>(q => q.SignupType == 4),
                It.IsAny<CancellationToken>()))
             .ReturnsAsync([Make(1, signupType: 4)]);
        _renderer.Setup(r => r.RenderWorship(It.IsAny<WorshipModel>())).Returns(new byte[] { 1 });
        _merger.Setup(m => m.Merge(It.IsAny<IReadOnlyList<byte[]>>())).Returns(new byte[] { 1 });

        // 呼叫端傳 SignupType=1 應被 worship 路徑強制覆寫成 4
        var (_, _, count) = await Sut().HandleAsync(new BatchReportRequest("worship", 1, 10, SignupType: 1));
        count.Should().Be(1);

        _repo.Verify(r => r.SearchByNumberRangeAsync(
            It.Is<SignupRangeQuery>(q => q.SignupType == 4),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReportType_is_case_insensitive_and_trimmed()
    {
        _repo.Setup(r => r.SearchByNumberRangeAsync(It.IsAny<SignupRangeQuery>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync([Make(1)]);
        _renderer.Setup(r => r.RenderTablet(It.IsAny<TabletModel>())).Returns(new byte[] { 1 });
        _merger.Setup(m => m.Merge(It.IsAny<IReadOnlyList<byte[]>>())).Returns(new byte[] { 1 });

        var (_, fileName, _) = await Sut().HandleAsync(new BatchReportRequest("  TABLET ", 1, 10));
        fileName.Should().Be("batch-tablet-1-10.pdf");
    }

    [Fact]
    public async Task Forwards_filters_to_repository()
    {
        SignupRangeQuery? captured = null;
        _repo.Setup(r => r.SearchByNumberRangeAsync(It.IsAny<SignupRangeQuery>(), It.IsAny<CancellationToken>()))
             .Callback<SignupRangeQuery, CancellationToken>((q, _) => captured = q)
             .ReturnsAsync([Make(5)]);
        _renderer.Setup(r => r.RenderReceipt(It.IsAny<ReceiptModel>())).Returns(new byte[] { 1 });
        _merger.Setup(m => m.Merge(It.IsAny<IReadOnlyList<byte[]>>())).Returns(new byte[] { 1 });

        var cid = Guid.NewGuid();
        await Sut().HandleAsync(new BatchReportRequest("receipt", 1, 100,
            Year: 115, YearGte: true, CeremonyCategoryId: cid, SignupType: 2));

        captured.Should().NotBeNull();
        captured!.NumberStart.Should().Be(1);
        captured.NumberEnd.Should().Be(100);
        captured.Year.Should().Be(115);
        captured.YearGte.Should().BeTrue();
        captured.CeremonyCategoryId.Should().Be(cid);
        captured.SignupType.Should().Be(2);
    }

    [Fact]
    public async Task Missing_both_ids_and_range_throws_VALIDATION_INVALID()
    {
        var act = () => Sut().HandleAsync(new BatchReportRequest("datacard"));
        await act.Should().ThrowAsync<DomainException>()
            .Where(e => e.ErrorCode == "VALIDATION_INVALID" && e.Message == "編號錯誤");
    }

    [Fact]
    public async Task SignupIds_prints_exact_selection_ignoring_gaps()
    {
        var signups = new[] { Make(1), Make(9) };
        var ids = signups.Select(s => s.Id).ToList();
        _repo.Setup(r => r.SearchByIdsAsync(
                It.Is<IReadOnlyList<Guid>>(l => l.SequenceEqual(ids)),
                It.IsAny<CancellationToken>()))
             .ReturnsAsync(signups);
        _renderer.Setup(r => r.RenderDataCard(It.IsAny<DataCardModel>())).Returns(new byte[] { 1 });
        _merger.Setup(m => m.Merge(It.IsAny<IReadOnlyList<byte[]>>())).Returns(new byte[] { 9, 9 });

        var (pdf, fileName, count) = await Sut().HandleAsync(new BatchReportRequest("datacard", SignupIds: ids));

        count.Should().Be(2);
        fileName.Should().Be("batch-datacard-selected-2.pdf");
        pdf.Should().Equal(9, 9);
        _repo.Verify(r => r.SearchByNumberRangeAsync(It.IsAny<SignupRangeQuery>(), It.IsAny<CancellationToken>()), Times.Never);
        _renderer.Verify(r => r.RenderDataCard(It.IsAny<DataCardModel>()), Times.Exactly(2));
    }

    [Fact]
    public async Task SignupIds_takes_priority_over_range_when_both_provided()
    {
        var signups = new[] { Make(1) };
        var ids = signups.Select(s => s.Id).ToList();
        _repo.Setup(r => r.SearchByIdsAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(signups);
        _renderer.Setup(r => r.RenderDataCard(It.IsAny<DataCardModel>())).Returns(new byte[] { 1 });
        _merger.Setup(m => m.Merge(It.IsAny<IReadOnlyList<byte[]>>())).Returns(new byte[] { 1 });

        await Sut().HandleAsync(new BatchReportRequest("datacard", NumberStart: 1, NumberEnd: 10, SignupIds: ids));

        _repo.Verify(r => r.SearchByNumberRangeAsync(It.IsAny<SignupRangeQuery>(), It.IsAny<CancellationToken>()), Times.Never);
        _repo.Verify(r => r.SearchByIdsAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SignupIds_worship_filters_non_type_4_and_can_end_empty()
    {
        var signups = new[] { Make(1, signupType: 1), Make(2, signupType: 4) };
        var ids = signups.Select(s => s.Id).ToList();
        _repo.Setup(r => r.SearchByIdsAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(signups);
        _renderer.Setup(r => r.RenderWorship(It.IsAny<WorshipModel>())).Returns(new byte[] { 1 });
        _merger.Setup(m => m.Merge(It.IsAny<IReadOnlyList<byte[]>>())).Returns(new byte[] { 1 });

        var (_, _, count) = await Sut().HandleAsync(new BatchReportRequest("worship", SignupIds: ids));

        count.Should().Be(1);
        _renderer.Verify(r => r.RenderWorship(It.IsAny<WorshipModel>()), Times.Once);
    }

    [Fact]
    public async Task SignupIds_worship_all_filtered_out_throws_BATCH_NO_SIGNUPS()
    {
        var signups = new[] { Make(1, signupType: 1) };
        var ids = signups.Select(s => s.Id).ToList();
        _repo.Setup(r => r.SearchByIdsAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(signups);

        var act = () => Sut().HandleAsync(new BatchReportRequest("worship", SignupIds: ids));
        await act.Should().ThrowAsync<DomainException>()
            .Where(e => e.ErrorCode == "BATCH_NO_SIGNUPS" && e.Message == "查無符合條件的報名資料");
    }
}
