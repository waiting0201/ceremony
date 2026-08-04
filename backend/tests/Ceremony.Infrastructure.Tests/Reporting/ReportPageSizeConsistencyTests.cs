using Ceremony.Domain.Reports;
using Ceremony.Infrastructure.Reporting;
using FluentAssertions;

namespace Ceremony.Infrastructure.Tests.Reporting;

/// <summary>
/// 鎖住「Domain 的紙張尺寸表」與「各 renderer 實際畫出來的頁面尺寸」完全一致。
/// </summary>
/// <remarks>
/// 為什麼需要這個測試：ReportPageSizes 會經由 X-Report-Page-Size header 決定 Electron 送印時的 pageSize。
/// 若有人改了 renderer 的 PageWidthCm 卻忘了改表（薦牌 25.4→25.5 就發生過一次），失敗模式是
/// 「PDF 內容正確、實印縮放跑掉」——不會丟例外、不會有紅字，只有印出來量尺寸才看得出來。
/// 這個測試把那個靜默失敗變成 build 失敗。
///
/// 註：各 renderer 的 page.Size(...) 一律改用這兩個 const，所以比對 const 等同比對實際頁面尺寸。
/// </remarks>
public sealed class ReportPageSizeConsistencyTests
{
    public static TheoryData<string, double, double> RendererPageSizes => new()
    {
        { "datacard", DataCardRenderer.PageWidthCm, DataCardRenderer.PageHeightCm },
        { "receipt", ReceiptRenderer.PageWidthCm, ReceiptRenderer.PageHeightCm },
        { "tablet", TabletRenderer.PageWidthCm, TabletRenderer.PageHeightCm },
        { "text", TextRenderer.PageWidthCm, TextRenderer.PageHeightCm },
        { "worship", WorshipRenderer.PageWidthCm, WorshipRenderer.PageHeightCm },
        { "worshipcard", WorshipCardRenderer.PageWidthCm, WorshipCardRenderer.PageHeightCm },
    };

    [Theory]
    [MemberData(nameof(RendererPageSizes))]
    public void Domain_table_matches_renderer_constants(string reportType, double widthCm, double heightCm)
    {
        ReportPageSizes.TryGet(reportType, out var size).Should().BeTrue($"{reportType} 必須在 ReportPageSizes 表內");
        size.WidthCm.Should().Be(widthCm);
        size.HeightCm.Should().Be(heightCm);
    }

    [Fact]
    public void Table_covers_exactly_the_six_report_types()
    {
        ReportPageSizes.All.Keys.Should().BeEquivalentTo(
            "datacard", "receipt", "tablet", "text", "worship", "worshipcard");
    }

    [Fact]
    public void Every_report_type_has_a_driver_form_name()
    {
        // 表單名是 Ceremony.PrintForm 拿去比對驅動紙張清單的 key（見 PrinterFormMatcher）。
        // 新增第 7 種報表卻忘了給名字，失敗模式會是「現場印在 A4 上」——這裡把它變成 build 失敗。
        ReportPageSizes.All.Values.Select(v => v.FormName).Should()
            .OnlyContain(n => !string.IsNullOrWhiteSpace(n)).And.OnlyHaveUniqueItems();
    }

    [Theory]
    [InlineData("datacard", "210000x148000")]
    [InlineData("tablet", "115000x255000")]
    [InlineData("text", "365000x262000")]
    public void Header_value_is_microns(string reportType, string expected)
    {
        ReportPageSizes.TryGet(reportType, out var size).Should().BeTrue();
        size.ToHeaderValue().Should().Be(expected);
    }

    [Fact]
    public void TryGet_is_case_insensitive_and_null_safe()
    {
        ReportPageSizes.TryGet("DataCard", out _).Should().BeTrue();
        ReportPageSizes.TryGet(null, out _).Should().BeFalse();
        ReportPageSizes.TryGet("nope", out _).Should().BeFalse();
    }
}
