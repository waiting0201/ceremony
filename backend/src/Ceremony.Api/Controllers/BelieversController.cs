using Ceremony.Application.Believers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ceremony.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/believers")]
public sealed class BelieversController(
    SearchBelieversHandler search,
    GetBelieverHandler get,
    CreateBelieverHandler create,
    UpdateBelieverHandler update,
    DeleteBelieverHandler delete) : ControllerBase
{
    /// <summary>搜尋信眾（至少需給 1 個搜尋條件）</summary>
    /// <remarks>
    /// Legacy: BelieverForm.cs:35-44 (btnSearch_Click) + :353-409 (LoadBelievers)
    /// Blueprint: docs/blueprints/api-endpoints/get-believers.md
    /// Coverage:  docs/blueprints/legacy-coverage/believer-form.md (rows 2, 13)
    /// </remarks>
    [HttpGet]
    [ProducesResponseType(typeof(BelieverListResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<BelieverListResponse>> Search(
        [FromQuery] string? name,
        [FromQuery] string? phone,
        [FromQuery] string? hallName,
        [FromQuery] string? livingName,
        [FromQuery] string? deadName,
        CancellationToken ct)
    {
        var query = new BelieverSearchQuery(name, phone, hallName, livingName, deadName);
        var result = await search.HandleAsync(query, ct);
        return Ok(result);
    }

    /// <summary>取得單筆信眾</summary>
    /// <remarks>
    /// Legacy: BelieverForm.cs:57-99 (CellClick prefill)
    /// Blueprint: docs/blueprints/api-endpoints/get-believer-by-id.md
    /// Coverage:  docs/blueprints/legacy-coverage/believer-form.md (row 4)
    /// </remarks>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(BelieverListItem), StatusCodes.Status200OK)]
    public async Task<ActionResult<BelieverListItem>> GetById(Guid id, CancellationToken ct)
        => Ok(await get.HandleAsync(id, ct));

    /// <summary>新增信眾</summary>
    /// <remarks>
    /// Legacy: BelieverForm.cs:101-152 (btnConfirm insert)
    /// Blueprint: docs/blueprints/api-endpoints/post-believers.md
    /// Coverage:  docs/blueprints/legacy-coverage/believer-form.md (rows 3, 5, 12)
    /// </remarks>
    [HttpPost]
    [ProducesResponseType(typeof(BelieverListItem), StatusCodes.Status201Created)]
    public async Task<ActionResult<BelieverListItem>> Create(
        [FromBody] BelieverUpsertRequest request,
        CancellationToken ct)
    {
        var result = await create.HandleAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    /// <summary>編輯信眾（全欄位覆寫）</summary>
    /// <remarks>
    /// Legacy: BelieverForm.cs:154-185 (btnConfirm edit)
    /// Blueprint: docs/blueprints/api-endpoints/put-believer.md
    /// Coverage:  docs/blueprints/legacy-coverage/believer-form.md (row 5)
    /// </remarks>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(BelieverListItem), StatusCodes.Status200OK)]
    public async Task<ActionResult<BelieverListItem>> Update(
        Guid id,
        [FromBody] BelieverUpsertRequest request,
        CancellationToken ct)
        => Ok(await update.HandleAsync(id, request, ct));

    /// <summary>刪除信眾（硬刪除；報名衝突回 409）</summary>
    /// <remarks>
    /// Legacy: BelieverForm.cs:211-250 (tsmiDelete with HasSignups check)
    /// Blueprint: docs/blueprints/api-endpoints/delete-believer.md
    /// Coverage:  docs/blueprints/legacy-coverage/believer-form.md (row 8)
    /// </remarks>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await delete.HandleAsync(id, ct);
        return NoContent();
    }
}
