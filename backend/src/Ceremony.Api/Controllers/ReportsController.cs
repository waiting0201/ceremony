using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Ceremony.Application.Reports;
using Ceremony.Domain.Reports;
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
    BatchPrintJobService jobs,
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
        AppendPageSize("datacard");
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
        AppendPageSize("receipt");
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
        AppendPageSize("tablet");
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
        AppendPageSize("tablet");
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
        AppendPageSize("text");
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
        AppendPageSize("worship");
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
        AppendPageSize("worshipcard");
        return File(pdf, "application/pdf", fileName);
    }

    /// <summary>建立批次列印背景工作，立刻回 jobId 與總筆數（驗證與查詢仍是同步的）。</summary>
    /// <remarks>Blueprint: docs/blueprints/api-endpoints/post-reports-batch-jobs.md</remarks>
    [HttpPost("batch/jobs")]
    public async Task<IActionResult> CreateBatchJob([FromBody] BatchReportRequest req, CancellationToken ct)
    {
        // 驗證＋查詢留在這裡 → VALIDATION_INVALID / BATCH_NO_SIGNUPS 的狀態碼與訊息與同步版完全相同
        var plan = await batch.ResolveAsync(req, ct);
        var created = jobs.Start(plan, OwnerSub());
        return Accepted(created);
    }

    /// <summary>查詢批次列印工作進度。前端每 250ms 輪詢一次。</summary>
    /// <remarks>Blueprint: docs/blueprints/api-endpoints/post-reports-batch-jobs.md</remarks>
    [HttpGet("batch/jobs/{jobId:guid}")]
    public IActionResult GetBatchJob(Guid jobId)
        => Ok(jobs.GetState(jobId, OwnerSub()));

    /// <summary>取出批次列印成品 PDF。成功後 job 立即釋放（one-shot）。</summary>
    /// <remarks>
    /// Blueprint: docs/blueprints/api-endpoints/post-reports-batch-jobs.md
    ///
    /// 成品是暫存檔而非 byte[]（取消分段後單一批次可達數百 MB），所以用
    /// <see cref="FileOptions.DeleteOnClose"/> 串流回應：ASP.NET 送完會 dispose stream，
    /// 檔案跟著消失——不需要另外排清理，客戶端中途斷線也一樣會刪。
    /// </remarks>
    [HttpGet("batch/jobs/{jobId:guid}/file")]
    public IActionResult GetBatchJobFile(Guid jobId)
    {
        var (pdfPath, fileName, total, reportType) = jobs.TakeFile(jobId, OwnerSub());
        Response.Headers.Append("X-Signup-Count", total.ToString());
        AppendPageSize(reportType);

        var stream = new FileStream(
            pdfPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 64 * 1024,
            FileOptions.Asynchronous | FileOptions.DeleteOnClose);

        return File(stream, "application/pdf", fileName);
    }

    /// <summary>取消批次列印工作。冪等：未知 id 也回 204。</summary>
    /// <remarks>Blueprint: docs/blueprints/api-endpoints/post-reports-batch-jobs.md</remarks>
    [HttpDelete("batch/jobs/{jobId:guid}")]
    public IActionResult CancelBatchJob(Guid jobId)
    {
        jobs.Cancel(jobId, OwnerSub());
        return NoContent();
    }

    /// <summary>
    /// 掛上 X-Report-Page-Size（微米，如 "210000x148000"），供 Electron 主行程送印時指定 pageSize。
    /// </summary>
    /// <remarks>
    /// 沒有這個 header，列印端只能猜紙張 → 檢視器/驅動各自套預設值，就是「有的印表機可以、有的要手動調」
    /// 的根因。值的權威是 Ceremony.Domain.Reports.ReportPageSizes。
    /// 不在 CORS safelist，Program.cs 需明示 WithExposedHeaders 前端才讀得到。
    /// 契約見 docs/blueprints/print-channel-electron.md。
    /// </remarks>
    private void AppendPageSize(string reportType)
    {
        if (ReportPageSizes.TryGet(reportType, out var size))
            Response.Headers.Append("X-Report-Page-Size", size.ToHeaderValue());
    }

    /// <summary>
    /// 取本次請求的使用者識別，用來把 job 綁在建立者身上。
    /// </summary>
    /// <remarks>
    /// JwtBearer 預設 MapInboundClaims=true 會把 "sub" 映射成 ClaimTypes.NameIdentifier，
    /// 所以要先查 NameIdentifier；直接查 "sub" 會拿到 null。見 docs/gotchas.md。
    /// </remarks>
    private string OwnerSub()
        => User.FindFirstValue(ClaimTypes.NameIdentifier)
           ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub)
           ?? string.Empty;
}
