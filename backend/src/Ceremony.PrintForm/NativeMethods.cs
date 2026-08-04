using System.Runtime.InteropServices;

namespace Ceremony.PrintForm;

/// <summary>
/// winspool.drv 的最小綁定：把一份 DEVMODE 存成「每使用者預設」。
/// </summary>
/// <remarks>
/// 為什麼是 Level 9 而不是 Level 8：
/// Level 8 是 printer 的**全域**預設，需要 PRINTER_ACCESS_ADMINISTER（＝系統管理員／印表機管理權）；
/// Level 9 是**每使用者**預設，只需要 PRINTER_ACCESS_USE。寺方使用者多半不是本機管理員，
/// 而 Level 9 正好就是「印表機 → 列印喜好設定」那個 UI 寫入的位置，也就是 PrintDlgEx 開啟時的初值來源。
/// </remarks>
internal static class NativeMethods
{
    internal const int PRINTER_ACCESS_USE = 0x00000008;

    internal const int DM_OUT_BUFFER = 2;
    internal const int DM_IN_BUFFER = 8;
    internal const int IDOK = 1;

    internal const uint DM_ORIENTATION = 0x00000001;
    internal const uint DM_PAPERSIZE = 0x00000002;
    internal const uint DM_PAPERLENGTH = 0x00000004;
    internal const uint DM_PAPERWIDTH = 0x00000008;

    [StructLayout(LayoutKind.Sequential)]
    internal struct PRINTER_DEFAULTS
    {
        public IntPtr pDatatype;
        public IntPtr pDevMode;
        public int DesiredAccess;
    }

    /// <summary>
    /// DEVMODEW 的**前綴**欄位，只為了用 <see cref="Marshal.OffsetOf"/> 算位移。
    /// </summary>
    /// <remarks>
    /// 刻意不定義完整結構、也刻意不用 PtrToStructure→StructureToPtr 一來一回搬整包：
    /// DEVMODE 後面緊接著 dmDriverExtra 的驅動私有資料，只要 struct 定義與 OS 版本差一個欄位就會把
    /// 私有區的位移弄壞（症狀是驅動靜默忽略、或跳出莫名其妙的設定）。這裡一律用
    /// Marshal.ReadInt16/WriteInt16 就地改那兩、三個欄位。
    /// LayoutKind.Sequential 保證前綴欄位的位移與完整結構相同。
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
        public uint dmFields;
        public short dmOrientation;
        public short dmPaperSize;
        public short dmPaperLength;
        public short dmPaperWidth;
    }

    internal static readonly int OffsetFields = (int)Marshal.OffsetOf<DevModePrefix>(nameof(DevModePrefix.dmFields));
    internal static readonly int OffsetPaperSize = (int)Marshal.OffsetOf<DevModePrefix>(nameof(DevModePrefix.dmPaperSize));
    internal static readonly int OffsetPaperLength = (int)Marshal.OffsetOf<DevModePrefix>(nameof(DevModePrefix.dmPaperLength));
    internal static readonly int OffsetPaperWidth = (int)Marshal.OffsetOf<DevModePrefix>(nameof(DevModePrefix.dmPaperWidth));

    [DllImport("winspool.drv", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "OpenPrinterW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool OpenPrinter(string pPrinterName, out IntPtr phPrinter, ref PRINTER_DEFAULTS pDefault);

    [DllImport("winspool.drv", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool ClosePrinter(IntPtr hPrinter);

    [DllImport("winspool.drv", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "DocumentPropertiesW")]
    internal static extern int DocumentProperties(
        IntPtr hWnd, IntPtr hPrinter, string pDeviceName, IntPtr pDevModeOutput, IntPtr pDevModeInput, int fMode);

    /// <summary>Level 9 時 pPrinter 指向 PRINTER_INFO_9（單一欄位：pDevMode）。</summary>
    [DllImport("winspool.drv", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "SetPrinterW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetPrinter(IntPtr hPrinter, int Level, IntPtr pPrinter, int Command);
}
