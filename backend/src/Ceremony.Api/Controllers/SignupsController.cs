using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Ceremony.Application.Signups;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ceremony.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/signups")]
public sealed class SignupsController(
    SearchSignupsHandler search,
    GetSignupHandler get,
    ListSignupLogsHandler logs,
    CreateSignupHandler create,
    UpdateSignupHandler update,
    DeleteSignupHandler delete,
    ExportSignupsHandler export) : ControllerBase
{
    /// <summary>報名查詢（含動態 AND/OR 條件）</summary>
    /// <remarks>
    /// Legacy: SignupForm.cs:71-74 (btnSearch_Click) + :807-864 (LoadSearchSignups PredicateBuilder)
    /// Blueprint: docs/blueprints/api-endpoints/get-signups.md
    /// Coverage:  docs/blueprints/legacy-coverage/signup-form.md (rows 1, 2, 24)
    /// </remarks>
    [HttpGet]
    [ProducesResponseType(typeof(SignupListResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<SignupListResponse>> Search(
        [FromQuery] int? year,
        [FromQuery] bool isScope = false,
        [FromQuery] Guid? ceremonyCategoryId = null,
        [FromQuery] int? signupType = null,
        [FromQuery] int? number = null,
        [FromQuery] string? searchKey = null,
        [FromQuery] bool scopeName = false,
        [FromQuery] bool scopeLivingName = false,
        [FromQuery] bool scopeDeadName = false,
        [FromQuery] bool scopePhone = false,
        [FromQuery] bool isFixedNumber = false,
        CancellationToken ct = default)
    {
        var query = new SignupSearchQuery(
            Year: year,
            IsScope: isScope,
            CeremonyCategoryId: ceremonyCategoryId,
            SignupType: signupType,
            Number: number,
            SearchKey: searchKey,
            ScopeName: scopeName,
            ScopeLivingName: scopeLivingName,
            ScopeDeadName: scopeDeadName,
            ScopePhone: scopePhone,
            IsFixedNumber: isFixedNumber);
        return Ok(await search.HandleAsync(query, ct));
    }

    /// <summary>取得單筆報名</summary>
    /// <remarks>
    /// Legacy: EditSignupForm.cs:70-73, 562-626
    /// Blueprint: docs/blueprints/api-endpoints/get-signup-by-id.md
    /// Coverage:  docs/blueprints/legacy-coverage/edit-signup-form.md (row 2)
    /// </remarks>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(SignupListItem), StatusCodes.Status200OK)]
    public async Task<ActionResult<SignupListItem>> GetById(Guid id, CancellationToken ct)
        => Ok(await get.HandleAsync(id, ct));

    /// <summary>取得報名變更紀錄</summary>
    /// <remarks>
    /// Legacy: SignupLogForm.cs:26-45
    /// Blueprint: docs/blueprints/api-endpoints/get-signup-logs.md
    /// Coverage:  docs/blueprints/legacy-coverage/signup-log-form.md (rows 1, 2)
    /// </remarks>
    [HttpGet("{id:guid}/logs")]
    [ProducesResponseType(typeof(SignupLogListResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<SignupLogListResponse>> GetLogs(Guid id, CancellationToken ct)
        => Ok(await logs.HandleAsync(id, ct));

    /// <summary>新增報名（含 UPDLOCK 編號分配 + SignupLog 同步寫入）</summary>
    /// <remarks>
    /// Legacy: NewSignupForm.cs:151-362 (btnConfirm_Click)
    /// Blueprint: docs/blueprints/api-endpoints/post-signups.md
    /// Coverage:  docs/blueprints/legacy-coverage/new-signup-form.md (rows 6, 14-18, 25)
    /// </remarks>
    [HttpPost]
    [ProducesResponseType(typeof(SignupListItem), StatusCodes.Status201Created)]
    public async Task<ActionResult<SignupListItem>> Create(
        [FromBody] CreateSignupRequest request,
        CancellationToken ct)
    {
        var caller = ExtractCaller(User);
        var result = await create.HandleAsync(request, caller, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    /// <summary>編輯報名（全欄位覆寫 + SignupLog 同步寫入）</summary>
    /// <remarks>
    /// Legacy: EditSignupForm.cs:186-368 (btnConfirm_Click)
    /// Blueprint: docs/blueprints/api-endpoints/put-signup.md
    /// Coverage:  docs/blueprints/legacy-coverage/edit-signup-form.md (rows 9-13)
    /// </remarks>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(SignupListItem), StatusCodes.Status200OK)]
    public async Task<ActionResult<SignupListItem>> Update(
        Guid id,
        [FromBody] CreateSignupRequest request,
        CancellationToken ct)
    {
        var caller = ExtractCaller(User);
        var result = await update.HandleAsync(id, request, caller, ct);
        return Ok(result);
    }

    /// <summary>刪除報名（硬刪除）</summary>
    /// <remarks>
    /// Legacy: SignupForm.cs:405-426 (tsmiDelete_Click)
    /// Blueprint: docs/blueprints/api-endpoints/delete-signup.md
    /// Coverage:  docs/blueprints/legacy-coverage/signup-form.md (row 14)
    /// </remarks>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await delete.HandleAsync(id, ct);
        return NoContent();
    }

    /// <summary>匯出 Excel (.xlsx)，32 欄</summary>
    /// <remarks>
    /// Legacy: SignupForm.cs:655-728 (btnExportExcel_Click) — 舊用 NPOI HSSF，新版改 ClosedXML
    /// Blueprint: docs/blueprints/api-endpoints/post-signups-export.md
    /// Coverage:  docs/blueprints/legacy-coverage/signup-form.md (row 17)
    /// </remarks>
    [HttpPost("export")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Export([FromBody] SignupSearchQuery query, CancellationToken ct)
    {
        var (bytes, fileName) = await export.HandleAsync(query, ct);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
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
