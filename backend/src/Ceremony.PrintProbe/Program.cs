using System.Drawing.Printing;
using System.Runtime.InteropServices;
using System.Text;
using static Ceremony.PrintProbe.NativeMethods;

namespace Ceremony.PrintProbe;

/// <summary>
/// Phase 0 探針：在現場那台印表機上回答**一個** go/no-go 問題——
/// comdlg32 的**舊版**列印對話框，會不會也噴「您的印表機已發生未預期的設定問題．0x80010105」。
/// </summary>
/// <remarks>
/// <para>
/// 背景：2026-08-15 的現場證據（reference/print-20260815.log ＋ print.mov ＋ print.png）已排除
/// 我們的寫入——兩次列印都是 <c>skipped-disabled</c>、無殘留還原檔、且最後一個寫那份每使用者預設
/// DEVMODE 的是 Windows 自己（客戶在 Kyocera 的列印喜好設定選了「資料卡」按確定），之後照樣噴，
/// 而且**列印按鈕是灰的**（＝完全印不出來）。
/// </para>
/// <para>
/// 假設：出錯的是**新版對話框做的 DEVMODE → PrintTicket 轉換**（v4 驅動的轉換由廠商自己實作）。
/// 舊系統不會噴，是因為它走 comdlg32 舊版對話框、而且自己帶一份 hDevMode 進去，
/// **從來不需要那段轉換**（見 docs/gotchas.md「復刻舊功能時先問舊系統寫到哪」）。
/// 這支探針就是去證實或推翻它。
/// </para>
/// <para>
/// ⚠️ 這支程式**完全不寫入任何系統狀態**：沒有 <c>SetPrinter</c>、沒有登錄檔、
/// 不改每使用者預設。跑幾次都不會讓現況變得更糟。
/// </para>
/// <para>形狀刻意做成「寺方可以自己雙擊執行」：全中文、有問有答、自動寫一份紀錄檔在同一個資料夾。</para>
/// </remarks>
internal static class Program
{
    private static readonly StringBuilder Transcript = new();

    /// <summary>DEVMODE 從哪裡來。這三個值就是計畫裡 <c>--devmode-source</c> 的三個選項。</summary>
    private enum DevModeSource
    {
        /// <summary>OpenPrinter + GetPrinter Level 2 的 pDevMode。繞開「每使用者預設」那一份。</summary>
        Printer,

        /// <summary>DocumentProperties(DM_OUT_BUFFER)＝每使用者預設，也就是今天出錯的那一份。</summary>
        User,

        /// <summary>不給 hDevMode，讓對話框自己去取（＝Chromium 今天的行為）。</summary>
        None,
    }

    private static int Main()
    {
        Console.OutputEncoding = Encoding.UTF8;

        Say("========================================");
        Say(" 寶覺寺法會報名系統 — 列印診斷工具");
        Say("========================================");
        Say("");
        Say("這個工具會叫出列印視窗做測試，**不會更改您電腦的任何設定**。");
        Say("整個過程大約 2 分鐘，最後會在同一個資料夾產生一份紀錄檔，請回傳給我們。");
        Say("");

        try
        {
            Run();
        }
        catch (Exception ex)
        {
            Say($"[例外] {ex.GetType().Name}: {ex.Message}");
            Say(ex.StackTrace ?? "");
        }

        var path = WriteTranscript();
        Say("");
        Say("========================================");
        Say($"紀錄檔已存到：{path}");
        Say("請把這個檔案回傳給我們。");
        Say("========================================");
        Say("");
        Console.Write("按 Enter 鍵結束…");
        Console.ReadLine();
        return 0;
    }

    private static void Run()
    {
        Say($"時間　　　：{DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        Say($"Windows　 ：{Environment.OSVersion.VersionString}");
        Say($"64 位元　 ：行程 {Environment.Is64BitProcess} / 系統 {Environment.Is64BitOperatingSystem}");
        Say($"電腦名稱　：{Environment.MachineName}");
        Say("");

        var settings = new PrinterSettings();
        var defaultPrinter = settings.PrinterName;
        Say($"預設印表機：{defaultPrinter}");
        Say("已安裝的印表機：");
        foreach (string? p in PrinterSettings.InstalledPrinters)
        {
            Say($"  - {p}{(p == defaultPrinter ? "  ← 預設" : "")}");
        }
        Say("");

        DescribePrinter(defaultPrinter);
        DescribeForms(settings);

        // 三種 DEVMODE 來源依序試。第一個成功就停——後面兩個只是為了在失敗時多蒐集資訊。
        foreach (var source in new[] { DevModeSource.Printer, DevModeSource.User, DevModeSource.None })
        {
            if (TryDialog(defaultPrinter, source)) return;
            Say("");
        }

        Say("三種模式都沒有成功送印。請把紀錄檔回傳給我們。");
    }

    // ───────────────────────── 印表機／驅動資訊 ─────────────────────────

    /// <summary>
    /// 印出驅動名稱、連接埠與**驅動類型（cVersion）**。
    /// </summary>
    /// <remarks>
    /// <c>cVersion</c> 是本次診斷最大的推論缺口：「PA2000 GX 是 v4 驅動」目前只從名稱推得，
    /// 沒有直接證據。3 ＝ v3（設定本體是 DEVMODE，不需要那段會出錯的轉換）、
    /// 4 ＝ v4（設定本體是 PrintTicket，DEVMODE 只是相容層）。
    /// </remarks>
    private static void DescribePrinter(string printerName)
    {
        var defaults = new PRINTER_DEFAULTS { DesiredAccess = PRINTER_ACCESS_USE };
        if (!OpenPrinter(printerName, out var hPrinter, ref defaults))
        {
            Say($"[警告] OpenPrinter 失敗，win32={Marshal.GetLastWin32Error()}");
            return;
        }

        try
        {
            GetPrinter(hPrinter, 2, IntPtr.Zero, 0, out int needed);
            if (needed > 0)
            {
                var buf = Marshal.AllocHGlobal(needed);
                try
                {
                    if (GetPrinter(hPrinter, 2, buf, needed, out _))
                    {
                        Say($"驅動名稱　：{ReadStringField<PrinterInfo2Prefix>(buf, nameof(PrinterInfo2Prefix.pDriverName))}");
                        Say($"連接埠　　：{ReadStringField<PrinterInfo2Prefix>(buf, nameof(PrinterInfo2Prefix.pPortName))}");
                    }
                }
                finally { Marshal.FreeHGlobal(buf); }
            }

            GetPrinterDriver(hPrinter, null, 2, IntPtr.Zero, 0, out int dNeeded);
            if (dNeeded > 0)
            {
                var dbuf = Marshal.AllocHGlobal(dNeeded);
                try
                {
                    if (GetPrinterDriver(hPrinter, null, 2, dbuf, dNeeded, out _))
                    {
                        var info = Marshal.PtrToStructure<DriverInfo2Prefix>(dbuf);
                        var kind = info.cVersion switch
                        {
                            4 => "v4（設定本體是 PrintTicket ← 就是會出問題的那一類）",
                            3 => "v3（設定本體是 DEVMODE）",
                            _ => "其他",
                        };
                        Say($"驅動類型　：cVersion = {info.cVersion}　{kind}");
                        Say($"驅動檔案　：{Marshal.PtrToStringUni(info.pDriverPath)}");
                    }
                }
                finally { Marshal.FreeHGlobal(dbuf); }
            }
        }
        finally { ClosePrinter(hPrinter); }

        Say("");
    }

    /// <summary>列出驅動的紙張表單，確認「資料卡」等自訂表單在不在、尺寸對不對。</summary>
    private static void DescribeForms(PrinterSettings settings)
    {
        Say("驅動的紙張表單（名稱 / 寬×高，單位 1/100 吋）：");
        try
        {
            foreach (PaperSize ps in settings.PaperSizes)
            {
                Say($"  - {ps.PaperName}  {ps.Width}×{ps.Height}  (kind={ps.Kind}, raw={ps.RawKind})");
            }
        }
        catch (Exception ex)
        {
            Say($"  [讀取失敗] {ex.Message}");
        }
        Say("");
    }

    // ───────────────────────── 對話框 ─────────────────────────

    private static bool TryDialog(string printerName, DevModeSource source)
    {
        var label = source switch
        {
            DevModeSource.Printer => "模式 1：使用印表機本身的設定（繞開每使用者預設）",
            DevModeSource.User => "模式 2：使用「列印喜好設定」裡那一份（今天出錯的那一份）",
            _ => "模式 3：不指定設定，讓 Windows 自己決定（＝目前系統的行為）",
        };

        Say("----------------------------------------");
        Say(label);
        Say("----------------------------------------");
        Say("接下來會跳出「列印」視窗。請看清楚兩件事，稍後我會問您：");
        Say("  (1) 視窗裡有沒有**紅色的錯誤訊息**？");
        Say("  (2)「列印」按鈕是不是**灰色不能按**？");
        Say("");
        Say("如果可以按，請按「列印」，會印出一張測試頁；不能按就按「取消」。");
        Console.Write("準備好了請按 Enter…");
        Console.ReadLine();

        IntPtr hDevMode = source == DevModeSource.None ? IntPtr.Zero : BuildDevMode(printerName, source);
        if (source != DevModeSource.None && hDevMode == IntPtr.Zero)
        {
            Say("[略過] 這個模式取不到設定資料。");
            return false;
        }

        var pd = new PRINTDLG
        {
            lStructSize = (uint)Marshal.SizeOf<PRINTDLG>(),
            hDevMode = hDevMode,
            Flags = PD_RETURNDC | PD_USEDEVMODECOPIESANDCOLLATE | PD_NOSELECTION | PD_HIDEPRINTTOFILE | PD_ALLPAGES,
            // 測試頁固定一頁。刻意給 1..1 而不是留 0：nMinPage/nMaxPage 全 0 時「頁面範圍」那組
            // 控制項的狀態由對話框自行決定，會多一個與本次診斷無關的變數。
            nFromPage = 1,
            nToPage = 1,
            nMinPage = 1,
            nMaxPage = 1,
            nCopies = 1,
        };

        bool ok = PrintDlg(ref pd);
        uint err = ok ? 0 : CommDlgExtendedError();

        if (!ok)
        {
            if (err == 0)
            {
                Say("結果：使用者取消（或對話框被關閉）。");
                AskAndRecord("視窗裡有沒有出現紅色的錯誤訊息？(y/n) ", "有錯誤訊息");
                AskAndRecord("「列印」按鈕是不是灰色不能按？(y/n) ", "列印鈕是灰的");
            }
            else
            {
                Say($"結果：**對話框本身失敗**，CommDlgExtendedError=0x{err:X4}");
            }
            FreeIf(pd.hDevMode);
            FreeIf(pd.hDevNames);
            return false;
        }

        Say("結果：對話框回傳「確定」——**沒有被擋下**。");
        Say($"使用者選的印表機：{ReadChosenDevice(pd.hDevNames)}");

        bool printed = false;
        try
        {
            printed = PrintTestPage(pd.hDC, label);
        }
        finally
        {
            if (pd.hDC != IntPtr.Zero) DeleteDC(pd.hDC);
            FreeIf(pd.hDevMode);
            FreeIf(pd.hDevNames);
        }

        if (printed)
        {
            AskAndRecord("印表機有沒有印出一張測試頁？(y/n) ", "實際出紙");
        }
        return printed;
    }

    /// <summary>把 DEVMODE 複製進一塊 HGLOBAL（對話框要的是 handle，而且它會接手擁有權）。</summary>
    private static IntPtr BuildDevMode(string printerName, DevModeSource source)
    {
        var defaults = new PRINTER_DEFAULTS { DesiredAccess = PRINTER_ACCESS_USE };
        if (!OpenPrinter(printerName, out var hPrinter, ref defaults))
        {
            Say($"[警告] OpenPrinter 失敗，win32={Marshal.GetLastWin32Error()}");
            return IntPtr.Zero;
        }

        IntPtr owned = IntPtr.Zero;   // 我們自己配置、要負責釋放的暫存區
        try
        {
            IntPtr src;
            if (source == DevModeSource.User)
            {
                int size = DocumentProperties(IntPtr.Zero, hPrinter, printerName, IntPtr.Zero, IntPtr.Zero, 0);
                if (size <= 0) { Say($"[警告] DocumentProperties 回 {size}"); return IntPtr.Zero; }
                owned = Marshal.AllocHGlobal(size);
                if (DocumentProperties(IntPtr.Zero, hPrinter, printerName, owned, IntPtr.Zero, DM_OUT_BUFFER) != 1)
                {
                    Say("[警告] DocumentProperties(DM_OUT_BUFFER) 失敗");
                    return IntPtr.Zero;
                }
                src = owned;
            }
            else
            {
                GetPrinter(hPrinter, 2, IntPtr.Zero, 0, out int needed);
                if (needed <= 0) { Say("[警告] GetPrinter 取不到大小"); return IntPtr.Zero; }
                owned = Marshal.AllocHGlobal(needed);
                if (!GetPrinter(hPrinter, 2, owned, needed, out _)) { Say("[警告] GetPrinter 失敗"); return IntPtr.Zero; }
                src = Marshal.ReadIntPtr(owned, OffsetInfo2DevMode);
                if (src == IntPtr.Zero) { Say("[警告] 這台印表機沒有回傳 DEVMODE"); return IntPtr.Zero; }
            }

            int total = Marshal.ReadInt16(src, OffsetDevModeSize) + Marshal.ReadInt16(src, OffsetDevModeDriverExtra);
            Say($"DEVMODE 大小：{total} bytes");

            var hMem = GlobalAlloc(GMEM_MOVEABLE, (UIntPtr)(uint)total);
            if (hMem == IntPtr.Zero) { Say("[警告] GlobalAlloc 失敗"); return IntPtr.Zero; }

            var dst = GlobalLock(hMem);
            try
            {
                var tmp = new byte[total];
                Marshal.Copy(src, tmp, 0, total);
                Marshal.Copy(tmp, 0, dst, total);
            }
            finally { GlobalUnlock(hMem); }

            return hMem;
        }
        finally
        {
            if (owned != IntPtr.Zero) Marshal.FreeHGlobal(owned);
            ClosePrinter(hPrinter);
        }
    }

    // ───────────────────────── 測試頁 ─────────────────────────

    private static bool PrintTestPage(IntPtr hdc, string label)
    {
        if (hdc == IntPtr.Zero) { Say("[警告] 沒有拿到印表機 DC（PD_RETURNDC 沒生效）"); return false; }

        int tech = GetDeviceCaps(hdc, TECHNOLOGY);
        int dpiX = GetDeviceCaps(hdc, LOGPIXELSX);
        int dpiY = GetDeviceCaps(hdc, LOGPIXELSY);
        int horz = GetDeviceCaps(hdc, HORZRES);
        int vert = GetDeviceCaps(hdc, VERTRES);
        int physW = GetDeviceCaps(hdc, PHYSICALWIDTH);
        int physH = GetDeviceCaps(hdc, PHYSICALHEIGHT);
        int offX = GetDeviceCaps(hdc, PHYSICALOFFSETX);
        int offY = GetDeviceCaps(hdc, PHYSICALOFFSETY);

        // 這幾個數字是整份計畫裡「縮放語意」的輸入值。今天「印歪」只能靠現場土法對照，
        // 有了它們才第一次能在辦公室算出偏移量。
        Say($"裝置類型　：{tech}{(tech == DT_RASPRINTER ? "（DT_RASPRINTER ✓）" : "（不是點陣印表機）")}");
        Say($"DPI　　　 ：{dpiX} × {dpiY}");
        Say($"可列印區　：{horz} × {vert} px　＝ {horz / (double)dpiX * 2.54:F2} × {vert / (double)dpiY * 2.54:F2} cm");
        Say($"實體紙張　：{physW} × {physH} px　＝ {physW / (double)dpiX * 2.54:F2} × {physH / (double)dpiY * 2.54:F2} cm");
        Say($"不可列印邊：{offX} × {offY} px　＝ {offX / (double)dpiX * 2.54:F2} × {offY / (double)dpiY * 2.54:F2} cm");

        var di = new DOCINFO { cbSize = Marshal.SizeOf<DOCINFO>(), lpszDocName = "列印診斷測試頁" };
        if (StartDoc(hdc, ref di) <= 0) { Say($"[失敗] StartDoc，win32={Marshal.GetLastWin32Error()}"); return false; }
        if (StartPage(hdc) <= 0) { Say($"[失敗] StartPage，win32={Marshal.GetLastWin32Error()}"); EndDoc(hdc); return false; }

        var font = CreateFont(-(12 * dpiY / 72), 0, 0, 0, 400, 0, 0, 0, 1, 0, 0, 0, 0, "新細明體");
        var old = font != IntPtr.Zero ? SelectObject(hdc, font) : IntPtr.Zero;
        try
        {
            // 沿著可列印區畫一圈框：實印之後量這個框，就知道驅動宣稱的可列印區準不準。
            Rectangle(hdc, 0, 0, horz - 1, vert - 1);

            int line = dpiY / 4;
            int y = line;
            foreach (var text in new[]
            {
                "寶覺寺法會報名系統 — 列印診斷測試頁",
                $"時間：{DateTime.Now:yyyy-MM-dd HH:mm:ss}",
                label,
                $"DPI {dpiX}×{dpiY}　可列印區 {horz}×{vert}px　實體紙 {physW}×{physH}px",
                $"不可列印邊界 {offX}×{offY}px",
                "",
                "這張紙印出來，代表舊版列印對話框這條路是通的。",
                "外框就是驅動宣稱的「可列印範圍」。",
            })
            {
                if (text.Length > 0) TextOut(hdc, line, y, text, text.Length);
                y += line;
            }
        }
        finally
        {
            if (old != IntPtr.Zero) SelectObject(hdc, old);
            if (font != IntPtr.Zero) DeleteObject(font);
        }

        if (EndPage(hdc) <= 0) { Say($"[失敗] EndPage，win32={Marshal.GetLastWin32Error()}"); EndDoc(hdc); return false; }
        if (EndDoc(hdc) <= 0) { Say($"[失敗] EndDoc，win32={Marshal.GetLastWin32Error()}"); return false; }

        Say("已送出測試頁（工作已進入列印佇列）。");
        return true;
    }

    // ───────────────────────── 小工具 ─────────────────────────

    private static string ReadStringField<T>(IntPtr buf, string fieldName) where T : struct
    {
        var p = Marshal.ReadIntPtr(buf, (int)Marshal.OffsetOf<T>(fieldName));
        return p == IntPtr.Zero ? "(無)" : Marshal.PtrToStringUni(p) ?? "(無)";
    }

    /// <summary>從對話框回傳的 DEVNAMES 讀出使用者實際選了哪一台。</summary>
    private static string ReadChosenDevice(IntPtr hDevNames)
    {
        if (hDevNames == IntPtr.Zero) return "(未回傳)";
        var p = GlobalLock(hDevNames);
        try
        {
            if (p == IntPtr.Zero) return "(鎖不住)";
            int deviceOffsetChars = Marshal.ReadInt16(p, 2);   // DEVNAMES.wDeviceOffset（單位是字元）
            return Marshal.PtrToStringUni(p + deviceOffsetChars * 2) ?? "(空)";
        }
        finally { GlobalUnlock(hDevNames); }
    }

    private static void FreeIf(IntPtr hMem)
    {
        if (hMem != IntPtr.Zero) GlobalFree(hMem);
    }

    private static void AskAndRecord(string question, string key)
    {
        Console.Write(question);
        var answer = (Console.ReadLine() ?? "").Trim().ToLowerInvariant();
        bool yes = answer is "y" or "yes" or "是" or "有" or "1";
        Transcript.AppendLine($"[回答] {key}：{(yes ? "是" : "否")}（原文「{answer}」）");
        Console.WriteLine();
    }

    private static void Say(string line)
    {
        Console.WriteLine(line);
        Transcript.AppendLine(line);
    }

    private static string WriteTranscript()
    {
        var path = Path.Combine(AppContext.BaseDirectory, $"列印診斷-{DateTime.Now:yyyyMMdd-HHmmss}.txt");
        try
        {
            File.WriteAllText(path, Transcript.ToString(), new UTF8Encoding(true));
            return path;
        }
        catch (Exception ex)
        {
            return $"(寫入失敗：{ex.Message})";
        }
    }
}
