using System.Runtime.InteropServices;

namespace Ceremony.PrintForm;

/// <summary>
/// comdlg32 的**舊版**列印對話框與 GDI 送印的最小綁定（決策 11）。
/// </summary>
/// <remarks>
/// <para>
/// <b>為什麼是具名匯出的 <c>PrintDlgW</c>，而不是 WinForms 的 <c>PrintDialog</c></b>：
/// WinForms 那顆對話框走哪一條是**框架內部的分支**（<c>UseEXDialog</c> × OS 版本 × 位元數），
/// 舊系統是靠「AnyCPU 實際跑 32-bit」這個**偶然**才走到 legacy 的。把偶然搬進新程式，
/// 等於換個地方踩同一顆雷。<c>PrintDlgW</c> 沒有分支：呼叫它拿到的就是那個對話框。
/// 而且開 <c>UseWindowsForms</c> 會把現場的前置需求從 ASP.NET Core Runtime
/// 變成還要 Windows Desktop Runtime（不同的安裝包）。
/// </para>
/// <para>
/// ⚠️ <b>絕對不要改成 <c>PrintDlgExW</c></b>：那是新版對話框，正是 2026-08-15 現場
/// 噴 <c>0x80010105</c>、列印鈕變灰的那一條。整個決策 11 的意義就是繞開它。
/// </para>
/// <para>
/// 現場實證（2026-08-15）：同一台 PA2000、同一份 PDF，Adobe Reader 印得出來而 Chrome 不行
/// ⇒ 印表機與驅動本身沒壞，壞的是「讀每使用者預設 DEVMODE → 轉 PrintTicket」那條路。
/// Adobe 走的正是「自帶一份設定 ＋ GDI 送印」的老路，與本檔要做的事同機制。
/// </para>
/// <para>
/// ⚠️ <c>Ceremony.PrintProbe</c>（Phase 0 一次性探針）有一份幾乎相同的複本，
/// 那是刻意的——探針要能獨立寄給客戶。Phase 3 刪除探針時一併消失。
/// </para>
/// </remarks>
internal static class PrintDialogNative
{
    // ───────────────────────── comdlg32 ─────────────────────────

    internal const uint PD_ALLPAGES = 0x00000000;
    internal const uint PD_PAGENUMS = 0x00000002;
    internal const uint PD_NOSELECTION = 0x00000004;
    internal const uint PD_RETURNDC = 0x00000100;
    internal const uint PD_USEDEVMODECOPIESANDCOLLATE = 0x00040000;
    internal const uint PD_HIDEPRINTTOFILE = 0x00100000;

    /// <remarks>
    /// x64 版面：<c>lStructSize</c> 之後補 4 bytes 讓 <c>hwndOwner</c> 對齊到 8；
    /// 五個 <c>WORD</c> 之後補 2 bytes 讓 <c>hInstance</c> 對齊。
    /// <see cref="LayoutKind.Sequential"/> 的 marshaller 套用同一套規則 ⇒ 兩邊一致（x64 共 120 bytes）。
    /// <b>不要加 <c>Pack</c></b>，那會破壞這個一致性。
    /// </remarks>
    [StructLayout(LayoutKind.Sequential)]
    internal struct PRINTDLG
    {
        public uint lStructSize;
        public IntPtr hwndOwner;
        public IntPtr hDevMode;
        public IntPtr hDevNames;
        public IntPtr hDC;
        public uint Flags;
        public ushort nFromPage;
        public ushort nToPage;
        public ushort nMinPage;
        public ushort nMaxPage;
        public ushort nCopies;
        public IntPtr hInstance;
        public IntPtr lCustData;
        public IntPtr lpfnPrintHook;
        public IntPtr lpfnSetupHook;
        public IntPtr lpPrintTemplateName;
        public IntPtr lpSetupTemplateName;
        public IntPtr hPrintTemplate;
        public IntPtr hSetupTemplate;
    }

    [DllImport("comdlg32.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "PrintDlgW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool PrintDlg(ref PRINTDLG lppd);

    /// <summary>回 FALSE 時：0 ＝ 使用者自己取消，非 0 ＝ 對話框真的失敗。</summary>
    [DllImport("comdlg32.dll", ExactSpelling = true)]
    internal static extern uint CommDlgExtendedError();

    // ───────────────────────── kernel32：DEVMODE 要住在 HGLOBAL ─────────────────────────

    internal const uint GMEM_MOVEABLE = 0x0002;

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern IntPtr GlobalAlloc(uint uFlags, UIntPtr dwBytes);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern IntPtr GlobalLock(IntPtr hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GlobalUnlock(IntPtr hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern IntPtr GlobalFree(IntPtr hMem);

    // ───────────────────────── gdi32 ─────────────────────────

    internal const int TECHNOLOGY = 2;
    internal const int HORZRES = 8;
    internal const int VERTRES = 10;
    internal const int LOGPIXELSX = 88;
    internal const int LOGPIXELSY = 90;
    internal const int PHYSICALWIDTH = 110;
    internal const int PHYSICALHEIGHT = 111;
    internal const int PHYSICALOFFSETX = 112;
    internal const int PHYSICALOFFSETY = 113;

    /// <summary>
    /// DT_RASPRINTER —— PDFium 的 GDI 印表機裝置驅動（<c>CGdiPrinterDriver</c>）只在這個值下生效。
    /// </summary>
    /// <remarks>
    /// 這一格決定了「向量變點陣」這項入場費要付多少：是 <c>DT_RASPRINTER</c> 時，
    /// PDFium 會把路徑／文字／影像以 GDI 圖元送進 DC，而不是先攤成一張整頁的大點陣圖。
    /// 不是的話就會退化成點陣，屆時要在診斷紀錄看得到。
    /// </remarks>
    internal const int DT_RASPRINTER = 2;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct DOCINFO
    {
        public int cbSize;
        [MarshalAs(UnmanagedType.LPWStr)] public string? lpszDocName;
        [MarshalAs(UnmanagedType.LPWStr)] public string? lpszOutput;
        [MarshalAs(UnmanagedType.LPWStr)] public string? lpszDatatype;
        public int fwType;
    }

    [DllImport("gdi32.dll", SetLastError = true)]
    internal static extern int GetDeviceCaps(IntPtr hdc, int nIndex);

    [DllImport("gdi32.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "StartDocW")]
    internal static extern int StartDoc(IntPtr hdc, ref DOCINFO lpdi);

    [DllImport("gdi32.dll", SetLastError = true)]
    internal static extern int StartPage(IntPtr hdc);

    [DllImport("gdi32.dll", SetLastError = true)]
    internal static extern int EndPage(IntPtr hdc);

    [DllImport("gdi32.dll", SetLastError = true)]
    internal static extern int EndDoc(IntPtr hdc);

    [DllImport("gdi32.dll", SetLastError = true)]
    internal static extern int AbortDoc(IntPtr hdc);

    [DllImport("gdi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool DeleteDC(IntPtr hdc);

    // ───────────────────────── winspool：DEVNAMES 讀回使用者選了哪台 ─────────────────────────

    /// <summary>
    /// DEVNAMES 的前綴，只為了用 <see cref="Marshal.OffsetOf"/> 算 <c>wDeviceOffset</c> 的位移。
    /// </summary>
    /// <remarks>四個欄位之後接的是字串區；offset 的單位是**字元**不是 byte。</remarks>
    [StructLayout(LayoutKind.Sequential)]
    internal struct DevNamesPrefix
    {
        public ushort wDriverOffset;
        public ushort wDeviceOffset;
        public ushort wOutputOffset;
        public ushort wDefault;
    }

    internal static readonly int OffsetDeviceOffset =
        (int)Marshal.OffsetOf<DevNamesPrefix>(nameof(DevNamesPrefix.wDeviceOffset));

    /// <summary>
    /// PRINTER_INFO_2 的前綴，只為了算 <c>pDevMode</c> 的位移。
    /// </summary>
    /// <remarks>
    /// 這是 <c>--devmode-source printer</c> 的來源：<c>GetPrinter</c> Level 2 給的 DEVMODE
    /// 繞開了「每使用者預設」那一份（＝現場出問題的那一份）。
    /// ⚠️ 它在使用者設過列印喜好設定之後是否仍回工廠值，文件沒有明說——**只能實測**，
    /// 所以做成三選一的旗標而不是猜一個寫死，並把實際用了哪一個寫進診斷紀錄。
    /// </remarks>
    [StructLayout(LayoutKind.Sequential)]
    internal struct PrinterInfo2Prefix
    {
        public IntPtr pServerName;
        public IntPtr pPrinterName;
        public IntPtr pShareName;
        public IntPtr pPortName;
        public IntPtr pDriverName;
        public IntPtr pComment;
        public IntPtr pLocation;
        public IntPtr pDevMode;
    }

    internal static readonly int OffsetInfo2DevMode =
        (int)Marshal.OffsetOf<PrinterInfo2Prefix>(nameof(PrinterInfo2Prefix.pDevMode));

    [DllImport("winspool.drv", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "GetPrinterW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetPrinter(IntPtr hPrinter, int Level, IntPtr pPrinter, int cbBuf, out int pcbNeeded);

    /// <summary>從對話框回傳的 DEVNAMES 讀出使用者實際選定的印表機名稱。</summary>
    internal static string? ReadChosenDevice(IntPtr hDevNames)
    {
        if (hDevNames == IntPtr.Zero) return null;

        var p = GlobalLock(hDevNames);
        try
        {
            if (p == IntPtr.Zero) return null;
            int deviceOffsetChars = Marshal.ReadInt16(p, OffsetDeviceOffset);
            return Marshal.PtrToStringUni(p + deviceOffsetChars * sizeof(char));
        }
        finally
        {
            GlobalUnlock(hDevNames);
        }
    }
}
