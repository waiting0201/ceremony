using Ceremony.Infrastructure.Backup;
using FluentAssertions;

namespace Ceremony.Infrastructure.Tests.Backup;

/// <summary>
/// 驗證 <see cref="SqlBackupService.BuildBackup"/> 對齊舊系統 MainForm.cs:100-101。
/// </summary>
public sealed class BackupSqlTests
{
    private static readonly DateTime Now = new DateTime(2026, 5, 29, 14, 35, 21, 123).AddTicks(4560);

    [Fact]
    public void DefaultFileName_UsesMicrosecondTimestamp_NoPrefix()
    {
        var (fileName, _, _) = SqlBackupService.BuildBackup("Ceremony", "/var/backups", null, Now);

        // 對齊舊系統 yyyyMMddHHmmssffffff（18 位數字 + .bak），無 "Ceremony-" 前綴
        fileName.Should().StartWith("20260529143521");
        fileName.Should().EndWith(".bak");
        fileName.Should().NotContain("Ceremony-");
        fileName[..^4].Should().HaveLength(20).And.MatchRegex("^[0-9]{20}$");
    }

    [Fact]
    public void CustomFileName_IsUsedVerbatim()
    {
        var (fileName, fullPath, _) = SqlBackupService.BuildBackup("Ceremony", "/var/backups", "manual.bak", Now);

        fileName.Should().Be("manual.bak");
        fullPath.Should().Contain("manual.bak");
    }

    [Fact]
    public void Sql_MatchesLegacyFlagsVerbatim()
    {
        var (_, _, sql) = SqlBackupService.BuildBackup("Ceremony", "/var/backups", "x.bak", Now);

        // MainForm.cs:101 逐字對齊
        sql.Should().Contain("BACKUP DATABASE [Ceremony] TO DISK = N'");
        sql.Should().Contain("WITH NOFORMAT, NOINIT, NAME = N'Ceremony-Full Database Backup', SKIP, NOREWIND, NOUNLOAD, STATS = 10");
        // 不應再用新版的 FORMAT/INIT/COMPRESSION
        sql.Should().NotContain("COMPRESSION");
    }

    [Theory]
    [InlineData("D:\\Backup\\", "f.bak", "D:\\Backup\\f.bak")]   // Windows：含反斜線 → 用 \，且不重複
    [InlineData("D:\\Backup", "f.bak", "D:\\Backup\\f.bak")]
    [InlineData("/var/opt/mssql/data/", "f.bak", "/var/opt/mssql/data/f.bak")] // Linux → 用 /
    [InlineData("/var/opt/mssql/data", "f.bak", "/var/opt/mssql/data/f.bak")]
    public void JoinForSqlServer_UsesHostSeparator_NoMixedSlash(string dir, string file, string expected)
    {
        // 不可在 macOS/Linux 用 Path.Combine 組 Windows 路徑（會變 D:\Backup\/f.bak）
        SqlBackupService.JoinForSqlServer(dir, file).Should().Be(expected);
        SqlBackupService.JoinForSqlServer(dir, file).Should().NotContain("\\/");
    }

    [Fact]
    public void Sql_EscapesIdentifierAndPath()
    {
        var (_, _, sql) = SqlBackupService.BuildBackup("My]DB", "/o'dir", "a'b.bak", Now);

        sql.Should().Contain("[My]]DB]");          // ] → ]]
        sql.Should().Contain("/o''dir");           // ' → ''（目錄）
        sql.Should().Contain("a''b.bak");          // ' → ''（檔名）
    }

    // === 清交易紀錄檔 ===

    [Theory]
    [InlineData("SIMPLE")]
    [InlineData("simple")]
    public void ClearLog_Simple_CheckpointAndShrink_NoLogBackup(string model)
    {
        var (sql, trn) = SqlBackupService.BuildClearLog(model, "Ceremony_log", "Ceremony", "/var/opt/mssql/data", Now);

        trn.Should().BeNull();
        sql.Should().Contain("CHECKPOINT");
        sql.Should().Contain("DBCC SHRINKFILE (N'Ceremony_log', 1)");
        sql.Should().NotContain("BACKUP LOG");
    }

    [Theory]
    [InlineData("FULL")]
    [InlineData("BULK_LOGGED")]
    public void ClearLog_Full_BackupLogThenShrink_ProducesTrn(string model)
    {
        var (sql, trn) = SqlBackupService.BuildClearLog(model, "Ceremony_log", "Ceremony", "/var/opt/mssql/data", Now);

        trn.Should().NotBeNull();
        trn.Should().EndWith(".trn");
        trn![..^4].Should().MatchRegex("^[0-9]{20}$");          // yyyyMMddHHmmssffffff
        sql.Should().Contain("BACKUP LOG [Ceremony] TO DISK = N'/var/opt/mssql/data/");
        sql.Should().Contain("NAME = N'Ceremony-Log Backup'");
        sql.Should().Contain("DBCC SHRINKFILE (N'Ceremony_log', 1)");
        sql.Should().NotContain("\\/");                          // 分隔符不混用
    }

    [Fact]
    public void ClearLog_Full_WindowsDir_UsesBackslash()
    {
        var (sql, _) = SqlBackupService.BuildClearLog("FULL", "Ceremony_log", "Ceremony", "D:\\Backup\\", Now);
        sql.Should().Contain("TO DISK = N'D:\\Backup\\");
        sql.Should().NotContain("\\/");
    }

    [Fact]
    public void ClearLog_EscapesDbAndLogName()
    {
        var (sql, _) = SqlBackupService.BuildClearLog("FULL", "lo'g", "My]DB", "/d", Now);
        sql.Should().Contain("[My]]DB]");          // db ] → ]]
        sql.Should().Contain("N'lo''g'");          // log 名 ' → ''
    }

    // === 下載備份檔名 traversal 防護 ===

    [Theory]
    [InlineData("20260529143521123456.bak")] // 預設時戳檔名
    [InlineData("manual.bak")]
    [InlineData("20260529143521123456.trn")] // log backup
    [InlineData("a-b_c.1.bak")]
    public void IsValidBackupFileName_AcceptsBakAndTrn(string name)
    {
        SqlBackupService.IsValidBackupFileName(name).Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("..")]
    [InlineData("../secret.bak")]              // 上層參照
    [InlineData("..\\secret.bak")]
    [InlineData("sub/dir.bak")]               // 路徑分隔符
    [InlineData("sub\\dir.bak")]
    [InlineData("C:\\Windows\\system.bak")]   // 磁碟代號
    [InlineData("\\\\host\\share\\x.bak")]    // UNC
    [InlineData("file.exe")]                   // 非 .bak/.trn 副檔名
    [InlineData("file.bak.exe")]
    [InlineData("file")]                       // 無副檔名
    [InlineData("名字.bak")]                    // 非 ASCII
    public void IsValidBackupFileName_RejectsTraversalAndForeignExtensions(string? name)
    {
        SqlBackupService.IsValidBackupFileName(name).Should().BeFalse();
    }
}
