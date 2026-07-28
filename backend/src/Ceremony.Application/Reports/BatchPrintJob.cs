namespace Ceremony.Application.Reports;

public enum BatchPrintJobStatus
{
    Running,
    Completed,
    Failed,
    Canceled,
}

/// <summary>
/// 一次批次列印的背景工作狀態。可變物件，由 <see cref="BatchPrintJobService"/> 獨佔管理。
/// </summary>
/// <remarks>
/// Blueprint: docs/blueprints/api-endpoints/post-reports-batch-jobs.md
/// </remarks>
public sealed class BatchPrintJob(Guid id, string ownerSub, string reportType, string fileName, int total)
{
    public Guid Id { get; } = id;

    /// <summary>建立者的 JWT sub。跨帳號存取一律當成不存在（回 404，不洩漏 job 是否存在）。</summary>
    public string OwnerSub { get; } = ownerSub;

    public string ReportType { get; } = reportType;
    public string FileName { get; } = fileName;
    public int Total { get; } = total;

    /// <summary>已渲染完成筆數。背景執行緒寫、HTTP 執行緒讀 → 用 Volatile 存取。</summary>
    private int _completed;
    public int Completed
    {
        get => Volatile.Read(ref _completed);
        set => Volatile.Write(ref _completed, value);
    }

    private int _status = (int)BatchPrintJobStatus.Running;
    public BatchPrintJobStatus Status
    {
        get => (BatchPrintJobStatus)Volatile.Read(ref _status);
        set => Volatile.Write(ref _status, (int)value);
    }

    public byte[]? Pdf { get; set; }
    public string? ErrorCode { get; set; }
    public string? Message { get; set; }

    public CancellationTokenSource Cts { get; } = new();

    private long _updatedAtTicks = DateTime.UtcNow.Ticks;
    public DateTime UpdatedAt
    {
        get => new(Volatile.Read(ref _updatedAtTicks), DateTimeKind.Utc);
        set => Volatile.Write(ref _updatedAtTicks, value.Ticks);
    }

    public void Touch() => UpdatedAt = DateTime.UtcNow;
}

/// <summary>POST /reports/batch/jobs 的回應：job 已建立，Total 已確定。</summary>
public sealed record BatchPrintJobCreated(Guid JobId, int Total, string FileName, string ReportType);

/// <summary>GET /reports/batch/jobs/{id} 的回應。</summary>
public sealed record BatchPrintJobState(
    Guid JobId,
    string Status,
    int Total,
    int Completed,
    string FileName,
    string? ErrorCode,
    string? Message);
