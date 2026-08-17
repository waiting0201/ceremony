using System.Runtime.InteropServices;

namespace Ceremony.PrintForm;

/// <summary>
/// PDFium 的最小綁定——**只走 <c>FPDF_RenderPage(HDC…)</c> 這一支**。
/// </summary>
/// <remarks>
/// <para>
/// <b>為什麼不用 managed wrapper</b>（<c>Docnet.Core</c> / <c>PDFtoImage</c> 之類）：
/// 它們一律只暴露 <c>FPDF_RenderPageBitmap</c>（算成點陣圖）。而 PDFium 在 Windows 上另有一條
/// **印表機 DC 專用**的裝置驅動：HDC 的 <c>TECHNOLOGY == DT_RASPRINTER</c> 時，它會把路徑、
/// 文字、影像**以 GDI 圖元送進 DC**，而不是先攤成一張整頁的大點陣圖。
/// </para>
/// <para>
/// 這是「向量變點陣」這項入場費**唯一能砍價的地方**——而且那項入場費比 blueprint 原本標的貴：
/// 原文寫「舊系統就是這樣」是錯的，舊系統 render 出來的是 EMF **向量**中繼檔
/// （見 docs/gotchas.md 該條 2026-08-15 更正）。光柵化是新方案獨有的新風險，
/// 值得為它多寫十支 DllImport。
/// </para>
/// <para>
/// 授權：PDFium 本體 BSD-3-Clause，<c>bblanchon.PDFium.Win32</c> 打包 Apache-2.0，
/// 皆允許商業再散布。聲明見 docs/design/infrastructure.md。
/// </para>
/// </remarks>
internal static class PdfiumNative
{
    private const string Dll = "pdfium";

    /// <summary>算圖旗標：畫註解 ＋ 以「列印」意圖算（而非螢幕）。</summary>
    internal const int FPDF_ANNOT = 0x01;

    internal const int FPDF_PRINTING = 0x800;

    [DllImport(Dll, EntryPoint = "FPDF_InitLibrary", ExactSpelling = true)]
    internal static extern void InitLibrary();

    [DllImport(Dll, EntryPoint = "FPDF_DestroyLibrary", ExactSpelling = true)]
    internal static extern void DestroyLibrary();

    /// <summary>
    /// PDFium 的檔案讀取回呼。回 1 成功、0 失敗。
    /// </summary>
    /// <remarks>⚠️ 委派實例必須由呼叫端保持存活（<c>GC.KeepAlive</c>），否則會在算圖中途被回收。</remarks>
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate int GetBlockCallback(IntPtr param, uint position, IntPtr buffer, uint size);

    [StructLayout(LayoutKind.Sequential)]
    internal struct FPDF_FILEACCESS
    {
        public uint m_FileLen;
        public IntPtr m_GetBlock;
        public IntPtr m_Param;
    }

    /// <summary>
    /// 用回呼讀檔，**刻意不用 <c>FPDF_LoadDocument</c>（吃路徑）或 <c>FPDF_LoadMemDocument</c>（吃整包）**。
    /// </summary>
    /// <remarks>
    /// 兩個各自獨立、而且都只在現場才會炸的理由：
    /// <list type="number">
    /// <item>
    /// <b>中文使用者名稱</b>。temp 路徑是 <c>%TEMP%\ceremony-print\…</c>，實際會落在
    /// <c>C:\Users\王小明\AppData\Local\Temp\…</c>。<c>FPDF_LoadDocument</c> 對非 ASCII 路徑的
    /// 處理是 codepage 相依的坑——開發機永遠看不到，與 docs/gotchas.md
    /// 〈<c>createWriteStream</c> 不會補父目錄〉同型。
    /// </item>
    /// <item>
    /// <b>批次 PDF 可達數百 MB</b>（blueprint 決策 5 記錄 5000 筆 ≈670MB）。
    /// <c>FPDF_LoadMemDocument</c> 要整份進記憶體。
    /// </item>
    /// </list>
    /// </remarks>
    [DllImport(Dll, EntryPoint = "FPDF_LoadCustomDocument", ExactSpelling = true)]
    internal static extern IntPtr LoadCustomDocument(ref FPDF_FILEACCESS access, string? password);

    [DllImport(Dll, EntryPoint = "FPDF_CloseDocument", ExactSpelling = true)]
    internal static extern void CloseDocument(IntPtr document);

    [DllImport(Dll, EntryPoint = "FPDF_GetPageCount", ExactSpelling = true)]
    internal static extern int GetPageCount(IntPtr document);

    [DllImport(Dll, EntryPoint = "FPDF_LoadPage", ExactSpelling = true)]
    internal static extern IntPtr LoadPage(IntPtr document, int pageIndex);

    [DllImport(Dll, EntryPoint = "FPDF_ClosePage", ExactSpelling = true)]
    internal static extern void ClosePage(IntPtr page);

    /// <summary>頁寬，單位 point（1/72 吋）。</summary>
    [DllImport(Dll, EntryPoint = "FPDF_GetPageWidthF", ExactSpelling = true)]
    internal static extern float GetPageWidth(IntPtr page);

    /// <summary>頁高，單位 point。</summary>
    [DllImport(Dll, EntryPoint = "FPDF_GetPageHeightF", ExactSpelling = true)]
    internal static extern float GetPageHeight(IntPtr page);

    /// <summary>
    /// **Windows 專屬**：直接把該頁畫進一個 HDC。這一支就是選 PDFium 而非 managed wrapper 的全部理由。
    /// </summary>
    [DllImport(Dll, EntryPoint = "FPDF_RenderPage", ExactSpelling = true)]
    internal static extern void RenderPage(
        IntPtr dc, IntPtr page, int startX, int startY, int sizeX, int sizeY, int rotate, int flags);

    [DllImport(Dll, EntryPoint = "FPDF_GetLastError", ExactSpelling = true)]
    internal static extern uint GetLastError();
}
