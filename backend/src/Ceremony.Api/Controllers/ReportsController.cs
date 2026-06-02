using Ceremony.Application.Reports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ceremony.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/reports")]
public sealed class ReportsController(
    GenerateDataCardHandler dataCard,
    GenerateReceiptHandler receipt,
    GenerateTabletHandler tablet,
    GenerateTextHandler text,
    GenerateWorshipHandler worship,
    BatchReportHandler batch) : ControllerBase
{
    /// <summary>產生報名資料卡 PDF (A5 橫 21×14.8cm)</summary>
    /// <remarks>
    /// Legacy: SignupForm.cs:188-240 (tsmiPrintDataCard) + :956-1050 (PrintDataCard helper) + tmpDataCard.rdlc
    /// </remarks>
    [HttpGet("datacard")]
    public async Task<IActionResult> DataCard([FromQuery] Guid signupId, CancellationToken ct)
    {
        var (pdf, fileName) = await dataCard.HandleAsync(signupId, ct);
        return File(pdf, "application/pdf", fileName);
    }

    /// <summary>產生收據 PDF (A4 直，雙聯)</summary>
    /// <remarks>
    /// Legacy: SignupForm.cs:1052-1146 (PrintReceipt) + tmpReceipt.rdlc
    /// </remarks>
    [HttpGet("receipt")]
    public async Task<IActionResult> Receipt([FromQuery] Guid signupId, CancellationToken ct)
    {
        var (pdf, fileName) = await receipt.HandleAsync(signupId, ct);
        return File(pdf, "application/pdf", fileName);
    }

    /// <summary>產生薦牌 PDF (11.5×25.4cm 窄長；9 變體選擇)</summary>
    /// <remarks>
    /// Legacy: SignupForm.cs:1148-1333 (PrintTablet) + tmpTablet*.rdlc 9 變體
    /// 變體選擇由 Domain.Services.PrintTemplateSelector.ChooseTablet 決定。
    /// </remarks>
    [HttpGet("tablet")]
    public async Task<IActionResult> Tablet([FromQuery] Guid signupId, CancellationToken ct)
    {
        var (pdf, fileName) = await tablet.HandleAsync(signupId, ct);
        return File(pdf, "application/pdf", fileName);
    }

    /// <summary>產生文牒 PDF (36.5×26.2cm 橫寬；2 變體)</summary>
    /// <remarks>
    /// Legacy: SignupForm.cs:1335-1552 (PrintText) + tmpText.rdlc / tmpTextTwo.rdlc
    /// </remarks>
    [HttpGet("text")]
    public async Task<IActionResult> Text([FromQuery] Guid signupId, CancellationToken ct)
    {
        var (pdf, fileName) = await text.HandleAsync(signupId, ct);
        return File(pdf, "application/pdf", fileName);
    }

    /// <summary>產生普桌 PDF (A4 直；6 變體；僅 SignupType=4)</summary>
    /// <remarks>
    /// Legacy: SignupForm.cs:1554-1696 (PrintWorship) + tmpWorship*.rdlc 6 變體
    /// </remarks>
    [HttpGet("worship")]
    public async Task<IActionResult> Worship([FromQuery] Guid signupId, CancellationToken ct)
    {
        var (pdf, fileName) = await worship.HandleAsync(signupId, ct);
        return File(pdf, "application/pdf", fileName);
    }

    /// <summary>按編號範圍批次列印同一類報表，合併為單一 PDF。</summary>
    /// <remarks>
    /// Legacy: SignupForm.cs:447-653 (btnPrint_Click) + :1698-1722 (CombinePDFs PdfSharp)
    /// Blueprint: docs/blueprints/api-endpoints/post-reports-batch.md
    /// </remarks>
    [HttpPost("batch")]
    public async Task<IActionResult> Batch([FromBody] BatchReportRequest req, CancellationToken ct)
    {
        var (pdf, fileName, count) = await batch.HandleAsync(req, ct);
        Response.Headers.Append("X-Signup-Count", count.ToString());
        return File(pdf, "application/pdf", fileName);
    }
}
