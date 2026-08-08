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
    /// <summary>
    /// 名稱含這些字樣的一律視為虛擬印表機，直接跳過預選（見 <see cref="PrinterFormPolicy"/>）。
    /// </summary>
    /// <remarks>
    /// 從前這只是一格診斷旗標，靠「虛擬印表機不會有我們的自訂表單」間接擋住寫入。
    /// 2026-08-06 起改成明確的 skip：那是**推論**不是保證，而賭錯的代價是去改使用者的 PDF 輸出設定。
    /// </remarks>
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
        var decision = PrinterFormPolicy.Decide(match.Match, isVirtual);

        // 不寫入的三條路徑一律在這裡結束：Prev 是 null ⇒ 呼叫端不會寫還原 journal ⇒
        // 這次列印完全沒碰過那份共用的每使用者預設 DEVMODE。理由見 PrinterFormPolicy。
        if (decision != PrinterFormPolicy.FormApplyDecision.Apply)
        {
            var isMismatch = decision == PrinterFormPolicy.FormApplyDecision.SkipSizeMismatch;
            return new Outcome
            {
                Result = PrinterFormPolicy.ToResult(decision),
                Form = match.FormName,
                // 刻意不回 Kind：它的語意是「我們設進去的表單 ID」，沒寫入就不該有值。
                MismatchWidthMm = isMismatch ? Math.Round(match.WidthDiffMm, 2) : null,
                MismatchHeightMm = isMismatch ? Math.Round(match.HeightDiffMm, 2) : null,
                PrinterHash = hash,
                Virtual = isVirtual,
            };
        }

        var form = match.Form!.Value;
        var write = WritePaperSize(printerName, form.Kind);

        return new Outcome
        {
            Result = write.Result == "ok" ? "exact" : write.Result,
            Form = match.FormName,
            Kind = form.Kind,
            Prev = write.Prev,
            Printer = write.Prev is null ? null : printerName,
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
        }, expectKind: kind, preflight: true);

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
        expectKind: (snap.Fields & DevModePaperFields.PaperSize) != 0 ? snap.Kind : null,
        // 還原刻意**不**做 PrintTicket 預檢：還原寫回去的是使用者自己原本的值，而預檢失敗會讓
        // 我們選的表單永遠留在他機器上——那正是預檢要防的事。還原路徑寧可寫，也不要卡住。
        preflight: false);

    /// <summary>
    /// OpenPrinter → DocumentProperties(讀) → mutate → DocumentProperties(驗證) → SetPrinter(Level 9)。
    /// </summary>
    /// <param name="mutate">就地改 pOut；回傳 false 表示不需要寫入（現況已正確）。</param>
    /// <param name="expectKind">
    /// 驗證步驟讀回來的 dmPaperSize 必須等於此值，否則視為驅動拒絕。null ＝ 不驗（見 WriteSnapshot）。
    /// </param>
    /// <param name="preflight">
    /// 寫入前是否先跑 DEVMODE → PrintTicket 轉換預檢（見 <see cref="PrintTicketPreflight"/>）。
    /// </param>
    private static WriteResult Write(
        string printerName, Func<IntPtr, Snapshot, bool> mutate, short? expectKind, bool preflight)
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

            // ⑥' 最後一道閘門：自己先做一次列印 UI 會做的 DEVMODE → PrintTicket 轉換，轉不過就不寫。
            // DevModePaperFields 只保證「沒違反已知的不變式」，驅動吃不吃終究是猜的；這裡把猜改成問。
            // 2026-08-08 KYOCERA PA2000 客訴的處置，理由見 PrintTicketPreflight。
            if (preflight)
            {
                var check = Preflight(printerName, pOut);
                if (!PrintTicketPreflight.MayWrite(check.Outcome))
                {
                    // Prev 一律回 null：什麼都沒寫 ⇒ 呼叫端不該留還原 journal。
                    return new WriteResult(PrintTicketPreflight.ToResult(check.Outcome), null, null, check.Error);
                }
            }

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
    /// 把改好的 DEVMODE 丟進 <c>PTConvertDevModeToPrintTicket</c> 走一次——**這正是 Windows 列印 UI
    /// 在開啟印表機設定時會做的轉換**，失敗時使用者看到的就是「您的印表機已發生未預期的設定問題
    /// 0x80010105」（<c>RPC_E_SERVERFAULT</c>）。
    /// </summary>
    /// <remarks>
    /// 判定與 fail-closed 的理由住在 <see cref="PrintTicketPreflight"/>（平台中立、測得到）；
    /// 這裡只負責把兩段 HRESULT 撈出來。任何例外（缺 dll、缺 entry point）一律吞掉並歸為
    /// <c>Unavailable</c>——預檢是為了保護使用者的驅動設定，它自己絕不能變成新的失敗來源。
    /// </remarks>
    private static (PrintTicketPreflight.PreflightOutcome Outcome, string? Error) Preflight(
        string printerName, IntPtr pDevMode)
    {
        int? openHr = null;
        int? convertHr = null;
        string? error = null;
        var provider = IntPtr.Zero;
        var stream = IntPtr.Zero;

        try
        {
            openHr = NativeMethods.PTOpenProvider(printerName, NativeMethods.PT_PROVIDER_VERSION, out provider);
            if (openHr >= 0)
            {
                var streamHr = NativeMethods.CreateStreamOnHGlobal(IntPtr.Zero, true, out stream);
                if (streamHr >= 0)
                {
                    // cbDevmode ＝ 公開部分 + 驅動私有部分。用 DocumentProperties 回報的 buffer 大小
                    // 會偏大（它可能含對齊填充），而這支 API 要的是 DEVMODE 自己宣告的長度。
                    var cb = (uint)((ushort)Marshal.ReadInt16(pDevMode, NativeMethods.OffsetSize)
                                    + (ushort)Marshal.ReadInt16(pDevMode, NativeMethods.OffsetDriverExtra));

                    convertHr = NativeMethods.PTConvertDevModeToPrintTicket(
                        provider, cb, pDevMode, NativeMethods.PT_JOB_SCOPE, stream);
                }
                else
                {
                    error = $"CreateStreamOnHGlobal 0x{streamHr:x8}";
                }
            }
        }
        catch (Exception e)
        {
            error = e.Message;
        }
        finally
        {
            if (stream != IntPtr.Zero) Marshal.Release(stream);
            if (provider != IntPtr.Zero)
            {
                try { NativeMethods.PTCloseProvider(provider); }
                catch (DllNotFoundException) { /* 開得起來就關得掉，這裡只是防禦 */ }
                catch (EntryPointNotFoundException) { }
            }
        }

        var outcome = PrintTicketPreflight.Classify(openHr, convertHr);
        error ??= outcome switch
        {
            PrintTicketPreflight.PreflightOutcome.Rejected => $"PTConvertDevModeToPrintTicket 0x{convertHr:x8}",
            PrintTicketPreflight.PreflightOutcome.Unavailable => $"PTOpenProvider 0x{openHr:x8}",
            _ => null,
        };

        return (outcome, error);
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
