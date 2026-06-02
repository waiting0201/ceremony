using Ceremony.Application.Zipcodes;
using FluentAssertions;
using Moq;

namespace Ceremony.Application.Tests.Zipcodes;

/// <summary>
/// Zipcodes 城市 / 區域查詢 handler 單元測試。
/// 對齊舊 NewSignupForm.LoadCity / dlMailCity_SelectedIndexChanged。
/// </summary>
public sealed class ZipcodeHandlersTests
{
    private readonly Mock<IZipcodeRepository> _repo = new();

    [Fact]
    public async Task Cities_returns_items_and_total()
    {
        _repo.Setup(r => r.GetCitiesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(["台中市", "台北市"]);

        var result = await new ListZipcodeCitiesHandler(_repo.Object).HandleAsync();

        result.Items.Should().Equal("台中市", "台北市");
        result.Total.Should().Be(2);
    }

    [Fact]
    public async Task Cities_empty_returns_total_0()
    {
        _repo.Setup(r => r.GetCitiesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var result = await new ListZipcodeCitiesHandler(_repo.Object).HandleAsync();

        result.Items.Should().BeEmpty();
        result.Total.Should().Be(0);
    }

    [Fact]
    public async Task Areas_maps_rows_to_items_and_passes_city_to_repo()
    {
        _repo.Setup(r => r.GetByCityAsync("台北市", It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new ZipcodeRow(100, "台北市", "中正區", "100"),
                new ZipcodeRow(103, "台北市", "大同區", "103"),
            ]);

        var result = await new ListZipcodeAreasHandler(_repo.Object).HandleAsync("台北市");

        result.Total.Should().Be(2);
        result.Items[0].Should().BeEquivalentTo(new ZipcodeAreaItem(100, "台北市", "中正區", "100"));
        result.Items[1].Area.Should().Be("大同區");
        _repo.Verify(r => r.GetByCityAsync("台北市", It.IsAny<CancellationToken>()), Times.Once);
    }
}
