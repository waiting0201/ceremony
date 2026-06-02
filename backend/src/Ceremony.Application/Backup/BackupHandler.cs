namespace Ceremony.Application.Backup;

/// <summary>
/// 觸發資料庫備份。
/// </summary>
/// <remarks>
/// Legacy: MainForm.cs:95-113 (btnBackup_Click)
/// Blueprint: docs/blueprints/api-endpoints/post-backup.md
/// Coverage:  docs/blueprints/legacy-coverage/main-form.md (row 8)
/// </remarks>
public sealed class BackupHandler(IBackupService service)
{
    public Task<BackupResponse> HandleAsync(BackupRequest req, CancellationToken ct = default)
        => service.BackupAsync(req.CustomFileName, req.ClearLog, ct);

    /// <summary>開啟備份檔串流供下載另存（檔名 traversal 防護於 service 內）。</summary>
    public BackupFileResult OpenFile(string fileName)
        => service.OpenBackupFile(fileName);
}
