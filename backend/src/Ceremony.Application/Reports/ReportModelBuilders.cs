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
}
