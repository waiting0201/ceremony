using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using Ceremony.PrintForm;

// Ceremony.PrintForm —— 列印前把驅動的紙張預選成該報表對應的自訂表單。
//
// 由 Electron 主行程在「開 PDF 檢視器視窗之前」以 execFile 呼叫（best-effort、3 秒逾時）。
// 存在的理由見 docs/blueprints/print-channel-electron.md 決策 9：舊系統在跳原生列印對話框前
// 會用中文表單名去驅動的紙張清單比對（SignupForm.cs:1770-1787），新系統 v2.3.9 把這格一起拿掉了，
// 於是六種報表都只能吃驅動的單一預設紙張——這就是「舊系統會自動找到印表機設定、新系統不行」的客訴。
//
// 契約：
//   apply <reportType>
//   restore <kind> <fields> <width> <length> [printerName]
//
// **exit code 永遠 0**，成敗一律看 stdout 那行 JSON 的 result 欄位。
// 理由：呼叫端絕不能因為這支程式失敗就讓列印失敗（PrintService 會把 ok:false 丟成使用者看得到的紅字），
// 用 exit code 表達失敗只會誘導呼叫端寫出「非 0 就當錯誤」的分支。

Console.OutputEncoding = Encoding.UTF8;

var sw = Stopwatch.StartNew();
JsonObject json;
try
{
    json = Run(args);
}
catch (Exception e)
{
    json = new JsonObject { ["result"] = "error", ["error"] = e.Message };
}

json["ms"] = sw.ElapsedMilliseconds;
Console.Out.WriteLine(json.ToJsonString());
return 0;

static JsonObject Run(string[] args)
{
    if (args.Length == 0) return Fail("missing command");

    switch (args[0])
    {
        case "apply":
            if (args.Length < 2) return Fail("apply requires <reportType>");
            return ToJson(PrinterFormApplier.Apply(args[1]));

        case "restore":
            if (args.Length < 5) return Fail("restore requires <kind> <fields> <width> <length>");
            if (!short.TryParse(args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var kind) ||
                !uint.TryParse(args[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var fields) ||
                !short.TryParse(args[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var width) ||
                !short.TryParse(args[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out var length))
                return Fail("restore args must be integers");

            var printer = args.Length >= 6 ? args[5] : null;
            return ToJson(PrinterFormApplier.Restore(
                new PrinterFormApplier.Snapshot(kind, fields, width, length), printer));

        default:
            return Fail($"unknown command: {args[0]}");
    }
}

static JsonObject Fail(string message) => new() { ["result"] = "error", ["error"] = message };

static JsonObject ToJson(PrinterFormApplier.Outcome o)
{
    var json = new JsonObject { ["result"] = o.Result };

    if (o.Form is not null) json["form"] = o.Form;
    if (o.Kind is not null) json["kind"] = o.Kind.Value;
    if (o.PrinterHash is not null) json["printerHash"] = o.PrinterHash;
    if (o.Virtual is not null) json["virtual"] = o.Virtual.Value;
    if (o.Win32 is not null) json["win32"] = o.Win32.Value;
    if (o.Error is not null) json["error"] = o.Error;

    if (o.MismatchWidthMm is not null && o.MismatchHeightMm is not null)
        json["mismatchMm"] = new JsonObject { ["w"] = o.MismatchWidthMm.Value, ["h"] = o.MismatchHeightMm.Value };

    // 還原用的四個純量 + 印表機名稱。刻意不回傳整包 DEVMODE blob：blob 會過期（使用者中途改過
    // 驅動設定就被整包蓋回去），四個純量只還原我們動過的東西，副作用面積最小、而且人看得懂。
    // ⚠️ printer 是原始名稱，呼叫端只能寫進還原 journal，**不得**寫進診斷紀錄（那裡用 printerHash）。
    if (o.Prev is { } prev)
        json["prev"] = new JsonObject
        {
            ["kind"] = prev.Kind,
            ["fields"] = prev.Fields,
            ["w"] = prev.Width,
            ["h"] = prev.Length,
            ["printer"] = o.Printer,
        };

    return json;
}
