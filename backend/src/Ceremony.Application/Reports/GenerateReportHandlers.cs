using Ceremony.Application.Signups;
using Ceremony.Domain.Exceptions;
using Ceremony.Domain.Services;

namespace Ceremony.Application.Reports;

/// <summary>
/// 共用 helper：從 SignupListItem 提取常見欄位（編號避4、堂號拆分、寄件地址 join）。
/// </summary>
internal static class SignupReportContext
{
    public static (string?[] LivingNames, string?[] DeadNames) Extract(SignupListItem s)
        => (s.LivingNames, s.DeadNames);

    /// <summary>編號的避4顯示文字（不含 NumberTitle）。對齊舊 GetNumberText。</summary>
    public static string NumberText(SignupListItem s)
        => s.Number.HasValue ? AvoidFourFormatter.Format(s.Number.Value) : string.Empty;

    // 各報表「編號欄」字串組法不同 — 對齊舊 SignupForm btnPrint_Click 路徑（SignupForm.cs:488-637），
    // 該路徑同時供單筆與批次列印。新版先前一律用 "{title}-{num}" 連字號是錯的。

    /// <summary>資料卡：NumberTitle + "." + 號（SignupForm.cs:488）。</summary>
    public static string DataCardNumber(SignupListItem s)
        => $"{s.NumberTitle}.{NumberText(s)}";

    /// <summary>收據：只印號碼、無 NumberTitle（SignupForm.cs:523 / :262）。</summary>
    public static string ReceiptNumber(SignupListItem s)
        => NumberText(s);

    /// <summary>薦牌 / 文牒：SignupType==2（寺方）只印 NumberTitle，否則 NumberTitle+號（SignupForm.cs:559/607）。</summary>
    public static string TabletTextNumber(SignupListItem s)
        => s.SignupType == 2 ? (s.NumberTitle ?? string.Empty) : $"{s.NumberTitle}{NumberText(s)}";

    /// <summary>普桌：NumberTitle + 號，無分隔（SignupForm.cs:637）。</summary>
    public static string WorshipNumber(SignupListItem s)
        => $"{s.NumberTitle}{NumberText(s)}";

    public static (string First, string Second) SplitHallName(string? hallName)
    {
        // 對齊舊系統 SignupForm 堂號拆分：2 字 1+1、4 字 2+2、其他保留
        var name = (hallName ?? string.Empty).Trim();
        return name.Length switch
        {
            2 => (name[..1], name[1..]),
            4 => (name[..2], name[2..]),
            _ => (name, string.Empty),
        };
    }

    /// <summary>郵寄地址 — 僅收據封面用（SignupForm.cs:520-521）。</summary>
    public static string AddressOf(SignupListItem s)
        => string.Concat(s.MailCity ?? string.Empty, s.MailZone ?? string.Empty, s.MailAddress ?? string.Empty);

    /// <summary>文牒地址 — 資料卡與文牒用（SignupForm.cs:233/350-352/502/608 兩報表皆取 Text*）。</summary>
    public static string TextAddressOf(SignupListItem s)
        => string.Concat(s.TextCity ?? string.Empty, s.TextZone ?? string.Empty, s.TextAddress ?? string.Empty);
}

/// <summary>
/// 收據 PDF。
/// </summary>
/// <remarks>
/// Legacy: SignupForm.cs:1052-1146 (PrintReceipt) + tmpReceipt.rdlc
/// </remarks>
public sealed class GenerateReceiptHandler(ISignupRepository repo, IReportRenderer renderer)
{
    public async Task<(byte[] Pdf, string FileName)> HandleAsync(Guid signupId, CancellationToken ct = default)
    {
        var s = await repo.GetByIdAsync(signupId, ct)
            ?? throw new DomainException("SIGNUP_NOT_FOUND", "找不到報名");

        return (renderer.RenderReceipt(ReportModelBuilders.Receipt(s, DateTime.Now)),
                $"receipt-{s.Year}-{s.NumberTitle}-{s.Number}.pdf");
    }
}

/// <summary>
/// 薦牌 PDF（含 9 變體選擇 + ParaFontSize 動態）。
/// </summary>
/// <remarks>
/// Legacy: SignupForm.cs:1148-1333 (PrintTablet) + tmpTablet*.rdlc 9 變體
/// </remarks>
public sealed class GenerateTabletHandler(ISignupRepository repo, IReportRenderer renderer)
{
    public async Task<(byte[] Pdf, string FileName)> HandleAsync(Guid signupId, bool debugOverlay = false, CancellationToken ct = default)
    {
        var s = await repo.GetByIdAsync(signupId, ct)
            ?? throw new DomainException("SIGNUP_NOT_FOUND", "找不到報名");

        return (renderer.RenderTablet(ReportModelBuilders.Tablet(s), debugOverlay),
                $"tablet-{s.Year}-{s.NumberTitle}-{s.Number}.pdf");
    }
}

/// <summary>
/// 開發用：薦牌「5 位亡者 + 5 位陽上」固定樣本 PDF（Base 變體，最擁擠的 2×3 矩陣排版）。
/// 不依賴 DB 資料，供開發人員搭配 debugOverlay 樣板疊圖直接檢視列印位置。
/// </summary>
/// <remarks>
/// Blueprint: docs/blueprints/printing-reports.md「開發用列印位置檢視工具」
/// </remarks>
public sealed class GenerateTabletSampleHandler(IReportRenderer renderer)
{
    public byte[] Handle(bool debugOverlay = false)
        => renderer.RenderTablet(ReportModelBuilders.TabletSample(), debugOverlay);
}

/// <summary>
/// 文牒 PDF（2 變體）。
/// </summary>
/// <remarks>
/// Legacy: SignupForm.cs:1335-1552 (PrintText) + tmpText.rdlc / tmpTextTwo.rdlc
/// </remarks>
public sealed class GenerateTextHandler(ISignupRepository repo, IReportRenderer renderer)
{
    public async Task<(byte[] Pdf, string FileName)> HandleAsync(Guid signupId, bool debugOverlay = false, CancellationToken ct = default)
    {
        var s = await repo.GetByIdAsync(signupId, ct)
            ?? throw new DomainException("SIGNUP_NOT_FOUND", "找不到報名");

        return (renderer.RenderText(ReportModelBuilders.Text(s), debugOverlay),
                $"text-{s.Year}-{s.NumberTitle}-{s.Number}.pdf");
    }
}

/// <summary>
/// 普桌 PDF（6 變體；不限 SignupType — 對齊舊系統選什麼印什麼）。
/// </summary>
/// <remarks>
/// Legacy: SignupForm.cs:1554-1696 (PrintWorship) + tmpWorship*.rdlc 6 變體（無型別檢查）
/// </remarks>
public sealed class GenerateWorshipHandler(ISignupRepository repo, IReportRenderer renderer)
{
    public async Task<(byte[] Pdf, string FileName)> HandleAsync(Guid signupId, CancellationToken ct = default)
    {
        var s = await repo.GetByIdAsync(signupId, ct)
            ?? throw new DomainException("SIGNUP_NOT_FOUND", "找不到報名");

        return (renderer.RenderWorship(ReportModelBuilders.Worship(s)),
                $"worship-{s.Year}-{s.NumberTitle}-{s.Number}.pdf");
    }
}

/// <summary>
/// 普桌資料卡 PDF（預印卡紙套印；葫蘆內 6 變體同普桌；不限 SignupType，與普桌一致）。
/// </summary>
/// <remarks>
/// 全新報表（舊系統無對應）。Blueprint: docs/blueprints/printing-reports.md「普桌資料卡」
/// </remarks>
public sealed class GenerateWorshipCardHandler(ISignupRepository repo, IReportRenderer renderer)
{
    public async Task<(byte[] Pdf, string FileName)> HandleAsync(Guid signupId, bool debugOverlay = false, CancellationToken ct = default)
    {
        var s = await repo.GetByIdAsync(signupId, ct)
            ?? throw new DomainException("SIGNUP_NOT_FOUND", "找不到報名");

        return (renderer.RenderWorshipCard(ReportModelBuilders.WorshipCard(s), debugOverlay),
                $"worshipcard-{s.Year}-{s.NumberTitle}-{s.Number}.pdf");
    }
}
