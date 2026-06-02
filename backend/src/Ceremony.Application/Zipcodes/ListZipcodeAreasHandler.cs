namespace Ceremony.Application.Zipcodes;

/// <summary>
/// 取得某縣市的鄉鎮區清單（區域下拉，連動城市）。
/// </summary>
/// <remarks>
/// Legacy: NewSignupForm.cs:406-424 (dlMailCity_SelectedIndexChanged) / 441-460 (dlTextCity_...)
/// Blueprint: docs/blueprints/api-endpoints/get-zipcodes.md
/// </remarks>
public sealed class ListZipcodeAreasHandler(IZipcodeRepository repo)
{
    public async Task<ZipcodeAreasResponse> HandleAsync(string city, CancellationToken ct = default)
    {
        var rows = await repo.GetByCityAsync(city, ct);
        var items = rows
            .Select(r => new ZipcodeAreaItem(r.ZipcodeId, r.City, r.Area, r.Zipcode))
            .ToList();
        return new ZipcodeAreasResponse(items, items.Count);
    }
}
