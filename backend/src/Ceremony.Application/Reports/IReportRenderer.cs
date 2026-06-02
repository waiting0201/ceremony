using Ceremony.Domain.Services;

namespace Ceremony.Application.Reports;

/// <summary>
/// 抽象化 PDF 渲染（讓 Application 不耦合到 QuestPDF/RDLC 等實作）。
/// </summary>
public interface IReportRenderer
{
    byte[] RenderDataCard(DataCardModel model);
    byte[] RenderReceipt(ReceiptModel model);
    byte[] RenderTablet(TabletModel model);
    byte[] RenderText(TextModel model);
    byte[] RenderWorship(WorshipModel model);
}

public sealed record DataCardModel(
    string Number,
    string? HallName,
    string? Prepay,
    string?[] DeadNames,
    string?[] LivingNames,
    string? Address,
    string? Phone,
    string? Remark);

public sealed record ReceiptModel(
    string Name,
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
