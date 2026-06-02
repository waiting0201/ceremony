using Ceremony.Application.Reports;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace Ceremony.Infrastructure.Reporting;

/// <summary>
/// PdfSharp 實作合併多個 PDF 為單一 PDF。
/// </summary>
/// <remarks>
/// Legacy: SignupForm.cs:1698-1722 (CombinePDFs) — 移植自舊系統 .NET Framework 版本到 PdfSharp 6.x (.NET 10)
/// </remarks>
public sealed class PdfSharpMerger : IPdfMerger
{
    public byte[] Merge(IReadOnlyList<byte[]> pdfs)
    {
        if (pdfs.Count == 0) return [];
        if (pdfs.Count == 1) return pdfs[0];

        using var resultStream = new MemoryStream();
        using (var resultPdf = new PdfDocument())
        {
            foreach (var pdfBytes in pdfs)
            {
                using var srcStream = new MemoryStream(pdfBytes);
                using var srcPdf = PdfReader.Open(srcStream, PdfDocumentOpenMode.Import);
                for (var i = 0; i < srcPdf.PageCount; i++)
                {
                    resultPdf.AddPage(srcPdf.Pages[i]);
                }
            }
            resultPdf.Save(resultStream, closeStream: false);
        }
        return resultStream.ToArray();
    }
}
