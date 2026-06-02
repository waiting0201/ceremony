using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Ceremony.Application.Prepay;
using Ceremony.Application.Signups;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ceremony.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/prepay")]
public sealed class PrepayController(
    PrepayLoadHandler load,
    GetBelieverLatestPrepayHandler latestPrepay) : ControllerBase
{
    /// <summary>取某信眾今年(含)以前最新報名的預繳資訊（新增報名選信眾時自動帶入預繳年/法會）。</summary>
    /// <remarks>
    /// Legacy: NewSignupForm.cs:1102-1115 (BelieverSelected 取最新報名預繳代入)
    /// Blueprint: docs/blueprints/api-endpoints/get-prepay-believer-latest.md
    /// Coverage:  docs/blueprints/legacy-coverage/new-signup-form.md (row 34)
    /// </remarks>
    [HttpGet]
    [ProducesResponseType(typeof(BelieverLatestPrepayResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<BelieverLatestPrepayResult>> BelieverLatest(
        [FromQuery] Guid believerId,
        [FromQuery] int? year,
        CancellationToken ct)
    {
        var y = year ?? System.DateTime.Now.Year - 1911; // 預設當前民國年
        return Ok(await latestPrepay.HandleAsync(believerId, y, ct));
    }

    /// <summary>批次載入預繳資料（6 分組 strategy + 自動 dedup）</summary>
    /// <remarks>
    /// Legacy: LoadPrepayForm.cs:45-824 (780-line btnConfirm_Click 6-case switch)
    /// Blueprint: docs/blueprints/api-endpoints/post-prepay-load.md
    /// Coverage:  docs/blueprints/legacy-coverage/load-prepay-form.md (rows 2, 3)
    /// </remarks>
    [HttpPost("load")]
    [ProducesResponseType(typeof(PrepayLoadResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<PrepayLoadResponse>> Load(
        [FromBody] PrepayLoadRequest request,
        CancellationToken ct)
    {
        var caller = ExtractCaller(User);
        var result = await load.HandleAsync(request, caller, ct);
        return Ok(result);
    }

    private static CallerContext ExtractCaller(ClaimsPrincipal user)
    {
        var sub = user.FindFirstValue(JwtRegisteredClaimNames.Sub)
                  ?? user.FindFirstValue(ClaimTypes.NameIdentifier);
        var adminId = int.TryParse(sub, out var id) ? id : 0;

        var name = user.FindFirstValue("name")
                   ?? user.FindFirstValue(JwtRegisteredClaimNames.UniqueName)
                   ?? user.Identity?.Name
                   ?? "unknown";
        return new CallerContext(adminId, name);
    }
}
