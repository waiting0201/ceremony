namespace Ceremony.Application.Categories;

/// <summary>
/// 法會分類列表項（樹節點）。Children 可空陣列。
/// </summary>
public sealed record CategoryNode(
    Guid Id,
    string Title,
    int Sort,
    IReadOnlyList<CategoryNode> Children);

public sealed record CategoryListResponse(IReadOnlyList<CategoryNode> Items, int Total);

/// <summary>Repository 回傳的 flat row（無 tree 結構）。</summary>
public sealed record CategoryRow(Guid CeremonyCategoryId, string Title, Guid? ParentId, int Sort);

/// <summary>新增分類請求。</summary>
public sealed record CreateCategoryRequest(string Title, int Sort, Guid? ParentId = null);

/// <summary>編輯分類請求（不可改 ParentID）。</summary>
public sealed record UpdateCategoryRequest(string Title, int Sort);

/// <summary>單筆分類回傳（POST/PUT response）。</summary>
public sealed record CategoryItem(Guid Id, string Title, int Sort, Guid? ParentId);
