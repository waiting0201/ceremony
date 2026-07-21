using Ceremony.Migrations;

// Ceremony schema migration runner（DbUp，SQL 腳本式）— 伺服器式部署用的 CLI 入口。
// 用法：dotnet Ceremony.Migrations.dll "<connection-string>"
//   或設環境變數 ConnectionStrings__Ceremony / CEREMONY_CONNECTION 後不帶參數執行。
// 客戶端（Electron sidecar）走的是 Ceremony.Api 啟動時自動呼叫 MigrationRunner.Run，不用此 CLI。

var connectionString =
    args.FirstOrDefault()
    ?? Environment.GetEnvironmentVariable("ConnectionStrings__Ceremony")
    ?? Environment.GetEnvironmentVariable("CEREMONY_CONNECTION");

if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.Error.WriteLine(
        "缺少連線字串。用法：dotnet Ceremony.Migrations.dll \"<connection-string>\"，" +
        "或設定環境變數 ConnectionStrings__Ceremony。");
    return 2;
}

var result = MigrationRunner.Run(connectionString, msg => Console.WriteLine(msg));

if (!result.Successful)
{
    Console.Error.WriteLine($"Migration 失敗：{result.Error}");
    return 1;
}

Console.WriteLine($"Migration 成功（本次套用 {result.ScriptsExecuted} 支腳本）。");
return 0;
