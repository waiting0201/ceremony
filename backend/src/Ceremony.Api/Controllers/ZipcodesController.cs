using Ceremony.Application.Zipcodes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ceremony.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/zipcodes")]
public sealed class ZipcodesController(
    ListZipcodeCitiesHandler cities,
    ListZipcodeAreasHandler areas) : ControllerBase
{
    /// <summary>縣市清單（城市下拉）</summary>
    /// <remarks>
    /// Legacy: NewSignupForm.cs:662-677 (LoadCity)
    /// Blueprint: docs/blueprints/api-endpoints/get-zipcodes.md
    /// </remarks>
    [HttpGet("cities")]
    [ProducesResponseType(typeof(ZipcodeCitiesResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ZipcodeCitiesResponse>> Cities(CancellationToken ct)
        => Ok(await cities.HandleAsync(ct));

    /// <summary>某縣市的鄉鎮區（區域下拉，連動城市）</summary>
    /// <remarks>
    /// Legacy: NewSignupForm.cs:406-424 (dlMailCity_SelectedIndexChanged)
    /// Blueprint: docs/blueprints/api-endpoints/get-zipcodes.md
    /// </remarks>
    [HttpGet]
    [ProducesResponseType(typeof(ZipcodeAreasResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ZipcodeAreasResponse>> Areas(
        [FromQuery] string? city,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(city))
            return Ok(new ZipcodeAreasResponse([], 0));

        return Ok(await areas.HandleAsync(city, ct));
    }
}
