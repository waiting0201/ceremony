namespace Ceremony.Application.Zipcodes;

/// <summary>
/// 縣市清單回傳（distinct City）。對齊舊 NewSignupForm.LoadCity（GroupBy City）。
/// </summary>
public sealed record ZipcodeCitiesResponse(IReadOnlyList<string> Items, int Total);

/// <summary>單筆鄉鎮區（區域下拉的 option）。ZipcodeId 即 Believers/Signups 的 MailZipcodeID/TextZipcodeID FK。</summary>
public sealed record ZipcodeAreaItem(int ZipcodeId, string City, string Area, string Zipcode);

/// <summary>
/// 某縣市的鄉鎮區清單回傳。對齊舊 dlMailCity_SelectedIndexChanged（Where City == X, OrderBy Zipcode）。
/// </summary>
public sealed record ZipcodeAreasResponse(IReadOnlyList<ZipcodeAreaItem> Items, int Total);

/// <summary>Repository 回傳的 flat row。</summary>
public sealed record ZipcodeRow(int ZipcodeId, string City, string Area, string Zipcode);
