namespace Ceremony.Application.Reports;

/// <summary>
/// 把多個 PDF 檔合併成單一 PDF 檔（來源與目的都是檔案路徑，不經 byte[]）。
/// </summary>
/// <remarks>
/// Legacy: SignupForm.cs:1698-1722 (CombinePDFs) — 舊系統用 PdfSharp.PdfDocument + PdfReader.Open(Import) 逐頁 AddPage
///
/// 2026-08-02 從 <c>byte[] Merge(IReadOnlyList&lt;byte[]&gt;)</c> 改成路徑版：
/// 大量列印取消分段後又變回「一次合併全部」，而 MemoryStream 有 2 GB 硬上限
/// （19018 筆實測丟 <c>Stream was too long</c>），再加上 <c>.ToArray()</c> 會把成品整份再複製一次。
/// 見 docs/blueprints/print-channel-electron.md。
/// </remarks>
public interface IPdfMerger
{
    /// <param name="srcPaths">要合併的單筆 PDF 檔路徑，順序即成品頁序。</param>
    /// <param name="destPath">成品輸出路徑；已存在會被覆寫。父目錄需先存在。</param>
    void Merge(IReadOnlyList<string> srcPaths, string destPath);
}
