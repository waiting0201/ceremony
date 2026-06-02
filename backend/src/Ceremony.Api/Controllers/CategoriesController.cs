using Ceremony.Application.Categories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ceremony.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/categories")]
public sealed class CategoriesController(
    ListCategoriesHandler list,
    CreateCategoryHandler create,
    UpdateCategoryHandler update,
    DeleteCategoryHandler delete) : ControllerBase
{
    /// <summary>取得法會分類兩層樹狀結構</summary>
    /// <remarks>
    /// Legacy: CeremonyCategoryForm.cs:167-195
    /// Blueprint: docs/blueprints/api-endpoints/get-categories.md
    /// </remarks>
    [HttpGet]
    [ProducesResponseType(typeof(CategoryListResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<CategoryListResponse>> List(CancellationToken ct)
        => Ok(await list.HandleAsync(ct));

    /// <summary>新增法會分類（含兩層階層限制）</summary>
    /// <remarks>
    /// Legacy: CeremonyCategoryForm.cs:94-114
    /// Blueprint: docs/blueprints/api-endpoints/post-categories.md
    /// </remarks>
    [HttpPost]
    [ProducesResponseType(typeof(CategoryItem), StatusCodes.Status201Created)]
    public async Task<ActionResult<CategoryItem>> Create(
        [FromBody] CreateCategoryRequest request,
        CancellationToken ct)
    {
        var result = await create.HandleAsync(request, ct);
        return CreatedAtAction(nameof(List), new { id = result.Id }, result);
    }

    /// <summary>編輯法會分類（Title + Sort）</summary>
    /// <remarks>
    /// Legacy: CeremonyCategoryForm.cs:115-127
    /// Blueprint: docs/blueprints/api-endpoints/put-category.md
    /// </remarks>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(CategoryItem), StatusCodes.Status200OK)]
    public async Task<ActionResult<CategoryItem>> Update(
        Guid id,
        [FromBody] UpdateCategoryRequest request,
        CancellationToken ct)
        => Ok(await update.HandleAsync(id, request, ct));

    /// <summary>刪除法會分類（雙重檢查：無報名且無子分類）</summary>
    /// <remarks>
    /// Legacy: CeremonyCategoryForm.cs:143-165
    /// Blueprint: docs/blueprints/api-endpoints/delete-category.md
    /// </remarks>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await delete.HandleAsync(id, ct);
        return NoContent();
    }
}
