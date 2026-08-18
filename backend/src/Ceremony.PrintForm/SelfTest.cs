using System.Drawing.Printing;
using System.Runtime.InteropServices;
using System.Text;
using Ceremony.Domain.Reports;
using static Ceremony.PrintForm.PrintDialogNative;

namespace Ceremony.PrintForm;

/// <summary>
/// 決策 11 送印路徑的非互動自我檢查——**不開任何視窗、不等任何輸入**。
/// </summary>
/// <remarks>
/// <para>
/// <b>存在的理由</b>：本專案的開發機是 macOS，而這條路徑整條都是 Win32 P/Invoke
/// （<c>PrintDlgW</c> / GDI / PDFium）。這類程式最典型的失敗是**編譯完全沒問題、一執行就崩**，
/// 在 macOS 上一行都驗不到。CI 的 windows-latest 是我們唯一的 Windows 環境。
/// </para>
/// <para>
/// <b>涵蓋得到</b>：exe 啟動、<c>PRINTDLG</c> 的 x64 struct 版面、<c>PrintDlgW</c> 進入點
/// （<c>PD_RETURNDEFAULT</c>，走同一個結構但不畫 UI）、pdfium.dll 載入與版面、
/// <c>PrintScalePolicy</c> 對真實 <c>GetDeviceCaps</c> 的輸出、以及
/// <c>StartDoc→FPDF_RenderPage→EndDoc</c> 全鏈路（輸出導向檔案，不需要實體印表機）。
/// </para>
/// <para>
/// <b>涵蓋不到</b>：對話框長什麼樣、列印鈕是不是灰的、實體套印位置對不對——
/// 前兩者只有現場那台 PA2000 答得出來，後者只有六報表對照組。
/// </para>
/// <para>
/// ⚠️ <b>exit code 仍然永遠 0</b>（見 Program.cs 檔頭）。這條規則不分子命令——
/// per-subcommand 的例外是未來一定會咬人的東西。CI 改看最後那行 JSON 的 <c>result</c>。
/// </para>
/// </remarks>
internal static class SelfTest
{
    internal static (string Result, List<string> Lines) Run()
    {
        var lines = new List<string>();
        bool failed = false;

        void Check(string name, bool ok, string? detail = null)
        {
            lines.Add($"{(ok ? "PASS" : "FAIL")} {name}{(detail is null ? "" : $" — {detail}")}");
            if (!ok) failed = true;
        }

        lines.Add($"OS       : {Environment.OSVersion.VersionString}");
        lines.Add($"64-bit   : process={Environment.Is64BitProcess} os={Environment.Is64BitOperatingSystem}");

        // ⓪ apartment。2026-08-18 客訴的根因：console app 主執行緒預設 MTA，而 comdlg32 的
        //    對話框與驅動的設定元件是 COM／要求 STA ⇒ 對話框開得出來、按下確定就再也不回來
        //    （對話框關不掉、owner 一直被 disable、換印表機一樣）。見 StaRunner。
        //    ⚠️ 這一格鎖的是「print 子命令真的跑在 STA 上」，不是主執行緒——主執行緒是 MTA 沒關係。
        lines.Add($"apartment: main={Thread.CurrentThread.GetApartmentState()}");
        Check("print 走 STA 執行緒",
            StaRunner.Run(() => Thread.CurrentThread.GetApartmentState()) == ApartmentState.STA);

        // ① struct 版面。算錯會回 CDERR_STRUCTSIZE 或直接踩壞記憶體——macOS 上驗不到的東西。
        int size = Marshal.SizeOf<PRINTDLG>();
        int expected = Environment.Is64BitProcess ? 120 : 66;
        Check("PRINTDLG size", size == expected, $"{size} (expected {expected})");

        // ② pdfium.dll 載得起來、而且是我們自己寫的那組綁定叫得動。
        //    這一格失敗多半是打包問題：PublishSingleFile 只打包 managed 組件，
        //    native 的 pdfium.dll 必須留在 exe 旁邊。
        string? pdfPath = null;
        try
        {
            PdfiumNative.InitLibrary();
            pdfPath = Path.Combine(Path.GetTempPath(), $"printform-selftest-{Guid.NewGuid():N}.pdf");
            File.WriteAllBytes(pdfPath, MinimalPdf.OnePage(595.28, 419.53, "Ceremony print self-test"));
            Check("pdfium load", true);
        }
        catch (Exception e)
        {
            Check("pdfium load", false, $"{e.GetType().Name}: {e.Message}");
            return (failed ? "failed" : "ok", lines);
        }

        try
        {
            // ③ 讀得到那份 PDF（走的是正式路徑用的 FPDF_LoadCustomDocument，不是吃路徑那支）。
            using var access = new SelfTestFileAccess(pdfPath);
            var doc = access.Load();
            Check("FPDF_LoadCustomDocument", doc != IntPtr.Zero, $"pdfium={PdfiumNative.GetLastError()}");
            if (doc == IntPtr.Zero) return ("failed", lines);

            try
            {
                int pageCount = PdfiumNative.GetPageCount(doc);
                Check("page count", pageCount == 1, $"{pageCount}");

                var page = PdfiumNative.LoadPage(doc, 0);
                Check("FPDF_LoadPage", page != IntPtr.Zero);
                if (page == IntPtr.Zero) return ("failed", lines);

                try
                {
                    float w = PdfiumNative.GetPageWidth(page);
                    float h = PdfiumNative.GetPageHeight(page);
                    Check("page size (pt)", w > 500 && h > 400, $"{w:F1}x{h:F1}");

                    // ④ 印表機：沒有的話後面全部略過（CI runner 常態，不算失敗）。
                    var settings = new PrinterSettings();
                    if (!settings.IsValid)
                    {
                        lines.Add("SKIP printer —— 這台機器沒有可用的預設印表機");
                        return (failed ? "failed" : "ok", lines);
                    }
                    lines.Add($"printer  : {settings.PrinterName}");

                    // ⑤ PrintDlgW 本身。PD_RETURNDEFAULT 走同一個進入點與同一個結構，但不畫 UI。
                    //    hDevMode / hDevNames 必須為 NULL。
                    var pd = new PRINTDLG
                    {
                        lStructSize = (uint)size,
                        Flags = PD_RETURNDEFAULT | PD_RETURNDC,
                    };
                    bool dlg = PrintDlg(ref pd);
                    Check("PrintDlgW(PD_RETURNDEFAULT)", dlg, dlg ? null : $"0x{CommDlgExtendedError():X4}");
                    if (!dlg || pd.hDC == IntPtr.Zero) return ("failed", lines);

                    try
                    {
                        // ⑥ GetDeviceCaps → PrintScalePolicy。這是整條路徑唯一的幾何決策點，
                        //    用真實裝置度量跑一次，比任何 fixture 都有說服力。
                        var m = new PrintScalePolicy.DeviceMetrics(
                            GetDeviceCaps(pd.hDC, HORZRES), GetDeviceCaps(pd.hDC, VERTRES),
                            GetDeviceCaps(pd.hDC, PHYSICALWIDTH), GetDeviceCaps(pd.hDC, PHYSICALHEIGHT),
                            GetDeviceCaps(pd.hDC, PHYSICALOFFSETX), GetDeviceCaps(pd.hDC, PHYSICALOFFSETY),
                            GetDeviceCaps(pd.hDC, LOGPIXELSX), GetDeviceCaps(pd.hDC, LOGPIXELSY));

                        lines.Add($"caps     : dpi={m.DpiX}x{m.DpiY} printable={m.PrintableWidthPx}x{m.PrintableHeightPx} " +
                                  $"physical={m.PhysicalWidthPx}x{m.PhysicalHeightPx} offset={m.OffsetXPx}x{m.OffsetYPx} " +
                                  $"technology={GetDeviceCaps(pd.hDC, TECHNOLOGY)}");

                        var dest = PrintScalePolicy.Compute(w, h, m, PrintScalePolicy.ScaleMode.Fit);
                        Check("PrintScalePolicy.Compute", !dest.IsEmpty,
                            $"{dest.X},{dest.Y},{dest.Width}x{dest.Height}");
                        Check("dest 落在可列印區內",
                            dest.X >= 0 && dest.Y >= 0
                            && dest.X + dest.Width <= m.PrintableWidthPx
                            && dest.Y + dest.Height <= m.PrintableHeightPx);

                        // ⑦ 全鏈路。lpszOutput 導向檔案 ⇒ 不需要實體印表機也能驗完 StartDoc→EndDoc。
                        var outFile = Path.Combine(Path.GetTempPath(), $"printform-selftest-{Guid.NewGuid():N}.out");
                        var di = new DOCINFO
                        {
                            cbSize = Marshal.SizeOf<DOCINFO>(),
                            lpszDocName = "Ceremony print self-test",
                            lpszOutput = outFile,
                        };

                        bool chain = StartDoc(pd.hDC, ref di) > 0 && StartPage(pd.hDC) > 0;
                        if (chain)
                        {
                            PdfiumNative.RenderPage(
                                pd.hDC, page, dest.X, dest.Y, dest.Width, dest.Height, 0,
                                PdfiumNative.FPDF_ANNOT | PdfiumNative.FPDF_PRINTING);
                            chain = EndPage(pd.hDC) > 0 && EndDoc(pd.hDC) > 0;
                        }

                        Check("StartDoc→FPDF_RenderPage→EndDoc", chain,
                            chain ? null : $"win32={Marshal.GetLastWin32Error()}");

                        // 有輸出才代表真的畫了東西——這是「PDF 有沒有變成墨」唯一可自動化的證據。
                        var len = File.Exists(outFile) ? new FileInfo(outFile).Length : 0;
                        Check("spool 輸出非空", len > 0, $"{len} bytes");
                        if (File.Exists(outFile)) File.Delete(outFile);
                    }
                    finally
                    {
                        DeleteDC(pd.hDC);
                        if (pd.hDevMode != IntPtr.Zero) GlobalFree(pd.hDevMode);
                        if (pd.hDevNames != IntPtr.Zero) GlobalFree(pd.hDevNames);
                    }
                }
                finally
                {
                    PdfiumNative.ClosePage(page);
                }
            }
            finally
            {
                PdfiumNative.CloseDocument(doc);
            }
        }
        catch (Exception e)
        {
            Check("unexpected", false, $"{e.GetType().Name}: {e.Message}");
        }
        finally
        {
            PdfiumNative.DestroyLibrary();
            if (pdfPath is not null && File.Exists(pdfPath)) File.Delete(pdfPath);
        }

        return (failed ? "failed" : "ok", lines);
    }

    /// <summary>與正式路徑同一套讀檔方式（<c>FPDF_LoadCustomDocument</c>）。</summary>
    private sealed class SelfTestFileAccess : IDisposable
    {
        private readonly FileStream _stream;
        private readonly PdfiumNative.GetBlockCallback _callback;
        private PdfiumNative.FPDF_FILEACCESS _access;

        internal SelfTestFileAccess(string path)
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

/// <summary>
/// 手刻一份最小的合法 PDF，只給自我檢查用。
/// </summary>
/// <remarks>
/// 刻意**不**用 QuestPDF 產：那會讓 <c>Ceremony.PrintForm</c> 相依整個 Infrastructure，
/// 而這支 exe 刻意是葉節點（只依賴 Domain）。自我檢查要的只是「一份 PDFium 讀得懂、
/// 有一頁、有一點墨」的檔案，六百個位元組就夠了。
/// xref 位移逐一算出來——不倚賴 PDFium 的 xref 修復路徑，否則這支檢查會連
/// 「我們產的檔是不是合法」都測不出來。
/// </remarks>
internal static class MinimalPdf
{
    internal static byte[] OnePage(double widthPt, double heightPt, string text)
    {
        var content = $"BT /F1 24 Tf 40 {heightPt - 60:F0} Td ({Escape(text)}) Tj ET\n"
                      + $"2 w 20 20 {widthPt - 40:F0} {heightPt - 40:F0} re S\n";
        var contentBytes = Encoding.ASCII.GetByteCount(content);

        var objects = new[]
        {
            "<</Type/Catalog/Pages 2 0 R>>",
            "<</Type/Pages/Kids[3 0 R]/Count 1>>",
            $"<</Type/Page/Parent 2 0 R/MediaBox[0 0 {widthPt:F2} {heightPt:F2}]"
                + "/Resources<</Font<</F1 4 0 R>>>>/Contents 5 0 R>>",
            "<</Type/Font/Subtype/Type1/BaseFont/Helvetica>>",
            $"<</Length {contentBytes}>>\nstream\n{content}endstream",
        };

        var sb = new StringBuilder("%PDF-1.4\n");
        var offsets = new int[objects.Length];
        for (int i = 0; i < objects.Length; i++)
        {
            offsets[i] = sb.Length;
            sb.Append(i + 1).Append(" 0 obj\n").Append(objects[i]).Append("\nendobj\n");
        }

        int xref = sb.Length;
        sb.Append("xref\n0 ").Append(objects.Length + 1).Append('\n');
        sb.Append("0000000000 65535 f \n");
        foreach (var off in offsets) sb.Append(off.ToString("D10")).Append(" 00000 n \n");
        sb.Append("trailer\n<</Size ").Append(objects.Length + 1).Append("/Root 1 0 R>>\n");
        sb.Append("startxref\n").Append(xref).Append("\n%%EOF\n");

        return Encoding.ASCII.GetBytes(sb.ToString());
    }

    private static string Escape(string s) =>
        s.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");
}
