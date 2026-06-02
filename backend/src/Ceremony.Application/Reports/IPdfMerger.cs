namespace Ceremony.Application.Reports;

/// <summary>
/// 合併多個 PDF byte[] 為單一 PDF。
/// </summary>
/// <remarks>
/// Legacy: SignupForm.cs:1698-1722 (CombinePDFs) — 舊系統用 PdfSharp.PdfDocument + PdfReader.Open(Import) 逐頁 AddPage
/// </remarks>
public interface IPdfMerger
{
    byte[] Merge(IReadOnlyList<byte[]> pdfs);
}
