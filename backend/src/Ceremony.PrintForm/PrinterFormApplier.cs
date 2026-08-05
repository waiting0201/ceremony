using System.Drawing.Printing;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Ceremony.Domain.Reports;

namespace Ceremony.PrintForm;

/// <summary>
/// 把預設印表機的「每使用者預設紙張」設成某報表對應的驅動自訂表單，並提供還原。
/// </summary>
/// <remarks>
/// 復刻舊系統 reference/old/Ceremony/SignupForm.cs:1770-1787 的「取得印表機尺寸設定」那段：
/// 用中文表單名比對驅動回報的紙張清單，命中的表單帶著驅動自己的 dmPaperSize ID，
/// 驅動就會自動套用該表單綁定的尺寸與紙匣。
///
/// 與舊系統的差異只有寫入位置：舊系統把 PaperSize 塞進自己那份 PrintDialog 的 DEVMODE，
/// 新系統的送印是 Chromium PDF 檢視器 → PrintDlgEx，JS 層沒有注入點（見 docs/gotchas.md），
/// 唯一的注入點是驅動的每使用者預設 DEVMODE，也就是 SetPrinter Level 9。
/// 因為那是**共用的**（會外溢到 Word/Excel），呼叫端必須在檢視器視窗關閉時還原。
/// </remarks>
internal static class PrinterFormApplier
{
    /// <summary>名稱含這些字樣的一律視為虛擬印表機。預設印表機是虛擬機時必然找不到自訂表單。</summary>
    private static readonly string[] VirtualMarkers =
    [
        "Print to PDF", "XPS Document Writer", "OneNote", "Fax", "Adobe PDF",
        "PDFCreator", "Foxit", "doPDF", "CutePDF", "Send To OneNote",
    ];

    /// <summary>DEVMODE 裡我們動過的那幾格；還原時原封不動寫回去。</summary>
    internal readonly record struct Snapshot(short Kind, uint Fields, short Width, short Length);

    internal sealed class Outcome
    {
        public required string Result { get; init; }
        public string? Form { get; init; }
        public short? Kind { get; init; }
        public Snapshot? Prev { get; init; }
        public double? MismatchWidthMm { get; init; }
        public double? MismatchHeightMm { get; init; }

        /// <summary>
        /// 印表機原始名稱。**只給還原用**（還原時預設印表機可能已經換人），呼叫端必須把它寫進
        /// 還原 journal 而**不是**診斷紀錄——理由見 <see cref="HashPrinterName"/>。
        /// </summary>
        public string? Printer { get; init; }

        public string? PrinterHash { get; init; }
        public bool? Virtual { get; init; }
        public int? Win32 { get; init; }
        public string? Error { get; init; }
    }

    // ───────────────────────── apply ─────────────────────────

    internal static Outcome Apply(string reportType)
    {
        var settings = new PrinterSettings();   // 不指定 PrinterName ＝ Windows 預設印表機（同舊系統）
        var printerName = settings.PrinterName;
        if (string.IsNullOrWhiteSpace(printerName) || !settings.IsValid)
            return new Outcome { Result = "no-default-printer" };

        var hash = HashPrinterName(printerName);
        var isVirtual = VirtualMarkers.Any(m => printerName.Contains(m, StringComparison.OrdinalIgnoreCase));

        List<PrinterFormMatcher.DriverForm> forms;
        try
        {
            forms = ReadDriverForms(settings);
        }
        catch (Exception e)
        {
            return new Outcome { Result = "error", PrinterHash = hash, Virtual = isVirtual, Error = e.Message };
        }

        var match = PrinterFormMatcher.Match(reportType, forms);
        if (match.Match == PrinterFormMatcher.FormMatch.NotFound)
            return new Outcome { Result = "not-found", Form = match.FormName, PrinterHash = hash, Virtual = isVirtual };

        var form = match.Form!.Value;
        var write = WritePaperSize(printerName, form.Kind);

        var mismatch = match.Match == PrinterFormMatcher.FormMatch.SizeMismatch;
        return new Outcome
        {
            // 尺寸不符仍然選它（比停在 A4 好得多），但 result 要區分得出來，好讓呼叫端警告使用者。
            Result = write.Result == "ok" ? (mismatch ? "mismatch" : "exact") : write.Result,
            Form = match.FormName,
            Kind = form.Kind,
            Prev = write.Prev,
            Printer = write.Prev is null ? null : printerName,
            MismatchWidthMm = mismatch ? Math.Round(match.WidthDiffMm, 2) : null,
            MismatchHeightMm = mismatch ? Math.Round(match.HeightDiffMm, 2) : null,
            PrinterHash = hash,
            Virtual = isVirtual,
            Win32 = write.Win32,
            Error = write.Error,
        };
    }

    /// <summary>
    /// 驅動回報的紙張清單。<see cref="PaperSize.Width"/>/<see cref="PaperSize.Height"/> 的單位是
    /// 1/100 吋（量化步階 0.254mm，這正是 <see cref="PrinterFormMatcher.ToleranceMm"/> 的下界依據）。
    /// </summary>
    private static List<PrinterFormMatcher.DriverForm> ReadDriverForms(PrinterSettings settings)
    {
        var list = new List<PrinterFormMatcher.DriverForm>();
        foreach (PaperSize p in settings.PaperSizes)
        {
            list.Add(new PrinterFormMatcher.DriverForm(
                p.PaperName, (short)p.RawKind, p.Width * 25.4 / 100.0, p.Height * 25.4 / 100.0));
        }

        return list;
    }

    // ───────────────────────── restore ─────────────────────────

    internal static Outcome Restore(Snapshot prev, string? printerName)
    {
        if (string.IsNullOrWhiteSpace(printerName))
        {
            var settings = new PrinterSettings();
            if (!settings.IsValid) return new Outcome { Result = "no-default-printer" };
            printerName = settings.PrinterName;
        }

        var write = WriteSnapshot(printerName!, prev);
        return new Outcome
        {
            Result = write.Result == "ok" ? "restored" : write.Result,
            Win32 = write.Win32,
            Error = write.Error,
        };
    }

    // ───────────────────────── Win32 ─────────────────────────

    private readonly record struct WriteResult(string Result, Snapshot? Prev, int? Win32, string? Error);

    private static WriteResult WritePaperSize(string printerName, short kind) =>
        Write(printerName, (pOut, prev) =>
        {
            // 已經是對的紙就不動它——省一次 SetPrinter，也讓呼叫端知道不必寫還原 journal。
            if (DevModePaperFields.AlreadySelected(prev.Kind, prev.Fields, kind)) return false;

            Marshal.WriteInt16(pOut, NativeMethods.OffsetPaperSize, kind);

            // 旗標與值必須同進退：清掉 DM_PAPERWIDTH/DM_PAPERLENGTH 就得把那兩個欄位一併寫 0。
            // 只清旗標、留著上一張紙的寬高＝一份自相矛盾的 DEVMODE，v4 驅動在 DEVMODE→PrintTicket
            // 轉換時會丟例外，使用者看到的就是「您的印表機已發生未預期的設定問題 0x80010105」。
            // 見 DevModePaperFields 的不變式與 docs/gotchas.md。
            Marshal.WriteInt16(pOut, NativeMethods.OffsetPaperWidth, 0);
            Marshal.WriteInt16(pOut, NativeMethods.OffsetPaperLength, 0);
            Marshal.WriteInt32(pOut, NativeMethods.OffsetFields,
                unchecked((int)DevModePaperFields.ForFormSelection(prev.Fields)));

            // 刻意不碰 dmOrientation（舊系統 5 處 Landscape 都被註解掉）
            // 也不碰 dmDefaultSource（舊系統從頭到尾沒動過 PaperSource）。
            return true;
        }, expectKind: kind);

    private static WriteResult WriteSnapshot(string printerName, Snapshot snap) =>
        Write(printerName, (pOut, cur) =>
        {
            Marshal.WriteInt16(pOut, NativeMethods.OffsetPaperSize, snap.Kind);
            Marshal.WriteInt16(pOut, NativeMethods.OffsetPaperWidth, snap.Width);
            Marshal.WriteInt16(pOut, NativeMethods.OffsetPaperLength, snap.Length);

            // 只換回三個紙張位元，其餘保留現況——使用者可能在對話框的「內容」改過方向／雙面，
            // 那個 UI 寫的是同一份 DEVMODE。整包蓋回去會清掉那些旗標卻留著值，等於還原這個動作
            // 自己又生出一份自相矛盾的 DEVMODE（同一個 0x80010105 的第二個入口）。
            Marshal.WriteInt32(pOut, NativeMethods.OffsetFields,
                unchecked((int)DevModePaperFields.ForRestore(cur.Fields, snap.Fields)));
            return true;
        },
        // 快照當時 DM_PAPERSIZE 沒設的話，dmPaperSize 只是個被忽略的殘值，
        // 驗證「驅動有沒有吃下這個 kind」就沒有意義（驅動正規化成別的值是合法的）。
        expectKind: (snap.Fields & DevModePaperFields.PaperSize) != 0 ? snap.Kind : null);

    /// <summary>
    /// OpenPrinter → DocumentProperties(讀) → mutate → DocumentProperties(驗證) → SetPrinter(Level 9)。
    /// </summary>
    /// <param name="mutate">就地改 pOut；回傳 false 表示不需要寫入（現況已正確）。</param>
    /// <param name="expectKind">
    /// 驗證步驟讀回來的 dmPaperSize 必須等於此值，否則視為驅動拒絕。null ＝ 不驗（見 WriteSnapshot）。
    /// </param>
    private static WriteResult Write(string printerName, Func<IntPtr, Snapshot, bool> mutate, short? expectKind)
    {
        var defaults = new NativeMethods.PRINTER_DEFAULTS { DesiredAccess = NativeMethods.PRINTER_ACCESS_USE };
        if (!NativeMethods.OpenPrinter(printerName, out var hPrinter, ref defaults) || hPrinter == IntPtr.Zero)
            return new WriteResult("driver-rejected", null, Marshal.GetLastWin32Error(), "OpenPrinter failed");

        var pOut = IntPtr.Zero;
        var pIn = IntPtr.Zero;
        try
        {
            // ② 問所需 buffer 大小（含驅動私有的 dmDriverExtra）
            var cb = NativeMethods.DocumentProperties(IntPtr.Zero, hPrinter, printerName, IntPtr.Zero, IntPtr.Zero, 0);
            if (cb <= 0)
                return new WriteResult("driver-rejected", null, Marshal.GetLastWin32Error(), "DocumentProperties(size) failed");

            pOut = Marshal.AllocHGlobal(cb);
            pIn = Marshal.AllocHGlobal(cb);

            // ④ 讀出目前的每使用者預設 DEVMODE
            if (NativeMethods.DocumentProperties(IntPtr.Zero, hPrinter, printerName, pOut, IntPtr.Zero,
                    NativeMethods.DM_OUT_BUFFER) != NativeMethods.IDOK)
                return new WriteResult("driver-rejected", null, Marshal.GetLastWin32Error(), "DocumentProperties(read) failed");

            var prev = new Snapshot(
                Marshal.ReadInt16(pOut, NativeMethods.OffsetPaperSize),
                unchecked((uint)Marshal.ReadInt32(pOut, NativeMethods.OffsetFields)),
                Marshal.ReadInt16(pOut, NativeMethods.OffsetPaperWidth),
                Marshal.ReadInt16(pOut, NativeMethods.OffsetPaperLength));

            // ⑤ 就地改欄位
            if (!mutate(pOut, prev)) return new WriteResult("unchanged", prev, null, null);

            var buf = new byte[cb];
            Marshal.Copy(pOut, buf, 0, cb);
            Marshal.Copy(buf, 0, pIn, cb);

            // ⑥ 讓驅動驗證／正規化（可能自己補上紙匣、算出寬高）
            if (NativeMethods.DocumentProperties(IntPtr.Zero, hPrinter, printerName, pOut, pIn,
                    NativeMethods.DM_IN_BUFFER | NativeMethods.DM_OUT_BUFFER) != NativeMethods.IDOK)
                return new WriteResult("driver-rejected", prev, Marshal.GetLastWin32Error(), "DocumentProperties(merge) failed");

            // 驅動可以合法地忽略我們的要求；讀回來對不上就不要寫進系統。
            if (expectKind is { } want && Marshal.ReadInt16(pOut, NativeMethods.OffsetPaperSize) != want)
                return new WriteResult("driver-rejected", prev, null, "driver ignored dmPaperSize");

            // 驅動的正規化結果也要守同一條不變式。我們送進去的已經是一致的，但驅動有機會自己清掉
            // 旗標卻留著值——而這份 blob 下一步就要成為每使用者預設值，被 Windows 列印 UI 反覆讀取。
            // 旗標沒設＝該欄位「未使用」，把未使用的欄位歸零在定義上是安全的；不像 driver-rejected
            // 那樣整個放棄預選（那會直接把 2026-08-04 的客訴原封不動退回去）。
            NormalizeUnusedPaperFields(pOut);

            // ⑦ 存成每使用者預設（PRINTER_INFO_9 只有一個欄位：pDevMode）
            var pInfo9 = Marshal.AllocHGlobal(IntPtr.Size);
            try
            {
                Marshal.WriteIntPtr(pInfo9, pOut);
                if (!NativeMethods.SetPrinter(hPrinter, 9, pInfo9, 0))
                    return new WriteResult("driver-rejected", prev, Marshal.GetLastWin32Error(), "SetPrinter failed");
            }
            finally
            {
                Marshal.FreeHGlobal(pInfo9);
            }

            return new WriteResult("ok", prev, null, null);
        }
        finally
        {
            if (pOut != IntPtr.Zero) Marshal.FreeHGlobal(pOut);
            if (pIn != IntPtr.Zero) Marshal.FreeHGlobal(pIn);
            NativeMethods.ClosePrinter(hPrinter);
        }
    }

    /// <summary>
    /// 把 <c>dmFields</c> 說「未使用」的紙張欄位歸零，維持 <see cref="DevModePaperFields"/> 的不變式。
    /// </summary>
    private static void NormalizeUnusedPaperFields(IntPtr pDevMode)
    {
        var fields = unchecked((uint)Marshal.ReadInt32(pDevMode, NativeMethods.OffsetFields));

        if ((fields & DevModePaperFields.PaperWidth) == 0)
            Marshal.WriteInt16(pDevMode, NativeMethods.OffsetPaperWidth, 0);
        if ((fields & DevModePaperFields.PaperLength) == 0)
            Marshal.WriteInt16(pDevMode, NativeMethods.OffsetPaperLength, 0);
    }

    // ───────────────────────── 診斷用 ─────────────────────────

    /// <summary>
    /// 印表機名稱的 sha256 前 8 碼。
    /// </summary>
    /// <remarks>
    /// 刻意不回傳原始名稱：現場的印表機名稱經常是 \\PC-王小明\HP LaserJet 1020 這種形式，
    /// 等於同時洩漏使用者姓名與內網主機名，而診斷紀錄是會被使用者整份傳回來的。
    /// 「是不是同一台」用 hash 就答得出來，「是不是虛擬印表機」有獨立旗標。
    /// 見 docs/design/security.md。
    /// </remarks>
    private static string HashPrinterName(string name)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(name));
        return Convert.ToHexString(bytes)[..8].ToLower(CultureInfo.InvariantCulture);
    }
}
