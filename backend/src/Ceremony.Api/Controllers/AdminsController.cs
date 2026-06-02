using Ceremony.Application.Admins;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ceremony.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/admins")]
public sealed class AdminsController(
    ListAdminsHandler list,
    CreateAdminHandler create,
    UpdateAdminHandler update,
    DeleteAdminHandler delete) : ControllerBase
{
    /// <summary>列出所有啟用的管理者（不含 password）</summary>
    /// <remarks>
    /// Legacy: AdminsForm.cs:207-213 (LoadAdmins helper)
    /// Blueprint: docs/blueprints/api-endpoints/get-admins.md
    /// Coverage:  docs/blueprints/legacy-coverage/admins-form.md (rows 1, 12)
    /// </remarks>
    [HttpGet]
    [ProducesResponseType(typeof(AdminListResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<AdminListResponse>> List(CancellationToken ct)
    {
        var result = await list.HandleAsync(ct);
        return Ok(result);
    }

    /// <summary>新增管理者</summary>
    /// <remarks>
    /// Legacy: AdminsForm.cs:88-105 (btnConfirm insert) + :160-187 (uniqueness) + :189-196 (pwd required)
    /// Blueprint: docs/blueprints/api-endpoints/post-admins.md
    /// Coverage:  docs/blueprints/legacy-coverage/admins-form.md (rows 3, 6, 9, 10)
    /// </remarks>
    [HttpPost]
    [ProducesResponseType(typeof(AdminListItem), StatusCodes.Status201Created)]
    public async Task<ActionResult<AdminListItem>> Create(
        [FromBody] CreateAdminRequest request,
        CancellationToken ct)
    {
        var result = await create.HandleAsync(request, ct);
        return CreatedAtAction(nameof(List), new { id = result.Id }, result);
    }

    /// <summary>更新管理者（name + 選擇性 password；username 不可變更）</summary>
    /// <remarks>Legacy: AdminsForm.cs:88-122 (btnConfirm update path)</remarks>
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(AdminListItem), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AdminListItem>> Update(
        int id,
        [FromBody] UpdateAdminRequest request,
        CancellationToken ct)
    {
        var result = await update.HandleAsync(id, request, ct);
        return Ok(result);
    }

    /// <summary>軟刪除管理者（IsEnabled = 0）</summary>
    /// <remarks>Legacy: AdminsForm.cs:134-158 (tsmiDelete_Click)</remarks>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await delete.HandleAsync(id, ct);
        return NoContent();
    }
}
