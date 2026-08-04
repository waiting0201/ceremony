using Ceremony.Domain.Reports;
using FluentAssertions;

namespace Ceremony.Application.Tests.Domain;

/// <summary>
/// 鎖住「報表 → 驅動自訂表單」的比對行為。
/// </summary>
/// <remarks>
/// 這組測試的核心資產是 <see cref="LegacyForms"/>：它用**舊系統實際寫死的表單尺寸**當 fixture，
/// 斷言 ±0.5mm 的容差恰好把它們切成 docs/design/infrastructure.md runbook 要求的兩組
/// （收據／普桌本來就對、資料卡／薦牌／文牒必須重建）。任何人調整容差都會打破這幾筆，
/// 也就必須先讀懂為什麼是 0.5mm。
/// </remarks>
public sealed class PrinterFormMatcherTests
{
    /// <summary>1/100 吋 → mm。System.Drawing 的 PaperSize.Width/Height 就是這個單位。</summary>
    private static double Mm(int hundredthsOfInch) => hundredthsOfInch * 25.4 / 100.0;

    private static PrinterFormMatcher.DriverForm Form(string name, int w, int h, short kind = 257)
        => new(name, kind, Mm(w), Mm(h));

    // ───────────────────────── 對照表完整性 ─────────────────────────

    [Fact]
    public void Every_report_type_has_a_distinct_non_empty_form_name()
    {
        var names = ReportPageSizes.All.Values.Select(v => v.FormName).ToList();

        names.Should().OnlyContain(n => !string.IsNullOrWhiteSpace(n));
        names.Should().OnlyHaveUniqueItems("表單名是驅動比對的 key，重複會讓兩種報表搶同一張紙");
    }

    [Fact]
    public void Datacard_and_worshipcard_share_a_size_but_not_a_form_name()
    {
        // 尺寸相同（21×14.8）卻是不同的紙——名稱是唯一能分辨的東西。
        ReportPageSizes.TryGet("datacard", out var card).Should().BeTrue();
        ReportPageSizes.TryGet("worshipcard", out var worshipCard).Should().BeTrue();

        card.WidthCm.Should().Be(worshipCard.WidthCm);
        card.HeightCm.Should().Be(worshipCard.HeightCm);
        card.FormName.Should().NotBe(worshipCard.FormName);
    }

    // ───────────────────────── 舊表單分類（容差的行為鎖） ─────────────────────────

    /// <summary>舊系統寫死的 PaperSize（1/100 吋）——見 reference/old/Ceremony/SignupForm.cs。</summary>
    public static TheoryData<string, string, int, int, PrinterFormMatcher.FormMatch> LegacyForms => new()
    {
        // reportType,   表單名,   舊寬,  舊高,  期望判定
        { "datacard", "資料卡", 794, 560, PrinterFormMatcher.FormMatch.SizeMismatch },   // −8.32 / −5.76
        { "receipt", "收據", 827, 1170, PrinterFormMatcher.FormMatch.Exact },            // +0.06 / +0.18
        { "tablet", "薦牌", 453, 1000, PrinterFormMatcher.FormMatch.SizeMismatch },      // +0.06 / −1.00
        { "text", "文牒", 1370, 990, PrinterFormMatcher.FormMatch.SizeMismatch },        // −17.02 / −10.54
        { "worship", "普桌", 827, 1165, PrinterFormMatcher.FormMatch.Exact },            // +0.06 / −0.09
    };

    [Theory]
    [MemberData(nameof(LegacyForms))]
    public void Legacy_forms_are_classified_per_the_runbook(
        string reportType, string formName, int w, int h, PrinterFormMatcher.FormMatch expected)
    {
        var result = PrinterFormMatcher.Match(reportType, [Form(formName, w, h)]);

        result.Match.Should().Be(expected);
        result.FormName.Should().Be(formName);
        result.Form.Should().NotBeNull("同名表單一定要回傳，尺寸不符也照選——拒選只會讓客訴原封不動");
    }

    [Fact]
    public void Legacy_datacard_reports_the_actual_millimetre_gap()
    {
        var result = PrinterFormMatcher.Match("datacard", [Form("資料卡", 794, 560)]);

        // 這兩個數字會直接進診斷紀錄的 formMismatchMm，對得上 runbook 的重建清單。
        result.WidthDiffMm.Should().BeApproximately(-8.32, 0.01);
        result.HeightDiffMm.Should().BeApproximately(-5.76, 0.01);
    }

    // ───────────────────────── 容差邊界 ─────────────────────────

    [Theory]
    [InlineData(0.49, PrinterFormMatcher.FormMatch.Exact)]
    [InlineData(0.50, PrinterFormMatcher.FormMatch.Exact)]
    [InlineData(0.51, PrinterFormMatcher.FormMatch.SizeMismatch)]
    [InlineData(-0.51, PrinterFormMatcher.FormMatch.SizeMismatch)]
    public void Tolerance_boundary_is_inclusive_at_half_a_millimetre(
        double deltaMm, PrinterFormMatcher.FormMatch expected)
    {
        var form = new PrinterFormMatcher.DriverForm("資料卡", 257, 210 + deltaMm, 148);

        PrinterFormMatcher.Match("datacard", [form]).Match.Should().Be(expected);
    }

    [Fact]
    public void Quantisation_noise_of_hundredths_of_an_inch_does_not_trip_the_tolerance()
    {
        // 正確建立的 210×148mm 表單，驅動經 1/100 吋量化後回報 827×583 → 210.06×148.08mm。
        PrinterFormMatcher.Match("datacard", [Form("資料卡", 827, 583)])
            .Match.Should().Be(PrinterFormMatcher.FormMatch.Exact);
    }

    // ───────────────────────── 找不到 ─────────────────────────

    [Fact]
    public void Missing_form_is_not_found()
    {
        var result = PrinterFormMatcher.Match("text", [Form("A4", 827, 1170), Form("資料卡", 827, 583)]);

        result.Match.Should().Be(PrinterFormMatcher.FormMatch.NotFound);
        result.FormName.Should().Be("文牒");
        result.Form.Should().BeNull();
    }

    [Fact]
    public void Same_size_form_with_a_different_name_is_never_substituted()
    {
        // 「資料卡」與「普桌資料卡」尺寸完全相同——靜默代換會讓報表印在別種紙上而沒有任何訊號。
        PrinterFormMatcher.Match("worshipcard", [Form("資料卡", 827, 583)])
            .Match.Should().Be(PrinterFormMatcher.FormMatch.NotFound);
    }

    [Fact]
    public void Empty_form_list_is_not_found()
    {
        PrinterFormMatcher.Match("datacard", []).Match.Should().Be(PrinterFormMatcher.FormMatch.NotFound);
    }

    // ───────────────────────── 其他 ─────────────────────────

    [Fact]
    public void Duplicate_names_take_the_first_one()
    {
        // 逐行對齊舊系統的 foreach + break。
        var result = PrinterFormMatcher.Match(
            "datacard",
            [Form("資料卡", 827, 583, kind: 301), Form("資料卡", 794, 560, kind: 302)]);

        result.Form!.Value.Kind.Should().Be(301);
        result.Match.Should().Be(PrinterFormMatcher.FormMatch.Exact);
    }

    [Fact]
    public void Form_name_matching_is_case_and_whitespace_sensitive()
    {
        // 驅動表單名是要送進 DEVMODE 的識別字串，不做正規化；runbook 因此要求名稱完全一致。
        PrinterFormMatcher.Match("datacard", [Form(" 資料卡", 827, 583)])
            .Match.Should().Be(PrinterFormMatcher.FormMatch.NotFound);
    }

    [Theory]
    [InlineData("nope")]
    [InlineData(null)]
    public void Unknown_report_type_throws(string? reportType)
    {
        var act = () => PrinterFormMatcher.Match(reportType, []);

        act.Should().Throw<ArgumentException>();
    }
}
