using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using Ceremony.Domain.Reports;
using Ceremony.PrintForm;

// Ceremony.PrintForm —— 列印前把驅動的紙張預選成該報表對應的自訂表單。
//
// 由 Electron 主行程在「開 PDF 檢視器視窗之前」以 execFile 呼叫（best-effort、3 秒逾時）。
// 存在的理由見 docs/blueprints/print-channel-electron.md 決策 9：舊系統在跳原生列印對話框前
// 會用中文表單名去驅動的紙張清單比對（SignupForm.cs:1770-1787），新系統 v2.3.9 把這格一起拿掉了，
// 於是六種報表都只能吃驅動的單一預設紙張——這就是「舊系統會自動找到印表機設定、新系統不行」的客訴。
//
// 契約：
//   apply <reportType> [--budget-ms <n>] [--blocked <hash,hash,…>]
//   restore <kind> <fields> <width> <length> [printerName]
//
// apply 的兩個選項都是 2026-08-10（決策 9d，KYOCERA PA2000 卡死客訴）加的，語意見
// Ceremony.Domain.Reports.PrinterContactPolicy：
//   --blocked   呼叫端記下的「碰過會出事」的印表機雜湊；命中就在**任何驅動呼叫之前**結束
//   --budget-ms 呼叫端還會等多久；超過就不寫入（呼叫端逾時後改成放著讓我們跑完，不再中途 kill）
//
// **exit code 永遠 0**，成敗一律看 stdout 那行 JSON 的 result 欄位。
// 理由：呼叫端絕不能因為這支程式失敗就讓列印失敗（PrintService 會把 ok:false 丟成使用者看得到的紅字），
// 用 exit code 表達失敗只會誘導呼叫端寫出「非 0 就當錯誤」的分支。

Console.OutputEncoding = Encoding.UTF8;

var sw = Stopwatch.StartNew();
JsonObject json;
try
{
    json = Run(args, sw);
}
catch (Exception e)
{
    json = new JsonObject { ["result"] = "error", ["error"] = e.Message };
}

json["ms"] = sw.ElapsedMilliseconds;
Console.Out.WriteLine(json.ToJsonString());
return 0;

static JsonObject Run(string[] args, Stopwatch sw)
{
    if (args.Length == 0) return Fail("missing command");

    switch (args[0])
    {
        case "apply":
            if (args.Length < 2) return Fail("apply requires <reportType>");
            return ToJson(PrinterFormApplier.Apply(
                args[1],
                PrinterContactPolicy.ParseBlocked(OptionValue(args, "--blocked")),
                ParseBudget(OptionValue(args, "--budget-ms")),
                () => sw.ElapsedMilliseconds));

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

        case "print":
            if (args.Length < 2) return Fail("print requires <pdfPath>");
            return RunPrint(args);

        default:
            return Fail($"unknown command: {args[0]}");
    }
}

/// <summary>
/// 決策 11：自己帶一份 DEVMODE 進舊版列印對話框，然後自己把 PDF 畫到紙上。
/// </summary>
/// <remarks>
/// <para>
/// <b>這個子命令的 stdout 是 NDJSON（多行），與 apply／restore 的單行不同。</b>
/// 第一行 <c>{"event":"dialog-shown"}</c> 在 <c>PrintDlg</c> **之前** flush——因為那支是 modal 的，
/// 呼叫端必須靠它「對話框已經在螢幕上」就 resolve UI，**絕不能等到印完**。
/// 那是 blueprint 決策 8 的教訓（<c>webContents.print</c> 的 callback 有時不回來，
/// UI 永久卡在「列印中」，只能換頁重建元件）在這條新路徑上的守法。
/// 最後一行永遠帶 <c>result</c>，呼叫端取最後一行即可。
/// </para>
/// <para><b>exit code 仍然永遠 0</b>——不因子命令而異，理由見檔頭。</para>
/// </remarks>
static JsonObject RunPrint(string[] args)
{
    var opt = new DialogPrinter.Options(
        PdfPath: args[1],
        Owner: ParseOwner(OptionValue(args, "--owner")),
        ReportType: OptionValue(args, "--report"),
        NoForm: args.Contains("--no-form"),
        Source: ParseSource(OptionValue(args, "--devmode-source")),
        Scale: OptionValue(args, "--scale") == "stretch"
            ? PrintScalePolicy.ScaleMode.StretchPhysical
            : PrintScalePolicy.ScaleMode.Fit,
        JobName: OptionValue(args, "--job-name"));

    var result = DialogPrinter.Run(opt, line =>
    {
        var obj = new JsonObject { ["event"] = line.Event };
        Fill(obj, line.Fields);
        Console.Out.WriteLine(obj.ToJsonString());
        Console.Out.Flush();   // ⚠️ 沒有這一行，第一行會卡在緩衝區直到行程結束＝失去全部意義
    });

    var json = new JsonObject();
    Fill(json, result);
    return json;
}

static void Fill(JsonObject obj, IReadOnlyDictionary<string, object?> fields)
{
    foreach (var (k, v) in fields)
    {
        obj[k] = v switch
        {
            null => null,
            string s => JsonValue.Create(s),
            int i => JsonValue.Create(i),
            short sh => JsonValue.Create(sh),
            long l => JsonValue.Create(l),
            bool b => JsonValue.Create(b),
            ushort us => JsonValue.Create((int)us),
            _ => JsonValue.Create(v.ToString()),
        };
    }
}

/// <summary>
/// owner 視窗的 HWND（十進位字串）。給不出來就傳 0，對話框仍會開，只是不綁 owner。
/// </summary>
/// <remarks>
/// 綁 owner 有兩個作用：對話框壓在預覽視窗上面，以及 modal 期間把 owner
/// <c>EnableWindow(FALSE)</c>——後者正好讓「對話框開著時預覽窗被關掉、temp 檔被刪」
/// 這個競態不可能發生。
/// </remarks>
static IntPtr ParseOwner(string? raw) =>
    long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var h) ? new IntPtr(h) : IntPtr.Zero;

static DialogPrinter.DevModeSource ParseSource(string? raw) => raw switch
{
    "user" => DialogPrinter.DevModeSource.User,
    "none" => DialogPrinter.DevModeSource.None,
    _ => DialogPrinter.DevModeSource.Printer,
};

static JsonObject Fail(string message) => new() { ["result"] = "error", ["error"] = message };

/// <summary>取 <c>--name value</c> 形式的選項值；沒給就回 null。</summary>
static string? OptionValue(string[] args, string name)
{
    var i = Array.IndexOf(args, name);
    return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
}

/// <summary>
/// 壞掉的預算值一律當成「沒給預算」而不是 0——0 會讓每一次 apply 都變成 skipped-over-budget，
/// 也就是一個打錯的參數把整個功能靜默關掉。
/// </summary>
static int? ParseBudget(string? raw) =>
    int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var ms) && ms > 0 ? ms : null;

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
