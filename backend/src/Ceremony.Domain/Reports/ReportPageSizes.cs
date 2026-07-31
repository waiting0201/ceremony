namespace Ceremony.Domain.Reports;

/// <summary>
/// 每種報表的實體紙張尺寸 —— 列印通道的 single source of truth。
/// </summary>
/// <remarks>
/// 為什麼要有這張表：尺寸原本只散在各 renderer 的 const 裡，而 API / Electron 主行程送印時需要同一組值
/// 才能指定 pageSize。若兩邊各寫一份，不同步的失敗模式是「PDF 正確但實印縮放跑掉」——靜默印歪、不會報錯。
/// 因此 (1) 這裡是唯一權威 (2) Ceremony.Infrastructure.Tests 的 ReportPageSizeConsistencyTests 斷言
/// 本表與每個 renderer 的 PageWidthCm/PageHeightCm 完全相同 (3) API 用 X-Report-Page-Size header 帶給前端。
///
/// 尺寸不可隨意更動：座標系以此為基準，見 docs/blueprints/printing-reports-positions.md。
/// 列印通道契約見 docs/blueprints/print-channel-electron.md。
/// </remarks>
public static class ReportPageSizes
{
    /// <summary>報表紙張尺寸。cm 為權威值，微米為送印用（Electron pageSize 的單位）。</summary>
    public readonly record struct PageSize(double WidthCm, double HeightCm)
    {
        // 1cm = 10000µm。各報表 cm 值最多 1 位小數 → 換微米必為整數，無捨入誤差。
        public int WidthMicrons => (int)Math.Round(WidthCm * 10_000);

        public int HeightMicrons => (int)Math.Round(HeightCm * 10_000);

        /// <summary>X-Report-Page-Size 的值，例：資料卡為 "210000x148000"。</summary>
        public string ToHeaderValue() => $"{WidthMicrons}x{HeightMicrons}";
    }

    private static readonly Dictionary<string, PageSize> Sizes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["datacard"] = new(21.0, 14.8),     // A5 橫
        ["receipt"] = new(21.0, 29.7),      // A4 直（雙聯，2 頁）
        ["tablet"] = new(11.5, 25.5),       // 薦牌窄長；25.5 為 2026-07-05 使用者實測值（RDLC 原 25.4）
        ["text"] = new(36.5, 26.2),         // 文牒超寬
        ["worship"] = new(21.0, 29.6),      // 普桌（比 A4 少 0.1cm，沿用 RDLC 值）
        ["worshipcard"] = new(21.0, 14.8),  // 普桌資料卡，A5 橫
    };

    /// <summary>report type（datacard / receipt / …）→ 紙張尺寸。key 比對忽略大小寫。</summary>
    public static IReadOnlyDictionary<string, PageSize> All => Sizes;

    public static bool TryGet(string? reportType, out PageSize size)
    {
        if (reportType is not null) return Sizes.TryGetValue(reportType, out size);
        size = default;
        return false;
    }
}
