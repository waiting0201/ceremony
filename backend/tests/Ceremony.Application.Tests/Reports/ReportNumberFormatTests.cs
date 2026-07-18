using Ceremony.Application.Reports;
using Ceremony.Application.Signups;
using FluentAssertions;

namespace Ceremony.Application.Tests.Reports;

/// <summary>
/// 鎖住各報表「編號欄」字串組法 — 對齊舊 SignupForm btnPrint_Click 路徑（SignupForm.cs:488-637）。
/// 各報表格式不同；新版先前一律用 "{title}-{num}" 連字號是錯的（含漏掉 SignupType==2 特例 + receipt 不該帶 title）。
/// </summary>
public sealed class ReportNumberFormatTests
{
    private static SignupListItem Make(
        int number, int signupType, string numberTitle,
        int fee = 600, int? prepayYear = null, string? prepayTitle = null) => new(
        Id: Guid.NewGuid(),
        Year: 115,
        CeremonyCategoryId: Guid.NewGuid(),
        CeremonyTitle: "春季",
        SignupType: signupType,
        NumberTitle: numberTitle,
        Number: number,
        Fee: fee,
        Employee: null,
        BelieverId: null,
        Name: "黃耀章",
        HallName: "甲",
        Phone: "0912345678",
        IsFixedNumber: false,
        LivingNames: ["子甲", null, null, null, null, null],
        DeadNames: ["陳大明", null, null, null, null, null],
        MailCity: "台北市", MailZone: "信義區", MailZipcode: "110", MailAddress: "市府路 1 號",
        TextCity: "高雄市", TextZone: "左營區", TextZipcode: "813", TextAddress: "文牒路 9 號",
        PrepayYear: prepayYear, PrepayCeremonyCategoryId: null, PrepayCeremonyTitle: prepayTitle,
        Remark: null, AdminName: "Administrator", CreateDate: DateTime.UtcNow);

    [Fact]
    public void DataCard_uses_title_dot_number()  // SignupForm.cs:488
        => ReportModelBuilders.DataCard(Make(123, 1, "No")).Number.Should().Be("No.123");

    [Fact]
    public void DataCard_applies_avoid_four()
        => ReportModelBuilders.DataCard(Make(14, 1, "No")).Number.Should().Be("No.13-1");

    [Fact]
    public void Receipt_uses_number_only_no_title()  // SignupForm.cs:523
        => ReportModelBuilders.Receipt(Make(123, 1, "No"), DateTime.Now).Number.Should().Be("123");

    [Fact]
    public void Receipt_applies_avoid_four()
        => ReportModelBuilders.Receipt(Make(24, 1, "No"), DateTime.Now).Number.Should().Be("23-1");

    [Fact]
    public void Receipt_prints_roc_year_month_day()  // SignupForm.cs:524 taiwanCalendar.GetYear
    {
        var m = ReportModelBuilders.Receipt(Make(1, 1, "No"), new DateTime(2026, 7, 18));
        m.Year.Should().Be("115");
        m.Month.Should().Be("7");
        m.Day.Should().Be("18");
    }

    [Fact]
    public void Receipt_fills_mailing_cover_fields()  // SignupForm.cs:520-521 封面頁用郵寄地址（非文牒地址）
    {
        var m = ReportModelBuilders.Receipt(Make(1, 1, "No"), DateTime.Now);
        m.Zipcode.Should().Be("110");
        m.Address.Should().Be("台北市信義區市府路 1 號");
    }

    [Fact]
    public void Receipt_formats_fee_with_thousand_separator()  // SignupForm.cs:522 ToString("N0")
        => ReportModelBuilders.Receipt(Make(1, 1, "No", fee: 1200), DateTime.Now).Fee.Should().Be("1,200");

    [Fact]
    public void Receipt_prepay_uses_legacy_wording()  // SignupForm.cs:527 預繳至X年Y
        => ReportModelBuilders.Receipt(Make(1, 1, "No", prepayYear: 116, prepayTitle: "春季"), DateTime.Now)
            .Prepay.Should().Be("預繳至116年春季");

    [Fact]
    public void DataCard_prepay_uses_legacy_wording()  // SignupForm.cs:220/489
        => ReportModelBuilders.DataCard(Make(1, 1, "No", prepayYear: 116, prepayTitle: "春季"))
            .Prepay.Should().Be("預繳至116年春季");

    [Fact]
    public void DataCard_uses_text_address()  // SignupForm.cs:233/502 資料卡印文牒地址（非郵寄地址）
        => ReportModelBuilders.DataCard(Make(1, 1, "No")).Address.Should().Be("高雄市左營區文牒路 9 號");

    [Fact]
    public void Text_uses_text_address()  // SignupForm.cs:350-352/608
        => ReportModelBuilders.Text(Make(1, 1, "No")).Address.Should().Be("高雄市左營區文牒路 9 號");

    [Fact]
    public void Tablet_type1_uses_title_plus_number_no_separator()  // SignupForm.cs:559
        => ReportModelBuilders.Tablet(Make(123, 1, "No")).Number.Should().Be("No123");

    [Fact]
    public void Tablet_type2_temple_prints_title_only()  // SignupForm.cs:559 (SignupType==2)
        => ReportModelBuilders.Tablet(Make(123, 2, "寺")).Number.Should().Be("寺");

    [Fact]
    public void Text_type2_temple_prints_title_only()  // SignupForm.cs:607 (SignupType==2)
        => ReportModelBuilders.Text(Make(123, 2, "寺")).Number.Should().Be("寺");

    [Fact]
    public void Text_type1_uses_title_plus_number_no_separator()
        => ReportModelBuilders.Text(Make(7, 1, "No")).Number.Should().Be("No7");

    [Fact]
    public void Worship_uses_title_plus_number_no_separator()  // SignupForm.cs:637
        => ReportModelBuilders.Worship(Make(5, 4, "普")).Number.Should().Be("普5");
}
