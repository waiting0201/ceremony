using System.Runtime.InteropServices;

namespace Ceremony.PrintProbe;

/// <summary>
/// comdlg32 / gdi32 / winspool 的最小綁定，只為 Phase 0 探針服務。
/// </summary>
/// <remarks>
/// <para>
/// **為什麼是 <c>PrintDlgW</c> 而不是 WinForms 的 <c>PrintDialog</c>**：
/// WinForms 對話框的選擇是**框架內部的分支**（<c>UseEXDialog</c> × OS 版本 × 位元數），
/// 舊系統是靠「AnyCPU 實際跑 32-bit」這個偶然才走到 legacy 對話框的。
/// 探針要回答的問題是「legacy 對話框會不會噴 0x80010105」，
/// 所以必須確定呼叫到的就是那一個——具名匯出函式沒有分支。
/// </para>
/// <para>
/// ⚠️ 刻意**不**呼叫 <c>PrintDlgExW</c>：那是新版對話框，正是現場出錯的那一條。
/// </para>
/// </remarks>
internal static class NativeMethods
{
    // ───────────────────────── comdlg32：舊版列印對話框 ─────────────────────────

    internal const uint PD_ALLPAGES = 0x00000000;
    internal const uint PD_SELECTION = 0x00000001;
    internal const uint PD_PAGENUMS = 0x00000002;
    internal const uint PD_NOSELECTION = 0x00000004;
    internal const uint PD_RETURNDC = 0x00000100;

    /// <summary>
    /// **不顯示對話框**，直接把系統預設印表機的 DEVMODE／DEVNAMES（配 PD_RETURNDC 則含 hDC）填回來。
    /// </summary>
    /// <remarks>
    /// 這是 <c>--selftest</c> 能在 CI（windows-latest，無人看著螢幕）上驗證整條 P/Invoke 的關鍵：
    /// 它走的是與互動模式**完全相同**的 <c>PrintDlgW</c> 進入點與 <c>PRINTDLG</c> 結構，
    /// 只是不畫 UI ⇒ struct 版面錯了一樣會現形。
    /// ⚠️ 呼叫時 hDevMode 與 hDevNames **必須都是 NULL**，否則 PrintDlg 直接回錯。
    /// </remarks>
    internal const uint PD_RETURNDEFAULT = 0x00000400;
    internal const uint PD_USEDEVMODECOPIESANDCOLLATE = 0x00040000;
    internal const uint PD_HIDEPRINTTOFILE = 0x00100000;

    /// <remarks>
    /// x64 版面：<c>lStructSize</c> 之後編譯器會補 4 bytes 讓 <c>hwndOwner</c> 對齊到 8。
    /// <see cref="LayoutKind.Sequential"/> 的 .NET marshaller 套用同一套對齊規則，所以兩邊一致；
    /// 中間那五個 <c>WORD</c> 之後同理會補 2 bytes 讓 <c>hInstance</c> 對齊。
    /// **不要**加 <c>Pack</c>，那會破壞這個一致性。
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

    /// <summary>對話框回 FALSE 時：0 ＝ 使用者自己取消，非 0 ＝ 真的出錯。</summary>
    [DllImport("comdlg32.dll", ExactSpelling = true)]
    internal static extern uint CommDlgExtendedError();

    // ───────────────────────── kernel32：DEVMODE 要放在 HGLOBAL 裡 ─────────────────────────

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

    // ───────────────────────── gdi32：把測試頁畫出來 ─────────────────────────

    internal const int TECHNOLOGY = 2;
    internal const int HORZRES = 8;
    internal const int VERTRES = 10;
    internal const int LOGPIXELSX = 88;
    internal const int LOGPIXELSY = 90;
    internal const int PHYSICALWIDTH = 110;
    internal const int PHYSICALHEIGHT = 111;
    internal const int PHYSICALOFFSETX = 112;
    internal const int PHYSICALOFFSETY = 113;

    /// <summary>DT_RASPRINTER —— PDFium 的 GDI 印表機裝置驅動只在這個值下生效。</summary>
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

    [DllImport("gdi32.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "TextOutW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool TextOut(IntPtr hdc, int x, int y, string lpString, int c);

    [DllImport("gdi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool Rectangle(IntPtr hdc, int left, int top, int right, int bottom);

    [DllImport("gdi32.dll", SetLastError = true)]
    internal static extern int EndPage(IntPtr hdc);

    [DllImport("gdi32.dll", SetLastError = true)]
    internal static extern int EndDoc(IntPtr hdc);

    [DllImport("gdi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool DeleteDC(IntPtr hdc);

    [DllImport("gdi32.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "CreateFontW")]
    internal static extern IntPtr CreateFont(
        int cHeight, int cWidth, int cEscapement, int cOrientation, int cWeight,
        uint bItalic, uint bUnderline, uint bStrikeOut, uint iCharSet,
        uint iOutPrecision, uint iClipPrecision, uint iQuality, uint iPitchAndFamily, string pszFaceName);

    [DllImport("gdi32.dll", SetLastError = true)]
    internal static extern IntPtr SelectObject(IntPtr hdc, IntPtr h);

    [DllImport("gdi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool DeleteObject(IntPtr ho);

    // ───────────────────────── winspool：DEVMODE 來源與驅動版本 ─────────────────────────

    internal const int PRINTER_ACCESS_USE = 0x00000008;
    internal const int DM_OUT_BUFFER = 2;

    [StructLayout(LayoutKind.Sequential)]
    internal struct PRINTER_DEFAULTS
    {
        public IntPtr pDatatype;
        public IntPtr pDevMode;
        public int DesiredAccess;
    }

    /// <summary>
    /// PRINTER_INFO_2 的**前綴**，只為了用 <see cref="Marshal.OffsetOf"/> 算出 pDevMode 的位移。
    /// </summary>
    /// <remarks>與 PrintForm 的 <c>DevModePrefix</c> 同一套作法：不定義完整結構，避免與 OS 版本差一個欄位。</remarks>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
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

    /// <summary>
    /// DRIVER_INFO_2 的前綴。<c>cVersion</c> 就是**驅動類型**：3 ＝ v3、4 ＝ v4。
    /// </summary>
    /// <remarks>
    /// 這一欄直接回答本次診斷最大的推論缺口——「PA2000 GX 是不是 v4 驅動」
    /// 目前只是從名稱推的，沒有直接證據。
    /// </remarks>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct DriverInfo2Prefix
    {
        public uint cVersion;
        public IntPtr pName;
        public IntPtr pEnvironment;
        public IntPtr pDriverPath;
    }

    [DllImport("winspool.drv", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "OpenPrinterW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool OpenPrinter(string pPrinterName, out IntPtr phPrinter, ref PRINTER_DEFAULTS pDefault);

    [DllImport("winspool.drv", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool ClosePrinter(IntPtr hPrinter);

    [DllImport("winspool.drv", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "GetPrinterW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetPrinter(IntPtr hPrinter, int Level, IntPtr pPrinter, int cbBuf, out int pcbNeeded);

    [DllImport("winspool.drv", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "GetPrinterDriverW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetPrinterDriver(
        IntPtr hPrinter, string? pEnvironment, int Level, IntPtr pDriverInfo, int cbBuf, out int pcbNeeded);

    [DllImport("winspool.drv", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "DocumentPropertiesW")]
    internal static extern int DocumentProperties(
        IntPtr hWnd, IntPtr hPrinter, string pDeviceName, IntPtr pDevModeOutput, IntPtr pDevModeInput, int fMode);

    /// <summary>
    /// DEVMODEW 的前綴，只為了算 <c>dmSize</c> / <c>dmDriverExtra</c> 的位移
    /// （一份 DEVMODE 的實際大小 ＝ 兩者相加）。
    /// </summary>
    /// <remarks>
    /// 與 <c>Ceremony.PrintForm.NativeMethods.DevModePrefix</c> 同一套作法與同一個理由：
    /// 不定義完整結構、不用 PtrToStructure 一來一回搬整包，因為 DEVMODE 後面緊接著
    /// dmDriverExtra 的驅動私有資料，struct 定義與 OS 版本差一個欄位就會把私有區的位移弄壞。
    /// **刻意不寫死 68 / 70**：這個 repo 的慣例是位移一律由 <see cref="Marshal.OffsetOf"/> 推導。
    /// </remarks>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct DevModePrefix
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string dmDeviceName;
        public ushort dmSpecVersion;
        public ushort dmDriverVersion;
        public ushort dmSize;
        public ushort dmDriverExtra;
    }

    internal static readonly int OffsetDevModeSize =
        (int)Marshal.OffsetOf<DevModePrefix>(nameof(DevModePrefix.dmSize));

    internal static readonly int OffsetDevModeDriverExtra =
        (int)Marshal.OffsetOf<DevModePrefix>(nameof(DevModePrefix.dmDriverExtra));
}
