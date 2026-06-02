namespace Ceremony.Application.Zipcodes;

/// <summary>
/// 取得縣市清單（城市下拉）。
/// </summary>
/// <remarks>
/// Legacy: NewSignupForm.cs:662-677 (LoadCity)
/// Blueprint: docs/blueprints/api-endpoints/get-zipcodes.md
/// </remarks>
public sealed class ListZipcodeCitiesHandler(IZipcodeRepository repo)
{
    public async Task<ZipcodeCitiesResponse> HandleAsync(CancellationToken ct = default)
    {
        var cities = await repo.GetCitiesAsync(ct);
        return new ZipcodeCitiesResponse(cities, cities.Count);
    }
}
