using Ceremony.Application.Reports;
using Ceremony.Application.Signups;
using Ceremony.Domain.Exceptions;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Ceremony.Application.Tests.Reports;

public sealed class BatchPrintJobServiceTests
{
    private const string Owner = "admin-1";
    private const string OtherOwner = "admin-2";

    private readonly Mock<IReportRenderer> _renderer = new();
    private readonly Mock<IPdfMerger> _merger = new();

    private BatchPrintJobService Sut() => new(_renderer.Object, _merger.Object, NullLogger<BatchPrintJobService>.Instance);

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

    private static BatchReportPlan Plan(int count = 3) =>
        new("datacard", $"batch-datacard-selected-{count}.pdf", Enumerable.Range(1, count).Select(Make).ToList());

    /// <summary>合併＝把假成品寫到 dest（成品現在是檔案，不是 byte[]）。</summary>
    private void SetupMerge()
        => _merger.Setup(m => m.Merge(It.IsAny<IReadOnlyList<string>>(), It.IsAny<string>()))
                  .Callback<IReadOnlyList<string>, string>((_, dest) => File.WriteAllBytes(dest, [9, 9]));

    private void RenderFast()
    {
        _renderer.Setup(r => r.RenderDataCard(It.IsAny<DataCardModel>())).Returns([1]);
        SetupMerge();
    }

    /// <summary>擋住渲染，讓測試能在 running 狀態下做斷言；回傳的 gate Set() 後才會繼續。</summary>
    private ManualResetEventSlim RenderBlocked()
    {
        var gate = new ManualResetEventSlim(false);
        _renderer.Setup(r => r.RenderDataCard(It.IsAny<DataCardModel>()))
                 .Returns(() => { gate.Wait(TimeSpan.FromSeconds(10)); return [1]; });
        SetupMerge();
        return gate;
    }

    private static BatchPrintJobState WaitForStatus(BatchPrintJobService sut, Guid jobId, string status)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < deadline)
        {
            var state = sut.GetState(jobId, Owner);
            if (state.Status == status) return state;
            Thread.Sleep(10);
        }
        throw new TimeoutException($"job 未在時限內進入 {status}");
    }

    [Fact]
    public void Start_returns_total_and_filename_immediately()
    {
        RenderFast();
        using var sut = Sut();

        var created = sut.Start(Plan(3), Owner);

        created.Total.Should().Be(3);
        created.FileName.Should().Be("batch-datacard-selected-3.pdf");
        created.ReportType.Should().Be("datacard");
        created.JobId.Should().NotBeEmpty();
    }

    [Fact]
    public void Job_completes_and_reports_full_progress()
    {
        RenderFast();
        using var sut = Sut();

        var created = sut.Start(Plan(3), Owner);
        var state = WaitForStatus(sut, created.JobId, "completed");

        state.Completed.Should().Be(3);
        state.Total.Should().Be(3);
        state.ErrorCode.Should().BeNull();
    }

    [Fact]
    public void TakeFile_returns_pdf_path_once_then_job_is_gone()
    {
        RenderFast();
        using var sut = Sut();

        var created = sut.Start(Plan(2), Owner);
        WaitForStatus(sut, created.JobId, "completed");

        var (pdfPath, fileName, total, reportType) = sut.TakeFile(created.JobId, Owner);
        // 檔案刻意不在 TakeFile 刪：controller 用 DeleteOnClose 串流回應後才消失
        File.Exists(pdfPath).Should().BeTrue();
        File.ReadAllBytes(pdfPath).Should().Equal(9, 9);
        fileName.Should().Be("batch-datacard-selected-2.pdf");
        total.Should().Be(2);
        // controller 用它掛 X-Report-Page-Size
        reportType.Should().Be("datacard");

        // one-shot：第二次就當作不存在
        var act = () => sut.TakeFile(created.JobId, Owner);
        act.Should().Throw<DomainException>().Where(e => e.ErrorCode == "BATCH_JOB_NOT_FOUND");

        File.Delete(pdfPath);
    }

    /// <summary>沒被取走的成品不能留在磁碟上（TTL / 上限 / Dispose 都走 Discard）。</summary>
    [Fact]
    public void Discarded_job_deletes_its_output_file()
    {
        RenderFast();
        var sut = Sut();

        var created = sut.Start(Plan(2), Owner);
        WaitForStatus(sut, created.JobId, "completed");

        // 先偷看路徑（取檔會把 job 移除，但不刪檔），再放回去讓 Dispose 走 Discard
        var (pdfPath, _, _, _) = sut.TakeFile(created.JobId, Owner);
        File.Delete(pdfPath);

        var root = Path.GetDirectoryName(pdfPath)!;
        var second = sut.Start(Plan(1), Owner);
        WaitForStatus(sut, second.JobId, "completed");
        var secondPath = Path.Combine(root, $"{second.JobId:N}.pdf");
        File.Exists(secondPath).Should().BeTrue();

        sut.Dispose();

        File.Exists(secondPath).Should().BeFalse();
    }

    [Fact]
    public void TakeFile_while_running_throws_NOT_READY()
    {
        using var gate = RenderBlocked();
        using var sut = Sut();

        var created = sut.Start(Plan(3), Owner);

        var act = () => sut.TakeFile(created.JobId, Owner);
        act.Should().Throw<DomainException>()
           .Where(e => e.ErrorCode == "BATCH_JOB_NOT_READY" && e.Message == "批次列印尚未完成");

        gate.Set();
    }

    [Fact]
    public void Cancel_stops_the_job_and_state_becomes_canceled()
    {
        using var gate = RenderBlocked();
        using var sut = Sut();

        var created = sut.Start(Plan(5), Owner);
        sut.Cancel(created.JobId, Owner);
        gate.Set(); // 放行 → 下一筆前的 ThrowIfCancellationRequested 會中止

        var state = WaitForStatus(sut, created.JobId, "canceled");
        state.Completed.Should().BeLessThan(5);
        _merger.Verify(m => m.Merge(It.IsAny<IReadOnlyList<string>>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void Cancel_is_idempotent_for_unknown_or_foreign_jobs()
    {
        RenderFast();
        using var sut = Sut();
        var created = sut.Start(Plan(1), Owner);

        var act = () =>
        {
            sut.Cancel(Guid.NewGuid(), Owner);
            sut.Cancel(created.JobId, OtherOwner);
        };

        act.Should().NotThrow();
    }

    [Fact]
    public void Other_owner_cannot_see_the_job()
    {
        RenderFast();
        using var sut = Sut();
        var created = sut.Start(Plan(1), Owner);

        var act = () => sut.GetState(created.JobId, OtherOwner);
        act.Should().Throw<DomainException>()
           .Where(e => e.ErrorCode == "BATCH_JOB_NOT_FOUND" && e.Message == "批次列印工作不存在或已逾期");
    }

    [Fact]
    public void Unknown_job_throws_NOT_FOUND()
    {
        using var sut = Sut();
        var act = () => sut.GetState(Guid.NewGuid(), Owner);
        act.Should().Throw<DomainException>().Where(e => e.ErrorCode == "BATCH_JOB_NOT_FOUND");
    }

    [Fact]
    public void Third_concurrent_job_for_same_owner_is_rejected()
    {
        using var gate = RenderBlocked();
        using var sut = Sut();

        sut.Start(Plan(3), Owner);
        sut.Start(Plan(3), Owner);

        var act = () => sut.Start(Plan(3), Owner);
        act.Should().Throw<DomainException>()
           .Where(e => e.ErrorCode == "BATCH_JOB_LIMIT" && e.Message == "批次列印工作過多，請稍後再試");

        // 不同使用者不受影響
        var other = () => sut.Start(Plan(3), OtherOwner);
        other.Should().NotThrow();

        gate.Set();
    }

    [Fact]
    public void Renderer_failure_marks_job_failed_with_internal_error()
    {
        _renderer.Setup(r => r.RenderDataCard(It.IsAny<DataCardModel>())).Throws(new InvalidOperationException("boom"));
        using var sut = Sut();

        var created = sut.Start(Plan(2), Owner);
        var state = WaitForStatus(sut, created.JobId, "failed");

        state.ErrorCode.Should().Be("INTERNAL_ERROR");
        state.Message.Should().Be("未預期的伺服器錯誤");

        // 失敗的 job 取檔會把原始錯誤丟回來，並釋放 job
        var act = () => sut.TakeFile(created.JobId, Owner);
        act.Should().Throw<DomainException>().Where(e => e.ErrorCode == "INTERNAL_ERROR");
    }

    [Fact]
    public void Dispose_cancels_running_jobs()
    {
        using var gate = RenderBlocked();
        var sut = Sut();
        var created = sut.Start(Plan(5), Owner);

        sut.Dispose();
        gate.Set();

        var act = () => sut.GetState(created.JobId, Owner);
        act.Should().Throw<DomainException>().Where(e => e.ErrorCode == "BATCH_JOB_NOT_FOUND");
    }
}
