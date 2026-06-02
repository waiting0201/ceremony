namespace Ceremony.Application.Zipcodes;

public interface IZipcodeRepository
{
    /// <summary>distinct 縣市，依 City 排序（對齊舊 LoadCity）。</summary>
    Task<IReadOnlyList<string>> GetCitiesAsync(CancellationToken ct = default);

    /// <summary>某縣市的鄉鎮區，依 Zipcode 排序（對齊舊 dlMailCity_SelectedIndexChanged）。</summary>
    Task<IReadOnlyList<ZipcodeRow>> GetByCityAsync(string city, CancellationToken ct = default);
}
