using System.Reflection;
using DbUp;
using DbUp.Engine;
using DbUp.Engine.Output;
using Microsoft.Data.SqlClient;

namespace Ceremony.Migrations;

/// <summary>
/// 可重用的 DbUp 執行器：腳本 embedded 在本組件（Scripts/*.sql）。
/// 兩個呼叫者共用同一份腳本與 journal（dbo.SchemaVersions）：
///   1. 本專案 console（伺服器式部署，CLI 帶連線字串）；
///   2. Ceremony.Api 啟動時自動執行（客戶端 Electron sidecar：安裝新版開 App 即自動就緒）。
/// DbUp 冪等 + journal → 每次啟動重跑安全，只套用未執行的腳本。
/// 不使用 EnsureDatabase：直連既有 Ceremony DB。
///
/// 多台客戶端 sidecar 可能幾乎同時啟動 → 以 sp_getapplock 串行化：同一時間只有一台真正跑 migration，
/// 其餘台等它跑完（釋放鎖後）再檢查 journal → 直接 no-op。與本專案配號/預繳共用 sp_getapplock 的並發做法。
/// </summary>
public static class MigrationRunner
{
    /// <summary>schema migration 互斥鎖資源名（與配號的 "signup-number:" 命名空間區隔）。</summary>
    private const string LockResource = "ceremony-schema-migration";

    /// <summary>等待其他台跑完 migration 的最長時間（ms）。寺方資料量下 backfill 僅數秒，90s 足夠寬裕。</summary>
    private const int LockTimeoutMs = 90_000;

    public sealed record Result(bool Successful, int ScriptsExecuted, string? Error);

    /// <summary>
    /// 對指定連線執行所有待套用的 migration（外層以 sp_getapplock 串行化並發）。
    /// </summary>
    /// <param name="connectionString">目標 DB 連線字串（需具 DDL 權限）。</param>
    /// <param name="log">可選的日誌 callback（每條訊息一次）。</param>
    public static Result Run(string connectionString, Action<string>? log = null)
    {
        // 專用鎖連線：sp_getapplock LockOwner='Session' 綁此連線 session，Run 期間持續持有；
        // 這只是 advisory 鎖（僅擋其他台的 sp_getapplock），不會擋 DbUp 自己在別的連線跑 DDL。
        using var lockConn = new SqlConnection(connectionString);
        lockConn.Open();
        AcquireLock(lockConn, log);
        try
        {
            return RunUpgrade(connectionString, log);
        }
        finally
        {
            ReleaseLock(lockConn);
        }
    }

    private static void AcquireLock(SqlConnection conn, Action<string>? log)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            DECLARE @rc int;
            EXEC @rc = sp_getapplock @Resource=@res, @LockMode='Exclusive', @LockOwner='Session', @LockTimeout=@timeout;
            SELECT @rc;
            """;
        cmd.CommandTimeout = (LockTimeoutMs / 1000) + 30;
        cmd.Parameters.AddWithValue("@res", LockResource);
        cmd.Parameters.AddWithValue("@timeout", LockTimeoutMs);

        // rc: 0 = 立即取得, 1 = 等待後取得, < 0 = 逾時(-1)/取消(-2)/死鎖(-3)/參數錯(-999)
        var rc = Convert.ToInt32(cmd.ExecuteScalar());
        if (rc < 0)
            throw new InvalidOperationException(
                $"取得 schema migration 鎖逾時（另一台可能正在執行 migration），rc={rc}。請稍後重新啟動。");
        log?.Invoke($"已取得 schema migration 鎖（rc={rc}）");
    }

    private static void ReleaseLock(SqlConnection conn)
    {
        // 連線關閉時 SQL Server 會自動釋放 Session 鎖；此處顯式釋放更明確，失敗可忽略。
        try
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "EXEC sp_releaseapplock @Resource=@res, @LockOwner='Session';";
            cmd.Parameters.AddWithValue("@res", LockResource);
            cmd.ExecuteNonQuery();
        }
        catch
        {
            // 連線 Dispose 時自動釋放，忽略。
        }
    }

    private static Result RunUpgrade(string connectionString, Action<string>? log)
    {
        var upgraderBuilder = DeployChanges.To
            .SqlDatabase(connectionString)
            .WithScriptsEmbeddedInAssembly(Assembly.GetExecutingAssembly())   // Scripts/*.sql（EmbeddedResource）
            .WithTransactionPerScript()
            .JournalToSqlTable("dbo", "SchemaVersions");                       // 已套用腳本紀錄表

        if (log is not null)
            upgraderBuilder = upgraderBuilder.LogTo(new DelegateUpgradeLog(log));

        UpgradeEngine upgrader = upgraderBuilder.Build();

        if (log is not null)
        {
            foreach (var script in upgrader.GetScriptsToExecute())
                log($"待套用：{script.Name}");
        }

        DatabaseUpgradeResult result = upgrader.PerformUpgrade();
        return new Result(result.Successful, result.Scripts.Count(), result.Error?.Message);
    }

    /// <summary>把 DbUp 內部日誌轉發到呼叫端的 callback（Serilog / Console）。</summary>
    private sealed class DelegateUpgradeLog(Action<string> sink) : IUpgradeLog
    {
        public void LogTrace(string format, params object[] args) => Write(format, args);
        public void LogDebug(string format, params object[] args) => Write(format, args);
        public void LogInformation(string format, params object[] args) => Write(format, args);
        public void LogWarning(string format, params object[] args) => Write(format, args);
        public void LogError(string format, params object[] args) => Write(format, args);
        public void LogError(Exception ex, string format, params object[] args) => Write(format + " " + ex.Message, args);

        private void Write(string format, object[] args)
        {
            try { sink(args is { Length: > 0 } ? string.Format(format, args) : format); }
            catch (FormatException) { sink(format); }
        }
    }
}
