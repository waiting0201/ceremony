using Ceremony.Application.Reports;

namespace Ceremony.Infrastructure.Reporting;

/// <summary>
/// Application IReportRenderer 介面的 QuestPDF 實作。
/// </summary>
public sealed class QuestPdfReportRenderer(
    DataCardRenderer dataCard,
    ReceiptRenderer receipt,
    TabletRenderer tablet,
    TextRenderer text,
    WorshipRenderer worship,
    WorshipCardRenderer worshipCard) : IReportRenderer
{
    public byte[] RenderDataCard(DataCardModel model, bool debugOverlay = false)
        => dataCard.Render(new DataCardData(
            model.Number, model.Prepay,
            model.DeadNames, model.LivingNames,
            model.Address, model.Phone, model.Remark), debugOverlay);

    public byte[] RenderReceipt(ReceiptModel model)
        => receipt.Render(new ReceiptData(
            model.Name, model.Fee, model.Number, model.Prepay,
            model.Year, model.Month, model.Day));

    public byte[] RenderTablet(TabletModel model, bool debugOverlay = false)
        => tablet.Render(new TabletData(
            model.Number, model.HallNameFirst, model.HallNameSecond,
            model.DeadNames, model.LivingNames,
            model.ParaFontSizeCm, model.Template), debugOverlay: debugOverlay);

    public byte[] RenderText(TextModel model, bool debugOverlay = false)
        => text.Render(new TextData(
            model.Number, model.HallNameFirst, model.HallNameSecond,
            model.DeadNames, model.LivingNames,
            model.Address, model.Template), debugOverlay);

    public byte[] RenderWorship(WorshipModel model)
        => worship.Render(new WorshipData(model.Number, model.LivingNames, model.Template));

    public byte[] RenderWorshipCard(WorshipCardModel model, bool debugOverlay = false)
        => worshipCard.Render(new WorshipCardData(
            model.Number, model.LivingNames, model.Template,
            model.Phone, model.Remark), debugOverlay);
}
