using Ceremony.Domain.Reports;
using FluentAssertions;

namespace Ceremony.Application.Tests.Domain;

/// <summary>
/// 鎖住「什麼情況才准去動每使用者預設 DEVMODE」。
/// </summary>
/// <remarks>
/// 這組測試存在的理由跟 <c>DevModePaperFieldsTests</c> 一樣：真正寫 DEVMODE 的
/// Ceremony.PrintForm 是 net10.0-windows 的 exe，macOS 開發機與 CI 都跑不到，
/// 邏輯留在那裡等於沒有測試（2026-08-05 客訴 0x80010105 的成因）。
///
/// 核心不變式只有一條，也是 2026-08-06「縮小爆炸半徑」的全部內容：
/// <b>只有 Exact + 實體印表機會寫入，其餘一律不碰。</b>
/// </remarks>
public sealed class PrinterFormPolicyTests
{
    [Fact]
    public void Exact_match_on_a_physical_printer_is_the_only_case_that_writes()
    {
        var all = Enum.GetValues<PrinterFormMatcher.FormMatch>()
            .SelectMany(m => new[] { (Match: m, Virtual: true), (Match: m, Virtual: false) })
            .Where(c => PrinterFormPolicy.Decide(c.Match, c.Virtual) == PrinterFormPolicy.FormApplyDecision.Apply)
            .ToList();

        all.Should().ContainSingle("動全域共用狀態的入口只能有一個")
            .Which.Should().Be((PrinterFormMatcher.FormMatch.Exact, false));
    }

    [Fact]
    public void Size_mismatch_no_longer_writes()
    {
        // 2026-08-06 推翻 2026-08-04：尺寸不符改成不寫，使用者在原生對話框自己選紙。
        // 不寫 ≠ 停在 A4，而是停在他目前的預設紙，且檢視器標題會請他選——最壞是多按幾下。
        PrinterFormPolicy.Decide(PrinterFormMatcher.FormMatch.SizeMismatch, isVirtualPrinter: false)
            .Should().Be(PrinterFormPolicy.FormApplyDecision.SkipSizeMismatch);
    }

    [Fact]
    public void Virtual_printer_wins_over_every_match_result()
    {
        // 就算 Print to PDF 上剛好有同名且尺寸相符的表單也不碰：預選拿不到好處，
        // 卻會改掉使用者的 PDF 輸出設定。
        foreach (var match in Enum.GetValues<PrinterFormMatcher.FormMatch>())
        {
            PrinterFormPolicy.Decide(match, isVirtualPrinter: true)
                .Should().Be(PrinterFormPolicy.FormApplyDecision.SkipVirtualPrinter,
                    "虛擬印表機的判斷優先於比對結果（match={0}）", match);
        }
    }

    [Fact]
    public void Not_found_skips()
    {
        PrinterFormPolicy.Decide(PrinterFormMatcher.FormMatch.NotFound, isVirtualPrinter: false)
            .Should().Be(PrinterFormPolicy.FormApplyDecision.SkipNotFound);
    }

    // ───────────────────────── 與 Electron 端的契約 ─────────────────────────

    [Theory]
    [InlineData(PrinterFormPolicy.FormApplyDecision.SkipVirtualPrinter, "skipped-virtual")]
    [InlineData(PrinterFormPolicy.FormApplyDecision.SkipNotFound, "not-found")]
    [InlineData(PrinterFormPolicy.FormApplyDecision.SkipSizeMismatch, "mismatch")]
    public void Skip_decisions_map_to_the_result_strings_electron_knows(
        PrinterFormPolicy.FormApplyDecision decision, string expected)
    {
        // 這些字串是跨語言契約：electron/print-form-core.ts 的 HELPER_RESULTS 少一個，
        // parseHelperOutput 就會把整包退成 helper-error。
        PrinterFormPolicy.ToResult(decision).Should().Be(expected);
    }

    [Fact]
    public void Apply_has_no_result_string_of_its_own()
    {
        // 寫入之後還可能被驅動拒絕，result 要由實際寫入結果決定，不能在這裡先講死。
        var act = () => PrinterFormPolicy.ToResult(PrinterFormPolicy.FormApplyDecision.Apply);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Every_skip_decision_has_a_result_string()
    {
        var skips = Enum.GetValues<PrinterFormPolicy.FormApplyDecision>()
            .Where(d => d != PrinterFormPolicy.FormApplyDecision.Apply);

        foreach (var skip in skips)
        {
            var act = () => PrinterFormPolicy.ToResult(skip);
            act.Should().NotThrow($"新增 skip 理由時必須同時給 electron 端一個看得懂的 result（{skip}）");
        }
    }
}
