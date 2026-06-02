namespace Ceremony.Application.Categories;

public interface ICategoryRepository
{
    Task<IReadOnlyList<CategoryRow>> GetAllAsync(CancellationToken ct = default);

    Task<CategoryRow?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<Guid> InsertAsync(string title, int sort, Guid? parentId, CancellationToken ct = default);

    /// <returns>true if updated; false if id not found.</returns>
    Task<bool> UpdateAsync(Guid id, string title, int sort, CancellationToken ct = default);

    /// <summary>檢查依賴：是否有 Signups 引用 OR 是否有子分類。</summary>
    Task<bool> HasDependencyAsync(Guid id, CancellationToken ct = default);

    /// <returns>true if deleted; false if id not found.</returns>
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
}
