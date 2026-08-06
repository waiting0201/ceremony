using Ceremony.Domain.Services;

namespace Ceremony.Application.Reports;

/// <summary>
/// 抽象化 PDF 渲染（讓 Application 不耦合到 QuestPDF/RDLC 等實作）。
/// </summary>
public interface IReportRenderer
{
    byte[] RenderDataCard(DataCardModel model, bool debugOverlay = false);
    byte[] RenderReceipt(ReceiptModel model);
    /// <param name="debugGrid">
    /// 疊 1cm 刻度格線的「現場對位校正版」。與 <c>debugOverlay</c>（疊樣板照片、僅 Development）不同，
    /// 這個是**現場工具**，生產環境也要能印——樣板量測到的邊界不等於實印能用的邊界，只有把刻度尺
    /// 印在同一張紙上、插進實體牌位座，才量得出真正的可用區。見 docs/blueprints/printing-reports.md。
    /// </param>
    byte[] RenderTablet(TabletModel model, bool debugOverlay = false, bool debugGrid = false);
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
    string? Remark,
    string? NumberTitle = null,      // 編號抬頭（與號碼分開繪製，中間留 0.3cm 空隙；2026-07-21 客訴）
    double ParaFontSizeCm = 0.6);    // 往者字級起點，由 PrintTemplateSelector.ChooseTablet 決定（與薦牌同）

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
