using Ceremony.Domain.Reports;
using FluentAssertions;
using static Ceremony.Domain.Reports.PrintScalePolicy;

namespace Ceremony.Application.Tests.Domain;

/// <summary>
/// <see cref="PrintScalePolicy"/> —— 決策 11 送印路徑唯一的幾何決策點。
/// </summary>
/// <remarks>
/// <para>
/// 這組測試鎖的是 docs/gotchas.md 的首要元教訓：**改列印通道會靜默作廢整份已驗收的座標表**。
/// 現行 ±0.05cm 的實體套印座標是在 Chromium 的 fit-to-printable-area 等比縮放置中下驗收的，
/// <see cref="ScaleMode.Fit"/> 必須逐項複製那個行為。
/// </para>
/// <para>
/// ⚠️ 單元測試**證明不了**「與 Chromium 實際輸出相同」——那只有六報表對照組（同機同紙同資料、
/// 兩條路徑各印一張疊放量測）能證明。這裡鎖的是「規則本身沒被改掉」。
/// </para>
/// </remarks>
public class PrintScalePolicyTests
{
    /// <summary>
    /// 一台 600dpi 雷射機印「資料卡」(21×14.8cm) 的典型度量。
    /// </summary>
    /// <remarks>
    /// 數字取自實務常見值：600dpi、四邊各約 4.2mm 不可列印邊界。
    /// 比照 <c>PrinterFormMatcherTests</c> 用真實舊表單值當 fixture 的作法——
    /// 現場探針（Ceremony.PrintProbe）會帶回 PA2000 的真實 GetDeviceCaps，屆時補一組進來。
    /// </remarks>
    private static DeviceMetrics DataCard600Dpi => new(
        PrintableWidthPx: 4960 - 200,    // 21cm@600dpi ≈ 4960px，左右各扣 100px
        PrintableHeightPx: 3496 - 200,   // 14.8cm@600dpi ≈ 3496px
        PhysicalWidthPx: 4960,
        PhysicalHeightPx: 3496,
        OffsetXPx: 100,
        OffsetYPx: 100,
        DpiX: 600,
        DpiY: 600);

    // ───────────── Fit：複製 Chromium 的行為 ─────────────

    [Fact]
    public void Fit_等比縮放置中於可列印區()
    {
        var pageW = CmToPoints(21.0);
        var pageH = CmToPoints(14.8);
        var d = DataCard600Dpi;

        var r = Compute(pageW, pageH, d, ScaleMode.Fit);

        // 等比：長寬比必須守住（容一個像素的四捨五入）
        var srcRatio = pageW / pageH;
        var dstRatio = (double)r.Width / r.Height;
        dstRatio.Should().BeApproximately(srcRatio, 0.001);

        // 置中：左右留白相等、上下留白相等。
        // 容 1px——餘量是奇數時整數除法必然讓一邊多一個像素，這無法避免也不影響套印。
        (d.PrintableWidthPx - r.Width - r.X).Should().BeCloseTo(r.X, 1);
        (d.PrintableHeightPx - r.Height - r.Y).Should().BeCloseTo(r.Y, 1);
    }

    [Fact]
    public void Fit_永遠不會超出可列印區()
    {
        var d = DataCard600Dpi;

        // 掃各種紙／內容比例，含極端長寬比
        foreach (var (wCm, hCm) in new[] { (21.0, 14.8), (11.5, 25.5), (36.5, 26.2), (1.0, 40.0), (40.0, 1.0) })
        {
            var r = Compute(CmToPoints(wCm), CmToPoints(hCm), d, ScaleMode.Fit);

            r.X.Should().BeGreaterThanOrEqualTo(0);
            r.Y.Should().BeGreaterThanOrEqualTo(0);
            (r.X + r.Width).Should().BeLessThanOrEqualTo(d.PrintableWidthPx);
            (r.Y + r.Height).Should().BeLessThanOrEqualTo(d.PrintableHeightPx);
        }
    }

    [Fact]
    public void Fit_至少有一邊頂到可列印區_否則就不是fit()
    {
        var d = DataCard600Dpi;
        var r = Compute(CmToPoints(21.0), CmToPoints(14.8), d, ScaleMode.Fit);

        // 「填滿到其中一邊頂到」是 fit 的定義。容 2px 給四捨五入。
        var touchesWidth = Math.Abs(r.Width - d.PrintableWidthPx) <= 2;
        var touchesHeight = Math.Abs(r.Height - d.PrintableHeightPx) <= 2;
        (touchesWidth || touchesHeight).Should().BeTrue();
    }

    [Fact]
    public void Fit_紙比內容大時照樣放大_不夾在原尺寸()
    {
        // 刻意不 clamp 到 1.0：fit 的定義是填滿，夾住就變成第三種語意了。
        var d = DataCard600Dpi;
        var r = Compute(CmToPoints(5.0), CmToPoints(3.5), d, ScaleMode.Fit);

        r.Width.Should().BeGreaterThan((int)(5.0 / 2.54 * 600));
    }

    [Fact]
    public void Fit_DPI不是方的也要算對()
    {
        // 點陣／標籤機常見 X≠Y。用 X 的 DPI 去換 Y 會靜默印歪，不會報錯。
        var d = DataCard600Dpi with { DpiX = 300, DpiY = 600, PrintableWidthPx = 2380, PhysicalWidthPx = 2480 };

        var r = Compute(CmToPoints(21.0), CmToPoints(14.8), d, ScaleMode.Fit);

        r.IsEmpty.Should().BeFalse();
        (r.X + r.Width).Should().BeLessThanOrEqualTo(d.PrintableWidthPx);
        (r.Y + r.Height).Should().BeLessThanOrEqualTo(d.PrintableHeightPx);
    }

    [Fact]
    public void Fit_六種報表全部算得出有效矩形()
    {
        var d = DataCard600Dpi;

        foreach (var (type, size) in ReportPageSizes.All)
        {
            var r = Compute(CmToPoints(size.WidthCm), CmToPoints(size.HeightCm), d, ScaleMode.Fit);
            r.IsEmpty.Should().BeFalse($"報表 {type} 必須算得出目標矩形");
        }
    }

    // ───────────── StretchPhysical：舊系統語意，只給排障 ─────────────

    [Fact]
    public void Stretch_覆蓋整張實體紙_原點在負象限()
    {
        var d = DataCard600Dpi;
        var r = Compute(CmToPoints(21.0), CmToPoints(14.8), d, ScaleMode.StretchPhysical);

        // 舊系統 DrawImage(img, PageBounds) 的等價：紙的左上角在 DC 座標的 (-offset, -offset)
        r.Should().Be(new DestRect(-d.OffsetXPx, -d.OffsetYPx, d.PhysicalWidthPx, d.PhysicalHeightPx));
    }

    [Fact]
    public void Stretch_與內容尺寸無關_這就是它非等比的證據()
    {
        var d = DataCard600Dpi;

        var a = Compute(CmToPoints(21.0), CmToPoints(14.8), d, ScaleMode.StretchPhysical);
        var b = Compute(CmToPoints(11.5), CmToPoints(25.5), d, ScaleMode.StretchPhysical);

        b.Should().Be(a);
    }

    [Fact]
    public void 兩種模式必定不同_否則對照組的二分法就失去意義()
    {
        var d = DataCard600Dpi;
        var pageW = CmToPoints(21.0);
        var pageH = CmToPoints(14.8);

        Compute(pageW, pageH, d, ScaleMode.Fit)
            .Should().NotBe(Compute(pageW, pageH, d, ScaleMode.StretchPhysical));
    }

    // ───────────── 防呆 ─────────────

    [Theory]
    [InlineData(0, 100)]
    [InlineData(100, 0)]
    [InlineData(-1, 100)]
    public void 頁面尺寸不合理回空矩形(double wPt, double hPt)
    {
        Compute(wPt, hPt, DataCard600Dpi, ScaleMode.Fit).IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void DPI為零回空矩形而不是除以零()
    {
        var d = DataCard600Dpi with { DpiX = 0 };
        Compute(CmToPoints(21.0), CmToPoints(14.8), d, ScaleMode.Fit).IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void 可列印區為零回空矩形()
    {
        var d = DataCard600Dpi with { PrintableWidthPx = 0 };
        Compute(CmToPoints(21.0), CmToPoints(14.8), d, ScaleMode.Fit).IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void CmToPoints_換算正確()
    {
        CmToPoints(2.54).Should().BeApproximately(72.0, 0.0001);
        CmToPoints(21.0).Should().BeApproximately(595.276, 0.01);   // A4 寬
    }
}
