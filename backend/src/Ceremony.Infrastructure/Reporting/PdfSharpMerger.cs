using Ceremony.Application.Reports;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace Ceremony.Infrastructure.Reporting;

/// <summary>
/// PdfSharp 實作合併多個 PDF 為單一 PDF，來源與成品都走檔案。
/// </summary>
/// <remarks>
/// Legacy: SignupForm.cs:1698-1722 (CombinePDFs) — 移植自舊系統 .NET Framework 版本到 PdfSharp 6.x (.NET 10)
///
/// 成品寫 FileStream 而非 MemoryStream：後者有 2 GB 硬上限，而取消分段後單一批次可達數百 MB
/// （實測 799 筆 datacard = 107 MB）。⚠️ 這只解掉 2 GB 上限與 ToArray() 的整份複製——
/// PdfSharp 的 AddPage 會把來源頁面複製進目標 document 的物件表，**峰值仍與總頁數相關**。
/// 真正的常數峰值需要換 PDF library 或 append-mode 合併，見 docs/design/performance.md。
/// </remarks>
public sealed class PdfSharpMerger : IPdfMerger
{
    public void Merge(IReadOnlyList<string> srcPaths, string destPath)
    {
        // 單筆批次直接複製：不重新編碼，成品與單筆報表端點的輸出逐位元相同
        if (srcPaths.Count == 1)
        {
            File.Copy(srcPaths[0], destPath, overwrite: true);
            return;
        }

        using var dest = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None);
        using var resultPdf = new PdfDocument();

        foreach (var srcPath in srcPaths)
        {
            using var srcPdf = PdfReader.Open(srcPath, PdfDocumentOpenMode.Import);
            for (var i = 0; i < srcPdf.PageCount; i++)
            {
                resultPdf.AddPage(srcPdf.Pages[i]);
            }
        }

        resultPdf.Save(dest, closeStream: false);
    }
}
