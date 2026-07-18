using Ceremony.Domain.Services;

namespace Ceremony.Application.Reports;

/// <summary>
/// 抽象化 PDF 渲染（讓 Application 不耦合到 QuestPDF/RDLC 等實作）。
/// </summary>
public interface IReportRenderer
{
    byte[] RenderDataCard(DataCardModel model, bool debugOverlay = false);
    byte[] RenderReceipt(ReceiptModel model);
    byte[] RenderTablet(TabletModel model, bool debugOverlay = false);
    byte[] RenderText(TextModel model, bool debugOverlay = false);
    byte[] RenderWorship(WorshipModel model);
    byte[] RenderWorshipCard(WorshipCardModel model, bool debugOverlay = false);
}

public sealed record DataCardModel(
    string Number,
    string? Prepay,
    string?[] DeadNames,
    string?[] LivingNames,
    string? Address,
    string? Phone,
    string? Remark);

public sealed record ReceiptModel(
    string Name,
    string Zipcode,
    string Address,
    string Fee,
    string Number,
    string Prepay,
    string Year,
    string Month,
    string Day);

public sealed record TabletModel(
    string Number,
    string? HallNameFirst,
    string? HallNameSecond,
    string?[] DeadNames,
    string?[] LivingNames,
    double ParaFontSizeCm,
    TabletTemplate Template);

public sealed record TextModel(
    string Number,
    string? HallNameFirst,
    string? HallNameSecond,
    string?[] DeadNames,
    string?[] LivingNames,
    string? Address,
    TextTemplate Template);

public sealed record WorshipModel(
    string Number,
    string?[] LivingNames,
    WorshipTemplate Template);

public sealed record WorshipCardModel(
    string Number,
    string?[] LivingNames,
    WorshipTemplate Template,
    string? Phone,
    string? Remark);
