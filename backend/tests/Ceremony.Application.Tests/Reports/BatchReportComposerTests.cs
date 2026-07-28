using Ceremony.Application.Reports;
using Ceremony.Application.Signups;
using FluentAssertions;
using Moq;

namespace Ceremony.Application.Tests.Reports;

/// <summary>
/// BatchReportComposer 的進度回報與取消行為（渲染內容本身由 BatchReportHandlerTests 覆蓋）。
/// </summary>
public sealed class BatchReportComposerTests
{
    private readonly Mock<IReportRenderer> _renderer = new();
    private readonly Mock<IPdfMerger> _merger = new();

    private static SignupListItem Make(int number) => new(
        Id: Guid.NewGuid(),
        Year: 115,
        CeremonyCategoryId: Guid.NewGuid(),
        CeremonyTitle: "春季",
        SignupType: 1,
        NumberTitle: "No",
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

    private static BatchReportPlan Plan(int count) =>
        new("datacard", "batch-datacard-1-9.pdf", Enumerable.Range(1, count).Select(Make).ToList());

    [Fact]
    public void Reports_progress_once_per_signup_in_order()
    {
        _renderer.Setup(r => r.RenderDataCard(It.IsAny<DataCardModel>())).Returns([1]);
        _merger.Setup(m => m.Merge(It.IsAny<IReadOnlyList<byte[]>>())).Returns([9]);

        var seen = new List<int>();
        BatchReportComposer.Render(_renderer.Object, _merger.Object, Plan(4), seen.Add);

        seen.Should().Equal(1, 2, 3, 4);
    }

    [Fact]
    public void Null_progress_callback_is_allowed()
    {
        _renderer.Setup(r => r.RenderDataCard(It.IsAny<DataCardModel>())).Returns([1]);
        _merger.Setup(m => m.Merge(It.IsAny<IReadOnlyList<byte[]>>())).Returns([9]);

        var pdf = BatchReportComposer.Render(_renderer.Object, _merger.Object, Plan(2), null);

        pdf.Should().Equal(9);
    }

    [Fact]
    public void Cancellation_stops_rendering_and_never_merges()
    {
        using var cts = new CancellationTokenSource();
        _renderer.Setup(r => r.RenderDataCard(It.IsAny<DataCardModel>())).Returns([1]);
        _merger.Setup(m => m.Merge(It.IsAny<IReadOnlyList<byte[]>>())).Returns([9]);

        // 印完第 2 筆就按取消 → 最多只會多跑當下這一筆
        var act = () => BatchReportComposer.Render(
            _renderer.Object, _merger.Object, Plan(10),
            done => { if (done == 2) cts.Cancel(); },
            cts.Token);

        act.Should().Throw<OperationCanceledException>();
        _renderer.Verify(r => r.RenderDataCard(It.IsAny<DataCardModel>()), Times.Exactly(2));
        _merger.Verify(m => m.Merge(It.IsAny<IReadOnlyList<byte[]>>()), Times.Never);
    }

    [Fact]
    public void Already_canceled_token_throws_before_first_render()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = () => BatchReportComposer.Render(_renderer.Object, _merger.Object, Plan(3), null, cts.Token);

        act.Should().Throw<OperationCanceledException>();
        _renderer.Verify(r => r.RenderDataCard(It.IsAny<DataCardModel>()), Times.Never);
    }
}
