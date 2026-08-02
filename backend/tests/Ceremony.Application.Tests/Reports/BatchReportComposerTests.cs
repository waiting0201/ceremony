using Ceremony.Application.Reports;
using Ceremony.Application.Signups;
using FluentAssertions;
using Moq;

namespace Ceremony.Application.Tests.Reports;

/// <summary>
/// BatchReportComposer 的進度回報、取消、暫存檔生命週期
/// （渲染內容本身由各 renderer 的測試覆蓋，選取邏輯由 <see cref="BatchReportHandlerTests"/> 覆蓋）。
/// </summary>
public sealed class BatchReportComposerTests : IDisposable
{
    private readonly Mock<IReportRenderer> _renderer = new();
    private readonly Mock<IPdfMerger> _merger = new();

    private readonly string _tmp = Path.Combine(Path.GetTempPath(), $"ceremony-test-{Guid.NewGuid():N}");
    private string WorkDir => Path.Combine(_tmp, "work");
    private string OutputPath => Path.Combine(_tmp, "out.pdf");

    public void Dispose()
    {
        if (Directory.Exists(_tmp)) Directory.Delete(_tmp, recursive: true);
    }

    /// <summary>合併成假的成品檔，並記下收到的來源路徑。</summary>
    private List<string> SetupMerge()
    {
        var seen = new List<string>();
        _merger.Setup(m => m.Merge(It.IsAny<IReadOnlyList<string>>(), It.IsAny<string>()))
               .Callback<IReadOnlyList<string>, string>((srcs, dest) =>
               {
                   seen.AddRange(srcs);
                   File.WriteAllBytes(dest, [9, 9]);
               });
        return seen;
    }

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

    private string Render(int count, Action<int>? onRendered, CancellationToken ct = default) =>
        BatchReportComposer.Render(
            _renderer.Object, _merger.Object, Plan(count), WorkDir, OutputPath, onRendered, ct);

    [Fact]
    public void Reports_progress_once_per_signup_in_order()
    {
        _renderer.Setup(r => r.RenderDataCard(It.IsAny<DataCardModel>())).Returns([1]);
        SetupMerge();

        var seen = new List<int>();
        Render(4, seen.Add);

        seen.Should().Equal(1, 2, 3, 4);
    }

    [Fact]
    public void Null_progress_callback_is_allowed()
    {
        _renderer.Setup(r => r.RenderDataCard(It.IsAny<DataCardModel>())).Returns([1]);
        SetupMerge();

        var path = Render(2, null);

        path.Should().Be(OutputPath);
        File.ReadAllBytes(path).Should().Equal(9, 9);
    }

    /// <summary>逐筆落檔而非累積 byte[]：合併拿到的是每筆一個檔，且順序＝筆序。</summary>
    [Fact]
    public void Writes_one_part_file_per_signup_in_order()
    {
        _renderer.Setup(r => r.RenderDataCard(It.IsAny<DataCardModel>())).Returns([1]);
        var srcs = SetupMerge();

        Render(3, null);

        srcs.Should().HaveCount(3);
        srcs.Select(Path.GetFileName).Should().Equal("000000.pdf", "000001.pdf", "000002.pdf");
    }

    /// <summary>中間檔一定要清掉——大量列印時它們跟成品一樣大。</summary>
    [Fact]
    public void Work_dir_is_removed_after_success()
    {
        _renderer.Setup(r => r.RenderDataCard(It.IsAny<DataCardModel>())).Returns([1]);
        SetupMerge();

        Render(3, null);

        Directory.Exists(WorkDir).Should().BeFalse();
        File.Exists(OutputPath).Should().BeTrue();
    }

    [Fact]
    public void Cancellation_stops_rendering_never_merges_and_still_cleans_up()
    {
        using var cts = new CancellationTokenSource();
        _renderer.Setup(r => r.RenderDataCard(It.IsAny<DataCardModel>())).Returns([1]);
        SetupMerge();

        // 印完第 2 筆就按取消 → 最多只會多跑當下這一筆
        var act = () => Render(10, done => { if (done == 2) cts.Cancel(); }, cts.Token);

        act.Should().Throw<OperationCanceledException>();
        _renderer.Verify(r => r.RenderDataCard(It.IsAny<DataCardModel>()), Times.Exactly(2));
        _merger.Verify(m => m.Merge(It.IsAny<IReadOnlyList<string>>(), It.IsAny<string>()), Times.Never);
        Directory.Exists(WorkDir).Should().BeFalse();
    }

    [Fact]
    public void Already_canceled_token_throws_before_first_render()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = () => Render(3, null, cts.Token);

        act.Should().Throw<OperationCanceledException>();
        _renderer.Verify(r => r.RenderDataCard(It.IsAny<DataCardModel>()), Times.Never);
        Directory.Exists(WorkDir).Should().BeFalse();
    }
}
