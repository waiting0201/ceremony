using Ceremony.Domain.Exceptions;

namespace Ceremony.Application.Categories;

/// <summary>
/// 新增分類（含兩層階層限制）。
/// </summary>
/// <remarks>
/// Legacy: CeremonyCategoryForm.cs:94-114 (btnConfirm insert path)
/// Blueprint: docs/blueprints/api-endpoints/post-categories.md
/// </remarks>
public sealed class CreateCategoryHandler(ICategoryRepository repo)
{
    public async Task<CategoryItem> HandleAsync(CreateCategoryRequest req, CancellationToken ct = default)
    {
        var title = (req.Title ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(title))
            throw new DomainException("VALIDATION_REQUIRED", "請輸入名稱");
        if (title.Length > 50)
            throw new DomainException("VALIDATION_LENGTH", "名稱最多 50 個字");

        if (req.ParentId is { } pid)
        {
            var parent = await repo.GetByIdAsync(pid, ct)
                ?? throw new DomainException("CATEGORY_NOT_FOUND", "找不到父分類");
            if (parent.ParentId is not null)
                throw new DomainException("CATEGORY_DEPTH_LIMIT", "第一層之下不可再新增");
        }

        var id = await repo.InsertAsync(title, req.Sort, req.ParentId, ct);
        return new CategoryItem(id, title, req.Sort, req.ParentId);
    }
}

/// <summary>
/// 編輯分類（Title + Sort；不可改 ParentID）。
/// </summary>
/// <remarks>
/// Legacy: CeremonyCategoryForm.cs:115-127 (btnConfirm update path)
/// </remarks>
public sealed class UpdateCategoryHandler(ICategoryRepository repo)
{
    public async Task<CategoryItem> HandleAsync(Guid id, UpdateCategoryRequest req, CancellationToken ct = default)
    {
        var title = (req.Title ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(title))
            throw new DomainException("VALIDATION_REQUIRED", "請輸入名稱");
        if (title.Length > 50)
            throw new DomainException("VALIDATION_LENGTH", "名稱最多 50 個字");

        var existing = await repo.GetByIdAsync(id, ct)
            ?? throw new DomainException("CATEGORY_NOT_FOUND", "找不到法會");

        var ok = await repo.UpdateAsync(id, title, req.Sort, ct);
        if (!ok)
            throw new DomainException("CATEGORY_NOT_FOUND", "找不到法會");

        return new CategoryItem(id, title, req.Sort, existing.ParentId);
    }
}

/// <summary>
/// 刪除分類（雙重檢查：無報名且無子分類）。
/// </summary>
/// <remarks>
/// Legacy: CeremonyCategoryForm.cs:143-165 (tsmiDelete_Click)
/// Blueprint: docs/blueprints/api-endpoints/delete-category.md
/// </remarks>
public sealed class DeleteCategoryHandler(ICategoryRepository repo)
{
    public async Task HandleAsync(Guid id, CancellationToken ct = default)
    {
        var existing = await repo.GetByIdAsync(id, ct)
            ?? throw new DomainException("CATEGORY_NOT_FOUND", "找不到法會");

        if (await repo.HasDependencyAsync(id, ct))
            throw new DomainException("CATEGORY_HAS_DEPENDENCY", "已有報名或還有下層法會，無法刪除");

        var ok = await repo.DeleteAsync(id, ct);
        if (!ok)
            throw new DomainException("CATEGORY_NOT_FOUND", "找不到法會");
    }
}
