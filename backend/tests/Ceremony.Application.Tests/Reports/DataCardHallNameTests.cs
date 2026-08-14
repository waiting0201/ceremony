using Ceremony.Application.Reports;
using Ceremony.Application.Signups;
using FluentAssertions;

namespace Ceremony.Application.Tests.Reports;

/// <summary>
/// 鎖住「資料卡帶得到堂號」——2026-08-14 客訴「資料卡列印 右邊的列印沒有印到堂號」的回歸鎖。
/// </summary>
/// <remarks>
/// 根因不在座標而在管線：2026-07-03 改版把 HallName 從 <c>DataCardModel</c>／<c>DataCardData</c>／
/// <c>ReportModelBuilders.DataCard</c> **整條移除**，builder 根本沒碰 <c>s.HallName</c>。這裡鎖住
/// builder 必須套用與薦牌／文牒同一支 <c>SignupReportContext.SplitHallName</c> 的拆分規則。
/// 版面（窗框內「故」字左右）由 <c>Ceremony.Infrastructure.Tests</c> 的
/// <c>DataCard_HallName_SitsBesideGuGlyphAndStaysAboveDeadNames</c> 負責。
/// </remarks>
public sealed class DataCardHallNameTests
{
    private static SignupListItem Make(string? hallName) => new(
        Id: Guid.NewGuid(),
        Year: 115,
        CeremonyCategoryId: Guid.NewGuid(),
        CeremonyTitle: "春季",
        SignupType: 1,
        NumberTitle: "No",
        Number: 123,
        Fee: 600,
        Employee: null,
        BelieverId: null,
        Name: "黃耀章",
        HallName: hallName,
        Phone: "0912345678",
        IsFixedNumber: false,
        LivingNames: ["子甲", null, null, null, null, null],
        DeadNames: ["陳大明", null, null, null, null, null],
        MailCity: "台北市", MailZone: "信義區", MailZipcode: "110", MailAddress: "市府路 1 號",
        TextCity: "高雄市", TextZone: "左營區", TextZipcode: "813", TextAddress: "文牒路 9 號",
        PrepayYear: null, PrepayCeremonyCategoryId: null, PrepayCeremonyTitle: null,
        Remark: null, AdminName: "Administrator", CreateDate: DateTime.UtcNow);

    [Theory]
    [InlineData("潁川", "潁", "川")]        // 2 字 → 1+1
    [InlineData("太原王氏", "太原", "王氏")] // 4 字 → 2+2
    [InlineData("隴西李", "隴西李", "")]     // 3 字 → 整串進 First
    [InlineData("", "", "")]
    [InlineData(null, "", "")]
    public void DataCard_splits_hall_name_like_tablet(string? hallName, string first, string second)
    {
        var m = ReportModelBuilders.DataCard(Make(hallName));
        m.HallNameFirst.Should().Be(first);
        m.HallNameSecond.Should().Be(second);
    }

    [Fact]
    public void DataCard_and_tablet_split_hall_name_identically()
    {
        var s = Make("太原王氏");
        var card = ReportModelBuilders.DataCard(s);
        var tablet = ReportModelBuilders.Tablet(s);
        card.HallNameFirst.Should().Be(tablet.HallNameFirst);
        card.HallNameSecond.Should().Be(tablet.HallNameSecond);
    }
}
