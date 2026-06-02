namespace Ceremony.Application.Categories;

/// <summary>
/// 取得法會分類兩層樹狀結構。
/// </summary>
/// <remarks>
/// Legacy: CeremonyCategoryForm.cs:167-195 (LoadCeremonyCategorys + CreateRootNode + CreateNode 遞迴)
/// Blueprint: docs/blueprints/api-endpoints/get-categories.md
/// Coverage:  docs/blueprints/legacy-coverage/ceremony-category-form.md (rows 1, 7, 8, 9)
/// 注意：兩層階層由 DB 既有資料 enforce；應用層用單層 lookup 不再遞迴。
/// </remarks>
public sealed class ListCategoriesHandler(ICategoryRepository repo)
{
    public async Task<CategoryListResponse> HandleAsync(CancellationToken ct = default)
    {
        var rows = await repo.GetAllAsync(ct);

        var byParent = rows
            .Where(r => r.ParentId.HasValue)
            .GroupBy(r => r.ParentId!.Value)
            .ToDictionary(g => g.Key, g => g
                .OrderBy(c => c.Sort)
                .Select(c => new CategoryNode(c.CeremonyCategoryId, c.Title, c.Sort, []))
                .ToList());

        var roots = rows
            .Where(r => !r.ParentId.HasValue)
            .OrderBy(r => r.Sort)
            .Select(r => new CategoryNode(
                r.CeremonyCategoryId,
                r.Title,
                r.Sort,
                byParent.TryGetValue(r.CeremonyCategoryId, out var children)
                    ? children
                    : []))
            .ToList();

        return new CategoryListResponse(roots, roots.Count);
    }
}
