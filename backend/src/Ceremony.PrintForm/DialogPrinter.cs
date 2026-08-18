using System.Drawing.Printing;
using System.Runtime.InteropServices;
using Ceremony.Domain.Reports;
using static Ceremony.PrintForm.PrintDialogNative;

namespace Ceremony.PrintForm;

/// <summary>
/// 決策 11 的送印路徑：**自己帶一份 DEVMODE 進舊版列印對話框，然後自己把 PDF 畫到紙上**。
/// </summary>
/// <remarks>
/// <para>
/// 這是舊系統 <c>SignupForm.cs:1764-1799</c> ＋ <c>:1732-1762</c> 的等價物，差別只在
/// 內容來源是 QuestPDF 產的 PDF（經 PDFium）而不是 RDLC 產的 EMF。
/// </para>
/// <para>
/// <b>不變式（改這個檔之前先讀）：這條路徑不得寫入任何共用系統狀態。</b>
/// 沒有 <c>SetPrinter</c>、沒有還原 journal、沒有 <c>prntvpt</c> 預檢、沒有黑名單。
/// 決策 9a～9d 整個系列存在的唯一理由是「我們不擁有那個列印工作，只能去改機器的共用設定」；
/// 擁有之後，那份共用狀態一格都不必碰。在這裡加一行 <c>SetPrinter</c> 不是多一層保險，是純風險。
/// </para>
/// </remarks>
internal static class DialogPrinter
{
    /// <summary>DEVMODE 的來源。三選一而不是猜一個寫死——理由見 <see cref="PrinterInfo2Prefix"/>。</summary>
    internal enum DevModeSource
    {
        /// <summary>GetPrinter Level 2 的 pDevMode。預設，繞開每使用者預設那一份。</summary>
        Printer,

        /// <summary>DocumentProperties(DM_OUT_BUFFER)＝每使用者預設，也就是現場出問題的那一份。</summary>
        User,

        /// <summary>不給 hDevMode，讓對話框自己去取（＝Chromium 今天的行為，對照組用）。</summary>
        None,
    }

    internal sealed record Options(
        string PdfPath,
        IntPtr Owner,
        string? ReportType,
        bool NoForm,
        DevModeSource Source,
        PrintScalePolicy.ScaleMode Scale,
        string? JobName);

    /// <summary>一行 NDJSON 的內容。呼叫端負責序列化。</summary>
    internal sealed record Line(string Event, IReadOnlyDictionary<string, object?> Fields);

    /// <summary>
    /// 跑完整條路徑。<paramref name="emit"/> 會被呼叫兩次以上：
    /// <c>dialog-shown</c>（**在對話框開啟之前** flush）與最後一行帶 <c>result</c> 的。
    /// </summary>
    /// <remarks>
    /// 第一行必須在 <c>PrintDlg</c> 之前送出，因為那支是 modal 的——呼叫端要靠它
    /// 「對話框已經在螢幕上」就 resolve UI，**絕不能等到印完**。
    /// 這是 blueprint 決策 8 的教訓（<c>webContents.print</c> 的 callback 有時不回來，
    /// UI 永久卡在「列印中」）在新路徑上的守法。
    /// </remarks>
    internal static Dictionary<string, object?> Run(Options opt, Action<Line> emit)
    {
        var fields = new Dictionary<string, object?>();

        var settings = new PrinterSettings();
        if (!settings.IsValid)
        {
            fields["result"] = PrintDialogResults.NoDefaultPrinter;
            return fields;
        }

        var printerName = settings.PrinterName;
        fields["devmodeSource"] = opt.Source.ToString().ToLowerInvariant();

        // ① 紙張預選：與決策 9 完全相同的比對，但命中之後改的是**我們手上這份 copy**。
        IntPtr hDevMode = IntPtr.Zero;
        if (opt.Source != DevModeSource.None)
        {
            hDevMode = BuildDevMode(printerName, opt, settings, fields);
        }

        // ② 對話框開啟前先把這一行送出去（見 remarks）。
        emit(new Line("dialog-shown", new Dictionary<string, object?>(fields)));

        var pd = new PRINTDLG
        {
            lStructSize = (uint)Marshal.SizeOf<PRINTDLG>(),
            hwndOwner = opt.Owner,
            hDevMode = hDevMode,
            // 刻意**不設** PD_NOPAGENUMS：頁面範圍是硬需求（卡紙續印，見決策 4）。
            Flags = PD_RETURNDC | PD_USEDEVMODECOPIESANDCOLLATE | PD_NOSELECTION | PD_HIDEPRINTTOFILE,
            nFromPage = 1,
            nToPage = 1,
            nMinPage = 1,
            nMaxPage = ushort.MaxValue,
            nCopies = 1,
        };

        bool ok = PrintDlg(ref pd);
        uint dlgErr = ok ? 0 : CommDlgExtendedError();

        try
        {
            if (!ok)
            {
                // CommDlgExtendedError()==0 ⇒ 使用者自己取消。**這不是錯誤**，呼叫端不得顯示紅字。
                fields["result"] = dlgErr == 0 ? PrintDialogResults.Cancelled : PrintDialogResults.DialogFailed;
                if (dlgErr != 0) fields["win32"] = (int)dlgErr;
                return fields;
            }

            fields["chosenPrinter"] = ReadChosenDevice(pd.hDevNames) is { } n && n != printerName ? "other" : "default";
            fields["copies"] = pd.nCopies;

            return Render(pd, opt, fields);
        }
        finally
        {
            if (pd.hDC != IntPtr.Zero) DeleteDC(pd.hDC);
            FreeIf(pd.hDevMode);
            FreeIf(pd.hDevNames);
        }
    }

    // ───────────────────────── DEVMODE ─────────────────────────

    private static IntPtr BuildDevMode(
        string printerName, Options opt, PrinterSettings settings, Dictionary<string, object?> fields)
    {
        var defaults = new NativeMethods.PRINTER_DEFAULTS { DesiredAccess = NativeMethods.PRINTER_ACCESS_USE };
        if (!NativeMethods.OpenPrinter(printerName, out var hPrinter, ref defaults))
        {
            fields["devmodeError"] = Marshal.GetLastWin32Error();
            return IntPtr.Zero;
        }

        IntPtr owned = IntPtr.Zero;
        try
        {
            IntPtr src = opt.Source == DevModeSource.User
                ? ReadUserDefault(hPrinter, printerName, ref owned)
                : ReadPrinterDefault(hPrinter, ref owned);

            if (src == IntPtr.Zero) return IntPtr.Zero;

            int total = Marshal.ReadInt16(src, NativeMethods.OffsetSize)
                        + Marshal.ReadInt16(src, NativeMethods.OffsetDriverExtra);
            if (total <= 0) return IntPtr.Zero;

            var hMem = GlobalAlloc(GMEM_MOVEABLE, (UIntPtr)(uint)total);
            if (hMem == IntPtr.Zero) return IntPtr.Zero;

            var dst = GlobalLock(hMem);
            try
            {
                var tmp = new byte[total];
                Marshal.Copy(src, tmp, 0, total);
                Marshal.Copy(tmp, 0, dst, total);

                if (!opt.NoForm) SelectForm(dst, opt.ReportType, settings, fields);
            }
            finally
            {
                GlobalUnlock(hMem);
            }

            return hMem;
        }
        finally
        {
            if (owned != IntPtr.Zero) Marshal.FreeHGlobal(owned);
            NativeMethods.ClosePrinter(hPrinter);
        }
    }

    private static IntPtr ReadUserDefault(IntPtr hPrinter, string printerName, ref IntPtr owned)
    {
        int size = NativeMethods.DocumentProperties(IntPtr.Zero, hPrinter, printerName, IntPtr.Zero, IntPtr.Zero, 0);
        if (size <= 0) return IntPtr.Zero;

        owned = Marshal.AllocHGlobal(size);
        return NativeMethods.DocumentProperties(
            IntPtr.Zero, hPrinter, printerName, owned, IntPtr.Zero, NativeMethods.DM_OUT_BUFFER) == NativeMethods.IDOK
            ? owned
            : IntPtr.Zero;
    }

    private static IntPtr ReadPrinterDefault(IntPtr hPrinter, ref IntPtr owned)
    {
        GetPrinter(hPrinter, 2, IntPtr.Zero, 0, out int needed);
        if (needed <= 0) return IntPtr.Zero;

        owned = Marshal.AllocHGlobal(needed);
        if (!GetPrinter(hPrinter, 2, owned, needed, out _)) return IntPtr.Zero;

        return Marshal.ReadIntPtr(owned, OffsetInfo2DevMode);
    }

    /// <summary>
    /// 舊系統 <c>SignupForm.cs:1770-1787</c> 那個比對迴圈的等價物——但比它嚴格。
    /// </summary>
    /// <remarks>
    /// 沿用 <see cref="PrinterFormMatcher"/>（±0.5mm 容差）與 <see cref="PrinterFormPolicy"/>
    /// （只有 Exact ＋實體印表機才選）。舊系統是純字串比對、尺寸不符照樣用，而且**靜默降級無提示**。
    /// 保留 9b 的保守規則有兩個理由：一是「靜默套一張已知不對的紙只會讓現場更晚發現表單建錯」，
    /// 二是不改就不必重驗。
    /// </remarks>
    private static void SelectForm(
        IntPtr pDevMode, string? reportType, PrinterSettings settings, Dictionary<string, object?> fields)
    {
        if (string.IsNullOrWhiteSpace(reportType)) return;
        if (!ReportPageSizes.TryGet(reportType, out var page)) return;

        fields["formTarget"] = page.FormName;

        var forms = PrinterFormApplier.ReadDriverForms(settings);
        var match = PrinterFormMatcher.Match(reportType, forms);
        var decision = PrinterFormPolicy.Decide(match.Match, IsVirtual(settings));

        if (decision != PrinterFormPolicy.FormApplyDecision.Apply)
        {
            fields["formResult"] = PrinterFormPolicy.ToResult(decision);
            return;
        }

        // 旗標與值必須同進退（決策 9a 的不變式，SSoT 在 Domain）。
        var kind = match.Form!.Value.Kind;
        uint cur = (uint)Marshal.ReadInt32(pDevMode, NativeMethods.OffsetFields);
        Marshal.WriteInt16(pDevMode, NativeMethods.OffsetPaperSize, kind);
        Marshal.WriteInt16(pDevMode, NativeMethods.OffsetPaperLength, 0);
        Marshal.WriteInt16(pDevMode, NativeMethods.OffsetPaperWidth, 0);
        Marshal.WriteInt32(pDevMode, NativeMethods.OffsetFields, (int)DevModePaperFields.ForFormSelection(cur));

        fields["formResult"] = "exact";
        fields["formKind"] = kind;
    }

    private static bool IsVirtual(PrinterSettings settings)
    {
        var n = settings.PrinterName;
        return n.Contains("PDF", StringComparison.OrdinalIgnoreCase)
               || n.Contains("XPS", StringComparison.OrdinalIgnoreCase)
               || n.Contains("OneNote", StringComparison.OrdinalIgnoreCase)
               || n.Contains("Fax", StringComparison.OrdinalIgnoreCase);
    }

    // ───────────────────────── 送印 ─────────────────────────

    private static Dictionary<string, object?> Render(PRINTDLG pd, Options opt, Dictionary<string, object?> fields)
    {
        var hdc = pd.hDC;
        if (hdc == IntPtr.Zero)
        {
            fields["result"] = PrintDialogResults.DialogFailed;
            fields["error"] = "PD_RETURNDC 沒有回傳 DC";
            return fields;
        }

        var metrics = ReadMetrics(hdc, fields);

        PdfiumNative.InitLibrary();
        using var file = new PdfFileAccess(opt.PdfPath);
        var doc = file.Load();
        if (doc == IntPtr.Zero)
        {
            fields["result"] = PrintDialogResults.RenderFailed;
            fields["error"] = $"pdfium={PdfiumNative.GetLastError()}";
            PdfiumNative.DestroyLibrary();
            return fields;
        }

        try
        {
            int pageCount = PdfiumNative.GetPageCount(doc);
            var pages = PrintPageRange.Resolve(pd.Flags, pd.nFromPage, pd.nToPage, pageCount);
            fields["pages"] = pages.Count;
            fields["pageCount"] = pageCount;
            fields["range"] = pages.Count == 0 ? null : $"{pages[0]}-{pages[^1]}";

            if (pages.Count == 0)
            {
                fields["result"] = PrintDialogResults.RenderFailed;
                fields["error"] = "PDF 沒有任何頁";
                return fields;
            }

            var di = new DOCINFO
            {
                cbSize = Marshal.SizeOf<DOCINFO>(),
                lpszDocName = opt.JobName ?? "寶覺寺法會報名系統",
            };

            // StartDoc 成功時回傳的**就是 spooler 的 job id**——記下來，現場才有辦法把
            // 「我們送出去了」與 Windows 列印佇列裡的那一筆對起來。沒有它時，
            // 「按了列印，印表機沒有反應」在紀錄上與「我們根本沒送」長得一樣（2026-08-18 客訴）。
            int jobId = StartDoc(hdc, ref di);
            if (jobId <= 0)
            {
                fields["result"] = PrintDialogResults.DriverRejected;
                fields["win32"] = Marshal.GetLastWin32Error();
                return fields;
            }

            fields["jobId"] = jobId;

            foreach (var pageNo in pages)
            {
                if (!RenderOne(hdc, doc, pageNo - 1, metrics, opt.Scale, fields))
                {
                    AbortDoc(hdc);
                    return fields;
                }
            }

            if (EndDoc(hdc) <= 0)
            {
                fields["result"] = PrintDialogResults.DriverRejected;
                fields["win32"] = Marshal.GetLastWin32Error();
                return fields;
            }

            fields["result"] = PrintDialogResults.Printed;
            return fields;
        }
        finally
        {
            PdfiumNative.CloseDocument(doc);
            PdfiumNative.DestroyLibrary();
        }
    }

    private static bool RenderOne(
        IntPtr hdc, IntPtr doc, int pageIndex, PrintScalePolicy.DeviceMetrics metrics,
        PrintScalePolicy.ScaleMode scale, Dictionary<string, object?> fields)
    {
        var page = PdfiumNative.LoadPage(doc, pageIndex);
        if (page == IntPtr.Zero)
        {
            fields["result"] = PrintDialogResults.RenderFailed;
            fields["error"] = $"第 {pageIndex + 1} 頁載入失敗，pdfium={PdfiumNative.GetLastError()}";
            return false;
        }

        try
        {
            var dest = PrintScalePolicy.Compute(
                PdfiumNative.GetPageWidth(page), PdfiumNative.GetPageHeight(page), metrics, scale);

            if (dest.IsEmpty)
            {
                fields["result"] = PrintDialogResults.RenderFailed;
                fields["error"] = $"第 {pageIndex + 1} 頁算不出目標矩形";
                return false;
            }

            // 只記第一頁——同一份 PDF 的每頁尺寸相同，記 5000 行沒有意義。
            fields.TryAdd("destRect", $"{dest.X},{dest.Y},{dest.Width}x{dest.Height}");

            if (StartPage(hdc) <= 0)
            {
                fields["result"] = PrintDialogResults.DriverRejected;
                fields["win32"] = Marshal.GetLastWin32Error();
                return false;
            }

            PdfiumNative.RenderPage(
                hdc, page, dest.X, dest.Y, dest.Width, dest.Height, 0,
                PdfiumNative.FPDF_ANNOT | PdfiumNative.FPDF_PRINTING);

            if (EndPage(hdc) <= 0)
            {
                fields["result"] = PrintDialogResults.DriverRejected;
                fields["win32"] = Marshal.GetLastWin32Error();
                return false;
            }

            return true;
        }
        finally
        {
            PdfiumNative.ClosePage(page);
        }
    }

    /// <summary>
    /// 讀 <c>GetDeviceCaps</c> 並**全部寫進診斷紀錄**。
    /// </summary>
    /// <remarks>
    /// 今天現場回報「印歪」只能靠土法對照；有了這幾個數字，第一次可以在辦公室算出偏移量。
    /// </remarks>
    private static PrintScalePolicy.DeviceMetrics ReadMetrics(IntPtr hdc, Dictionary<string, object?> fields)
    {
        var m = new PrintScalePolicy.DeviceMetrics(
            GetDeviceCaps(hdc, HORZRES),
            GetDeviceCaps(hdc, VERTRES),
            GetDeviceCaps(hdc, PHYSICALWIDTH),
            GetDeviceCaps(hdc, PHYSICALHEIGHT),
            GetDeviceCaps(hdc, PHYSICALOFFSETX),
            GetDeviceCaps(hdc, PHYSICALOFFSETY),
            GetDeviceCaps(hdc, LOGPIXELSX),
            GetDeviceCaps(hdc, LOGPIXELSY));

        int tech = GetDeviceCaps(hdc, TECHNOLOGY);
        fields["dpi"] = $"{m.DpiX}x{m.DpiY}";
        fields["printablePx"] = $"{m.PrintableWidthPx}x{m.PrintableHeightPx}";
        fields["physicalPx"] = $"{m.PhysicalWidthPx}x{m.PhysicalHeightPx}";
        fields["offsetPx"] = $"{m.OffsetXPx}x{m.OffsetYPx}";

        // 不是 DT_RASPRINTER 時 PDFium 走不到 GDI 印表機裝置驅動 ⇒ 會退化成整頁點陣圖。
        // 不擋（照樣印得出來），但要看得見。
        if (tech != DT_RASPRINTER) fields["technology"] = tech;

        return m;
    }

    private static void FreeIf(IntPtr hMem)
    {
        if (hMem != IntPtr.Zero) GlobalFree(hMem);
    }

    /// <summary>
    /// 用 <c>FPDF_LoadCustomDocument</c> 的回呼讀檔——理由見 <see cref="PdfiumNative.LoadCustomDocument"/>。
    /// </summary>
    private sealed class PdfFileAccess : IDisposable
    {
        private readonly FileStream _stream;
        private readonly PdfiumNative.GetBlockCallback _callback;   // ⚠️ 必須持有，否則會被 GC 回收
        private PdfiumNative.FPDF_FILEACCESS _access;

        internal PdfFileAccess(string path)
        {
            _stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            _callback = GetBlock;
            _access = new PdfiumNative.FPDF_FILEACCESS
            {
                m_FileLen = (uint)_stream.Length,
                m_GetBlock = Marshal.GetFunctionPointerForDelegate(_callback),
                m_Param = IntPtr.Zero,
            };
        }

        internal IntPtr Load() => PdfiumNative.LoadCustomDocument(ref _access, null);

        private int GetBlock(IntPtr param, uint position, IntPtr buffer, uint size)
        {
            try
            {
                _stream.Seek(position, SeekOrigin.Begin);
                var tmp = new byte[size];
                int read = 0;
                while (read < size)
                {
                    int n = _stream.Read(tmp, read, (int)size - read);
                    if (n <= 0) return 0;
                    read += n;
                }

                Marshal.Copy(tmp, 0, buffer, (int)size);
                return 1;
            }
            catch
            {
                return 0;
            }
        }

        public void Dispose()
        {
            _stream.Dispose();
            GC.KeepAlive(_callback);
        }
    }
}
