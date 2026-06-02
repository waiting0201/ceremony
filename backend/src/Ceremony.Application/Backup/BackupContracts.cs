namespace Ceremony.Application.Backup;

public sealed record BackupRequest(string? CustomFileName = null, bool ClearLog = false);

public sealed record BackupResponse(
    string FileName,
    string FullPath,
    long SizeBytes,
    DateTime StartedAt,
    DateTime CompletedAt,
    bool LogCleared = false,
    string? LogBackupFileName = null,
    string? LogClearError = null);

/// <summary>
/// 開啟備份檔串流的結果。<see cref="Content"/> 由呼叫端（controller）負責 dispose。
/// </summary>
public sealed record BackupFileResult(Stream Content, string FileName, long Length);

public interface IBackupService
{
    Task<BackupResponse> BackupAsync(string? customFileName, bool clearLog, CancellationToken ct = default);

    /// <summary>
    /// 開啟 <c>Backup:Directory</c> 下的備份檔（.bak/.trn）以供下載另存。
    /// 檔名須通過 traversal 防護；找不到 / 不可讀回 <c>BACKUP_FILE_NOT_FOUND</c>。
    /// sidecar 模式下 API process 須讀得到該目錄（prod 建議 UNC 共用）。
    /// </summary>
    BackupFileResult OpenBackupFile(string fileName);
}
