using Ceremony.Application.Signups;
using Ceremony.Domain.Services;

namespace Ceremony.Application.Reports;

/// <summary>
/// 從 <see cref="SignupListItem"/> 組各報表 model。
/// 共用於單筆列印 handlers 與批次 <c>BatchReportHandler</c>。
/// </summary>
internal static class ReportModelBuilders
{
    public static DataCardModel DataCard(SignupListItem s)
    {
        var (livingNames, deadNames) = SignupReportContext.Extract(s);
        var prepay = s.PrepayYear.HasValue
            ? $"預繳 {s.PrepayYear} {s.PrepayCeremonyTitle ?? string.Empty}"
            : string.Empty;
        return new DataCardModel(
            Number: SignupReportContext.DataCardNumber(s),
            Prepay: prepay,
            DeadNames: deadNames,
            LivingNames: livingNames,
            Address: SignupReportContext.AddressOf(s),
            Phone: s.Phone,
            Remark: s.Remark);
    }

    public static ReceiptModel Receipt(SignupListItem s, DateTime now)
    {
        return new ReceiptModel(
            Name: s.Name ?? string.Empty,
            Fee: s.Fee?.ToString() ?? string.Empty,
            Number: SignupReportContext.ReceiptNumber(s),
            Prepay: s.PrepayYear.HasValue ? $"預繳 {s.PrepayYear} {s.PrepayCeremonyTitle}" : string.Empty,
            Year: now.Year.ToString(),
            Month: now.Month.ToString(),
            Day: now.Day.ToString());
    }

    public static TabletModel Tablet(SignupListItem s)
    {
        var (livingNames, deadNames) = SignupReportContext.Extract(s);
        var (template, paraFontSize) = PrintTemplateSelector.ChooseTablet(deadNames, livingNames);
        var paraSizeCm = double.Parse(paraFontSize.Replace("cm", ""));
        var (hallFirst, hallSecond) = SignupReportContext.SplitHallName(s.HallName);
        return new TabletModel(
            Number: SignupReportContext.TabletTextNumber(s),
            HallNameFirst: hallFirst,
            HallNameSecond: hallSecond,
            DeadNames: deadNames,
            LivingNames: livingNames,
            ParaFontSizeCm: paraSizeCm,
            Template: template);
    }

    public static TextModel Text(SignupListItem s)
    {
        var (livingNames, deadNames) = SignupReportContext.Extract(s);
        var template = PrintTemplateSelector.ChooseText(deadNames);
        var (hallFirst, hallSecond) = SignupReportContext.SplitHallName(s.HallName);
        return new TextModel(
            Number: SignupReportContext.TabletTextNumber(s),
            HallNameFirst: hallFirst,
            HallNameSecond: hallSecond,
            DeadNames: deadNames,
            LivingNames: livingNames,
            Address: SignupReportContext.AddressOf(s),
            Template: template);
    }

    public static WorshipModel Worship(SignupListItem s)
    {
        var (livingNames, _) = SignupReportContext.Extract(s);
        var template = PrintTemplateSelector.ChooseWorship(livingNames);
        return new WorshipModel(
            Number: SignupReportContext.WorshipNumber(s),
            LivingNames: livingNames,
            Template: template);
    }

    public static WorshipCardModel WorshipCard(SignupListItem s)
    {
        var (livingNames, _) = SignupReportContext.Extract(s);
        var template = PrintTemplateSelector.ChooseWorship(livingNames);
        return new WorshipCardModel(
            Number: SignupReportContext.WorshipNumber(s),
            LivingNames: livingNames,
            Template: template,
            Phone: s.Phone,
            Remark: s.Remark);
    }

    /// <summary>
    /// 開發用固定測試資料：5 位亡者 + 5 位陽上（3+ 亡 3+ 陽 → 落在 TabletTemplate.Base fallback，
    /// 也就是最擁擠的 2×3 矩陣排版），供 <c>GenerateTabletSampleHandler</c> 搭配 debugOverlay 樣板疊圖
    /// 做列印位置檢視，不需要在 DB 建對應的報名資料。
    /// </summary>
    public static TabletModel TabletSample()
    {
        var deadNames = new string?[] { "亡者一", "亡者二", "亡者三", "亡者四", "亡者五", null };
        var livingNames = new string?[] { "陽上一", "陽上二", "陽上三", "陽上四", "陽上五", null };
        var (template, paraFontSize) = PrintTemplateSelector.ChooseTablet(deadNames, livingNames);
        var paraSizeCm = double.Parse(paraFontSize.Replace("cm", ""));
        return new TabletModel(
            Number: "測1",
            HallNameFirst: "測",
            HallNameSecond: "試",
            DeadNames: deadNames,
            LivingNames: livingNames,
            ParaFontSizeCm: paraSizeCm,
            Template: template);
    }
}
