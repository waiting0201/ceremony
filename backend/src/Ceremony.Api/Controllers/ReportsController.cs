using Ceremony.Application.Reports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;

namespace Ceremony.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/reports")]
public sealed class ReportsController(
    GenerateDataCardHandler dataCard,
    GenerateReceiptHandler receipt,
    GenerateTabletHandler tablet,
    GenerateTabletSampleHandler tabletSample,
    GenerateTextHandler text,
    GenerateWorshipHandler worship,
    GenerateWorshipCardHandler worshipCard,
    BatchReportHandler batch,
    IHostEnvironment env) : ControllerBase
{
    /// <summary>產生報名資料卡 PDF (A5 橫 21×14.8cm)</summary>
    /// <remarks>
    /// Legacy: SignupForm.cs:188-240 (tsmiPrintDataCard) + :956-1050 (PrintDataCard helper) + tmpDataCard.rdlc
    /// debugOverlay：開發用列印位置檢視工具（樣板疊圖），僅 Development 環境可用，見
    /// docs/blueprints/printing-reports.md「開發用列印位置檢視工具」。
    /// </remarks>
    [HttpGet("datacard")]
    public async Task<IActionResult> DataCard([FromQuery] Guid signupId, [FromQuery] bool debugOverlay, CancellationToken ct)
    {
        if (debugOverlay && !env.IsDevelopment()) return NotFound();

        var (pdf, fileName) = await dataCard.HandleAsync(signupId, debugOverlay, ct);
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
    /// debugOverlay：開發用列印位置檢視工具（樣板疊圖），僅 Development 環境可用，見
    /// docs/blueprints/printing-reports.md「開發用列印位置檢視工具」。
    /// </remarks>
    [HttpGet("tablet")]
    public async Task<IActionResult> Tablet([FromQuery] Guid signupId, [FromQuery] bool debugOverlay, CancellationToken ct)
    {
        if (debugOverlay && !env.IsDevelopment()) return NotFound();

        var (pdf, fileName) = await tablet.HandleAsync(signupId, debugOverlay, ct);
        return File(pdf, "application/pdf", fileName);
    }

    /// <summary>開發用：薦牌「5 位亡者 + 5 位陽上」固定樣本 PDF（不依賴 DB，Base 變體）</summary>
    /// <remarks>
    /// 僅 Development 環境可用（同 debugOverlay），供搭配 <c>?debugOverlay=true</c> 樣板疊圖直接檢視列印位置，
    /// 不需要在 DB 建一筆對應的報名資料。見 docs/blueprints/printing-reports.md「開發用列印位置檢視工具」。
    /// </remarks>
    [HttpGet("tablet/sample")]
    public IActionResult TabletSample([FromQuery] bool debugOverlay = false)
    {
        if (!env.IsDevelopment()) return NotFound();

        var pdf = tabletSample.Handle(debugOverlay);
        return File(pdf, "application/pdf", "tablet-sample-5dead-5living.pdf");
    }

    /// <summary>產生文牒 PDF (36.5×26.2cm 橫寬；2 變體)</summary>
    /// <remarks>
    /// Legacy: SignupForm.cs:1335-1552 (PrintText) + tmpText.rdlc / tmpTextTwo.rdlc
    /// debugOverlay：開發用列印位置檢視工具（樣板疊圖），僅 Development 環境可用，見
    /// docs/blueprints/printing-reports.md「開發用列印位置檢視工具」。
    /// </remarks>
    [HttpGet("text")]
    public async Task<IActionResult> Text([FromQuery] Guid signupId, [FromQuery] bool debugOverlay, CancellationToken ct)
    {
        if (debugOverlay && !env.IsDevelopment()) return NotFound();

        var (pdf, fileName) = await text.HandleAsync(signupId, debugOverlay, ct);
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

    /// <summary>產生普桌資料卡 PDF (A5 橫 21×14.8cm，template 全印白紙可印；葫蘆內 6 變體；僅 SignupType=4)</summary>
    /// <remarks>
    /// 全新報表（舊系統無對應 RDLC）。Blueprint: docs/blueprints/api-endpoints/get-reports-worshipcard.md
    /// debugOverlay：開發用列印位置檢視工具（樣板疊圖），僅 Development 環境可用，見
    /// docs/blueprints/printing-reports.md「開發用列印位置檢視工具」。
    /// </remarks>
    [HttpGet("worshipcard")]
    public async Task<IActionResult> WorshipCard([FromQuery] Guid signupId, [FromQuery] bool debugOverlay, CancellationToken ct)
    {
        if (debugOverlay && !env.IsDevelopment()) return NotFound();

        var (pdf, fileName) = await worshipCard.HandleAsync(signupId, debugOverlay, ct);
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
