using ClosedXML.Excel;

namespace Ceremony.Application.Signups;

/// <summary>
/// 匯出報名查詢結果為 Excel (.xlsx)。
/// </summary>
/// <remarks>
/// Legacy: SignupForm.cs:655-728 (btnExportExcel_Click) — 舊用 NPOI HSSF (.xls)；新版改 ClosedXML (.xlsx)
/// Blueprint: docs/blueprints/api-endpoints/post-signups-export.md
/// Coverage:  docs/blueprints/legacy-coverage/signup-form.md (row 17)
/// 32 個欄位順序對齊舊 line 670-700。
/// </remarks>
public sealed class ExportSignupsHandler(SearchSignupsHandler search)
{
    public async Task<(byte[] Bytes, string FileName)> HandleAsync(SignupSearchQuery query, CancellationToken ct = default)
    {
        var result = await search.HandleAsync(query, ct);

        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Search");

        var row = 1;  // ClosedXML 1-based；舊系統 NPOI 0-based 但無 header，新版維持「無 header row，第 1 row 就是資料」對齊舊
        foreach (var s in result.Items)
        {
            sheet.Cell(row, 1).Value = s.Year;
            sheet.Cell(row, 2).Value = s.CeremonyTitle ?? string.Empty;
            sheet.Cell(row, 3).Value = s.NumberTitle ?? string.Empty;
            sheet.Cell(row, 4).Value = s.Number?.ToString() ?? string.Empty;
            sheet.Cell(row, 5).Value = s.Fee?.ToString() ?? string.Empty;
            sheet.Cell(row, 6).Value = s.Employee ?? string.Empty;
            sheet.Cell(row, 7).Value = s.Name ?? string.Empty;
            sheet.Cell(row, 8).Value = s.Remark ?? string.Empty;
            sheet.Cell(row, 9).Value = s.HallName ?? string.Empty;
            // Dead 6 + Living 6（對齊舊順序：先死後生）
            sheet.Cell(row, 10).Value = s.DeadNames[0] ?? string.Empty;
            sheet.Cell(row, 11).Value = s.DeadNames[1] ?? string.Empty;
            sheet.Cell(row, 12).Value = s.DeadNames[2] ?? string.Empty;
            sheet.Cell(row, 13).Value = s.DeadNames[3] ?? string.Empty;
            sheet.Cell(row, 14).Value = s.DeadNames[4] ?? string.Empty;
            sheet.Cell(row, 15).Value = s.DeadNames[5] ?? string.Empty;
            sheet.Cell(row, 16).Value = s.LivingNames[0] ?? string.Empty;
            sheet.Cell(row, 17).Value = s.LivingNames[1] ?? string.Empty;
            sheet.Cell(row, 18).Value = s.LivingNames[2] ?? string.Empty;
            sheet.Cell(row, 19).Value = s.LivingNames[3] ?? string.Empty;
            sheet.Cell(row, 20).Value = s.LivingNames[4] ?? string.Empty;
            sheet.Cell(row, 21).Value = s.LivingNames[5] ?? string.Empty;
            sheet.Cell(row, 22).Value = s.PrepayYear?.ToString() ?? string.Empty;
            sheet.Cell(row, 23).Value = s.PrepayCeremonyTitle ?? string.Empty;
            sheet.Cell(row, 24).Value = s.Phone ?? string.Empty;
            sheet.Cell(row, 25).Value = s.MailCity ?? string.Empty;
            sheet.Cell(row, 26).Value = s.MailZone ?? string.Empty;
            sheet.Cell(row, 27).Value = s.MailAddress ?? string.Empty;
            sheet.Cell(row, 28).Value = s.TextCity ?? string.Empty;
            sheet.Cell(row, 29).Value = s.TextZone ?? string.Empty;
            sheet.Cell(row, 30).Value = s.TextAddress ?? string.Empty;
            sheet.Cell(row, 31).Value = s.AdminName ?? string.Empty;
            sheet.Cell(row, 32).Value = s.CreateDate?.ToString("yyyy-MM-dd HH:mm:ss") ?? string.Empty;
            row++;
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        var bytes = stream.ToArray();

        var fileName = $"signups-{DateTime.UtcNow:yyyyMMddHHmmss}.xlsx";
        return (bytes, fileName);
    }
}
