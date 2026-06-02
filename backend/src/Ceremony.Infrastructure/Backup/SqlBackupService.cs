using System.Text.RegularExpressions;
using Ceremony.Application.Backup;
using Ceremony.Domain.Exceptions;
using Ceremony.Infrastructure.Persistence;
using Dapper;
using Microsoft.Extensions.Configuration;

namespace Ceremony.Infrastructure.Backup;

/// <summary>
/// SQL Server BACKUP DATABASE 包裝。
/// </summary>
/// <remarks>
/// Legacy: MainForm.cs:95-113 — 舊系統用 SqlCommand 跑 BACKUP DATABASE [dbname] TO DISK。
/// 對齊舊系統：檔名 yyyyMMddHHmmssffffff.bak、SQL flags 完全相同、DB 名動態取自連線、目錄不存在則建立。
/// 唯一改良：備份目錄改由 Backup:Directory config 提供（舊系統硬編碼 D:\Backup\）。
/// </remarks>
public sealed class SqlBackupService(IDbConnectionFactory factory, IConfiguration config) : IBackupService
{
    public async Task<BackupResponse> BackupAsync(string? customFileName, bool clearLog, CancellationToken ct = default)
    {
        var startedAt = DateTime.UtcNow;

        var directory = config["Backup:Directory"]
            ?? throw new DomainException("BACKUP_NOT_CONFIGURED", "Backup:Directory 未設定");

        // 對齊舊系統 MainForm.cs:103-106：目錄不存在則建立。
        // 但 sidecar 架構下 SQL Server 與 API 可能不同機（DB 在容器 / 遠端 Windows），
        // 此目錄屬於「SQL Server 主機」的檔案系統，API 端建立只是 best-effort；
        // 真正要可寫由 DBA 在 DB 主機預先建立並授權 SQL Server 服務帳號。
        TryCreateLocalDirectory(directory);

        await using var conn = await factory.CreateOpenAsync(ct);

        // 對齊舊系統 MainForm.cs:99：DB 名動態取自連線（db.Database.Connection.Database）
        var dbName = conn.Database;

        var (fileName, fullPath, sql) = BuildBackup(dbName, directory, customFileName, DateTime.Now);

        await conn.ExecuteAsync(new CommandDefinition(sql, commandTimeout: 600, cancellationToken: ct));

        var completedAt = DateTime.UtcNow;

        // .bak 落在「SQL Server 主機」檔案系統；當 API 與 DB 不同機（容器 / 遠端）時 File.Exists 看不到。
        // 優先用本機檔案大小（可見時最精確），否則改查 msdb.dbo.backupset 取邏輯備份大小。
        long size = File.Exists(fullPath)
            ? new FileInfo(fullPath).Length
            : await TryGetBackupSizeAsync(conn, dbName, ct);

        // 清交易紀錄檔（opt-in）。完整備份已成功，清 log 失敗不應讓整支 API 失敗 → 回傳結果欄位。
        var logResult = clearLog
            ? await ClearTransactionLogAsync(conn, dbName, directory, ct)
            : (Cleared: false, TrnFileName: (string?)null, Error: (string?)null);

        return new BackupResponse(
            fileName, fullPath, size, startedAt, completedAt,
            LogCleared: logResult.Cleared,
            LogBackupFileName: logResult.TrnFileName,
            LogClearError: logResult.Error);
    }

    /// <summary>
    /// 開啟 Backup:Directory 下指定備份檔以供下載另存（Electron 原生「另存新檔」）。
    /// 檔名先過 <see cref="IsValidBackupFileName"/> traversal 防護；目錄取自 config、
    /// 用 <see cref="JoinForSqlServer"/> 組「SQL Server 主機」路徑。
    /// sidecar 模式下 API 與 DB 不同機時，該目錄須為 API process 讀得到的共用路徑（prod = UNC），
    /// 否則 File.Exists 為 false → 視為找不到檔（回 404）。
    /// </summary>
    public BackupFileResult OpenBackupFile(string fileName)
    {
        if (!IsValidBackupFileName(fileName))
            throw new DomainException("VALIDATION_BACKUP_FILENAME", "備份檔名格式不正確");

        var directory = config["Backup:Directory"]
            ?? throw new DomainException("BACKUP_NOT_CONFIGURED", "Backup:Directory 未設定");

        var fullPath = JoinForSqlServer(directory, fileName);

        if (!File.Exists(fullPath))
            throw new DomainException(
                "BACKUP_FILE_NOT_FOUND",
                "找不到備份檔（請確認備份目錄可由應用程式讀取；sidecar 模式建議使用 UNC 共用路徑）");

        var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return new BackupFileResult(stream, fileName, stream.Length);
    }

    /// <summary>
    /// 備份檔名 traversal 防護（純函式，供單元測試）。
    /// 僅允許 <c>[0-9A-Za-z._-]</c> + 副檔名 <c>.bak</c>/<c>.trn</c>，且不含 <c>..</c>。
    /// 拒絕任何路徑分隔符 / 磁碟代號 / 上層參照，避免讀到 Backup:Directory 以外的檔。
    /// </summary>
    internal static bool IsValidBackupFileName(string? fileName) =>
        !string.IsNullOrWhiteSpace(fileName)
        && !fileName.Contains("..", StringComparison.Ordinal)
        && BackupFileNamePattern.IsMatch(fileName);

    private static readonly Regex BackupFileNamePattern =
        new(@"^[0-9A-Za-z._-]+\.(bak|trn)$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// 依 recovery model 安全清交易紀錄檔（不破壞還原鏈；完整備份已先完成）。
    /// FULL/BULK_LOGGED：先 BACKUP LOG（正確截斷、保留 .trn）再 SHRINKFILE；
    /// SIMPLE：CHECKPOINT 後 SHRINKFILE。失敗不丟例外，回傳 Error 供前端提示。
    /// </summary>
    private static async Task<(bool Cleared, string? TrnFileName, string? Error)> ClearTransactionLogAsync(
        System.Data.Common.DbConnection conn, string dbName, string directory, CancellationToken ct)
    {
        try
        {
            var meta = await conn.QuerySingleOrDefaultAsync<LogMeta>(
                new CommandDefinition(
                    "SELECT d.recovery_model_desc AS RecoveryModel, f.name AS LogName "
                    + "FROM sys.databases d JOIN sys.master_files f ON f.database_id = d.database_id "
                    + "WHERE d.name = @db AND f.type = 1",
                    new { db = dbName }, cancellationToken: ct));

            if (meta is null || string.IsNullOrEmpty(meta.LogName))
                return (false, null, "找不到交易紀錄檔（log file）");

            var (sql, trnFileName) = BuildClearLog(meta.RecoveryModel, meta.LogName, dbName, directory, DateTime.Now);

            await conn.ExecuteAsync(new CommandDefinition(sql, commandTimeout: 600, cancellationToken: ct));
            return (true, trnFileName, null);
        }
        catch (Exception ex)
        {
            return (false, null, ex.Message);
        }
    }

    /// <summary>
    /// 組備份檔名 + 完整路徑 + BACKUP SQL（抽出為純函式以利單元測試）。
    /// 對齊 MainForm.cs:100-101：檔名 yyyyMMddHHmmssffffff.bak、SQL flags 逐字相同。
    /// BACKUP DATABASE 不能參數化 DB 名 / 路徑；DB 名來自連線、路徑由 config + 時戳組成，
    /// 皆不接受外部任意輸入，仍對識別字 / 字串字面值 escape 防注入。
    /// </summary>
    internal static (string FileName, string FullPath, string Sql) BuildBackup(
        string dbName, string directory, string? customFileName, DateTime now)
    {
        var fileName = string.IsNullOrWhiteSpace(customFileName)
            ? $"{now:yyyyMMddHHmmssffffff}.bak"
            : customFileName;

        var fullPath = JoinForSqlServer(directory, fileName);

        var sql = $"BACKUP DATABASE [{dbName.Replace("]", "]]")}] TO DISK = N'{fullPath.Replace("'", "''")}' "
                + "WITH NOFORMAT, NOINIT, NAME = N'Ceremony-Full Database Backup', SKIP, NOREWIND, NOUNLOAD, STATS = 10";

        return (fileName, fullPath, sql);
    }

    /// <summary>
    /// 依 recovery model 組「清交易紀錄檔」的 batch SQL（抽出為純函式以利單元測試）。
    /// FULL / BULK_LOGGED：BACKUP LOG（截斷、保留 .trn）+ DBCC SHRINKFILE；回傳 .trn 檔名。
    /// SIMPLE（及其他）：CHECKPOINT + DBCC SHRINKFILE；TrnFileName 為 null。
    /// SHRINKFILE 目標 1 MB。DB 名走 [ ] 跳脫、路徑 / log 名走字串字面值 '' 跳脫。
    /// </summary>
    internal static (string Sql, string? TrnFileName) BuildClearLog(
        string recoveryModel, string logName, string dbName, string directory, DateTime now)
    {
        var db = dbName.Replace("]", "]]");
        var log = logName.Replace("'", "''");
        var needsLogBackup = recoveryModel is "FULL" or "BULK_LOGGED";

        if (needsLogBackup)
        {
            var trnFileName = $"{now:yyyyMMddHHmmssffffff}.trn";
            var trnPath = JoinForSqlServer(directory, trnFileName).Replace("'", "''");
            var sql =
                $"BACKUP LOG [{db}] TO DISK = N'{trnPath}' "
                + "WITH NOFORMAT, NOINIT, NAME = N'Ceremony-Log Backup', SKIP, NOREWIND, NOUNLOAD; "
                + $"DBCC SHRINKFILE (N'{log}', 1);";
            return (sql, trnFileName);
        }

        // SIMPLE：checkpoint 即截斷，再 shrink 釋放空間（不需也不可 BACKUP LOG）
        return ($"CHECKPOINT; DBCC SHRINKFILE (N'{log}', 1);", null);
    }

    /// <summary>
    /// 從 msdb.dbo.backupset 取最近一次該 DB 的備份大小（bytes）；查不到回 0。
    /// 用於 API 與 SQL Server 不同機、本機看不到 .bak 檔的情境。
    /// </summary>
    private static async Task<long> TryGetBackupSizeAsync(System.Data.Common.DbConnection conn, string dbName, CancellationToken ct)
    {
        try
        {
            const string q = "SELECT TOP 1 CAST(backup_size AS bigint) FROM msdb.dbo.backupset "
                           + "WHERE database_name = @db ORDER BY backup_finish_date DESC";
            return await conn.ExecuteScalarAsync<long?>(new CommandDefinition(q, new { db = dbName }, cancellationToken: ct)) ?? 0L;
        }
        catch
        {
            return 0L;
        }
    }

    /// <summary>
    /// 依「SQL Server 主機」的路徑風格組路徑，而非 API 執行機（不可用 Path.Combine：
    /// 在 macOS/Linux 跑、但 DB 是 Windows 時會組出 <c>D:\Backup\/file</c> 混用分隔符）。
    /// 規則：目錄含反斜線 → Windows 風格用 <c>\</c>；否則 Unix 風格用 <c>/</c>。
    /// </summary>
    internal static string JoinForSqlServer(string directory, string fileName)
    {
        var dir = directory.TrimEnd('/', '\\');
        var sep = dir.Contains('\\') ? '\\' : '/';
        return $"{dir}{sep}{fileName}";
    }

    private sealed record LogMeta(string RecoveryModel, string LogName);

    private static void TryCreateLocalDirectory(string directory)
    {
        try
        {
            Directory.CreateDirectory(directory);
        }
        catch
        {
            // 目錄在 SQL Server 主機（容器 / 遠端）→ API 端無法建立屬正常，交由 SQL Server 寫入。
        }
    }
}
