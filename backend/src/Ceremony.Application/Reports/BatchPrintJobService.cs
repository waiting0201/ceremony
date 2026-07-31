using System.Collections.Concurrent;
using Ceremony.Domain.Exceptions;
using Microsoft.Extensions.Logging;

namespace Ceremony.Application.Reports;

/// <summary>
/// 批次列印背景工作管理：建立 job、回報進度、取出成品 PDF、取消。
/// </summary>
/// <remarks>
/// Blueprint: docs/blueprints/api-endpoints/post-reports-batch-jobs.md
///
/// 狀態存在記憶體（ConcurrentDictionary）而非 DB/Redis：本系統是 Electron + .NET sidecar，
/// 一台 client 一個 API process、只服務一個使用者，無反向代理也無多實例
/// （見 docs/design/infrastructure.md），與既有 MemoryJwtBlacklist 是同一組取捨。
/// 不用 IMemoryCache 是因為這裡需要列舉（TTL sweep、per-owner 計數）與可變 job 物件。
///
/// 記憶體釋放三層：(1) /file 取走即移除（主力）(2) TTL 10 分鐘 sweep (3) 硬上限 MaxRetained。
/// </remarks>
public sealed class BatchPrintJobService(
    IReportRenderer renderer,
    IPdfMerger merger,
    ILogger<BatchPrintJobService> logger) : IDisposable
{
    private const int MaxRunningPerOwner = 2;
    private const int MaxRetained = 4;
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(10);

    private readonly ConcurrentDictionary<Guid, BatchPrintJob> _jobs = new();

    /// <summary>建立 job 並立刻在背景開始渲染。Total 在此時就是確定值。</summary>
    /// <exception cref="DomainException">BATCH_JOB_LIMIT — 同一使用者同時進行中的批次過多</exception>
    public BatchPrintJobCreated Start(BatchReportPlan plan, string ownerSub)
    {
        Sweep();

        var running = _jobs.Values.Count(j =>
            j.Status == BatchPrintJobStatus.Running && string.Equals(j.OwnerSub, ownerSub, StringComparison.Ordinal));
        if (running >= MaxRunningPerOwner)
            throw new DomainException("BATCH_JOB_LIMIT", "批次列印工作過多，請稍後再試");

        var job = new BatchPrintJob(Guid.NewGuid(), ownerSub, plan.ReportType, plan.FileName, plan.Signups.Count);
        _jobs[job.Id] = job;

        _ = Task.Run(() => Run(job, plan));

        return new BatchPrintJobCreated(job.Id, job.Total, job.FileName, job.ReportType);
    }

    public BatchPrintJobState GetState(Guid jobId, string ownerSub)
    {
        var job = Require(jobId, ownerSub);
        return new BatchPrintJobState(
            job.Id,
            job.Status switch
            {
                BatchPrintJobStatus.Running => "running",
                BatchPrintJobStatus.Completed => "completed",
                BatchPrintJobStatus.Failed => "failed",
                _ => "canceled",
            },
            job.Total,
            job.Completed,
            job.FileName,
            job.ErrorCode,
            job.Message);
    }

    /// <summary>取出成品 PDF；成功後 job 立即移除（one-shot，這是釋放記憶體的主力）。</summary>
    /// <exception cref="DomainException">
    /// BATCH_JOB_NOT_FOUND（不存在／逾期／已取走／已取消／非本人）、
    /// BATCH_JOB_NOT_READY（仍在渲染中）、或該 job 失敗時原本的錯誤
    /// </exception>
    public (byte[] Pdf, string FileName, int Total, string ReportType) TakeFile(Guid jobId, string ownerSub)
    {
        var job = Require(jobId, ownerSub);

        switch (job.Status)
        {
            case BatchPrintJobStatus.Running:
                throw new DomainException("BATCH_JOB_NOT_READY", "批次列印尚未完成");
            case BatchPrintJobStatus.Failed:
                Discard(job);
                throw new DomainException(job.ErrorCode ?? "INTERNAL_ERROR", job.Message ?? "未預期的伺服器錯誤");
            case BatchPrintJobStatus.Canceled:
                Discard(job);
                throw new DomainException("BATCH_JOB_NOT_FOUND", "批次列印工作不存在或已逾期");
        }

        var pdf = job.Pdf ?? throw new DomainException("INTERNAL_ERROR", "未預期的伺服器錯誤");
        Discard(job);
        // ReportType 一併回傳：controller 要據此掛 X-Report-Page-Size（合併 PDF 的每頁尺寸都是同一種報表）。
        return (pdf, job.FileName, job.Total, job.ReportType);
    }

    /// <summary>
    /// 取消 job。冪等：未知 / 非本人 / 已結束都靜默成功（避開「剛好完成」的競態）。
    /// 刻意不移除 job，讓前端最後一次輪詢能看到 canceled 終態；清理交給 TTL sweep。
    /// </summary>
    public void Cancel(Guid jobId, string ownerSub)
    {
        if (!_jobs.TryGetValue(jobId, out var job)) return;
        if (!string.Equals(job.OwnerSub, ownerSub, StringComparison.Ordinal)) return;
        SafeCancel(job);
    }

    private void Run(BatchPrintJob job, BatchReportPlan plan)
    {
        try
        {
            var pdf = BatchReportComposer.Render(
                renderer,
                merger,
                plan,
                done =>
                {
                    job.Completed = done;
                    job.Touch();
                },
                job.Cts.Token);

            job.Pdf = pdf;
            job.Status = BatchPrintJobStatus.Completed;
        }
        catch (OperationCanceledException)
        {
            job.Status = BatchPrintJobStatus.Canceled;
        }
        catch (DomainException ex)
        {
            job.ErrorCode = ex.ErrorCode;
            job.Message = ex.Message;
            job.Status = BatchPrintJobStatus.Failed;
        }
        catch (Exception ex)
        {
            // 背景例外不經過 ExceptionMiddleware，這裡是唯一會留下 stack trace 的地方。
            logger.LogError(ex, "批次列印 job {JobId} 失敗（{ReportType}, {Total} 筆）", job.Id, job.ReportType, job.Total);
            job.ErrorCode = "INTERNAL_ERROR";
            job.Message = "未預期的伺服器錯誤";
            job.Status = BatchPrintJobStatus.Failed;
        }
        finally
        {
            job.Touch();
            // CTS 的擁有權在此：每個 job 只有一個 Run，所以只會 Dispose 一次。
            job.Cts.Dispose();
            Sweep();
        }
    }

    private BatchPrintJob Require(Guid jobId, string ownerSub)
    {
        if (!_jobs.TryGetValue(jobId, out var job)
            || !string.Equals(job.OwnerSub, ownerSub, StringComparison.Ordinal))
        {
            // 非本人也回「不存在」：不洩漏 job 是否存在
            throw new DomainException("BATCH_JOB_NOT_FOUND", "批次列印工作不存在或已逾期");
        }
        return job;
    }

    private void Sweep()
    {
        var cutoff = DateTime.UtcNow - Ttl;
        foreach (var job in _jobs.Values)
        {
            if (job.UpdatedAt < cutoff) Discard(job);
        }

        var excess = _jobs.Count - MaxRetained;
        if (excess <= 0) return;

        // 只淘汰已結束的，進行中的不動（使用者正在等）
        var finished = _jobs.Values
            .Where(j => j.Status != BatchPrintJobStatus.Running)
            .OrderBy(j => j.UpdatedAt)
            .ToList();
        for (var i = 0; i < excess && i < finished.Count; i++) Discard(finished[i]);
    }

    private void Discard(BatchPrintJob job)
    {
        if (!_jobs.TryRemove(job.Id, out _)) return;
        SafeCancel(job);
    }

    private static void SafeCancel(BatchPrintJob job)
    {
        if (job.Status != BatchPrintJobStatus.Running) return;
        try
        {
            job.Cts.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // job 剛好在這瞬間跑完並 Dispose 了 CTS — 沒事可做
        }
    }

    /// <summary>App 關閉時（DI 釋放 singleton）中止所有進行中的渲染。</summary>
    public void Dispose()
    {
        foreach (var job in _jobs.Values) Discard(job);
    }
}
