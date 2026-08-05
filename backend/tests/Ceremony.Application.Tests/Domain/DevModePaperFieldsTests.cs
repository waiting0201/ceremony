using Ceremony.Domain.Reports;
using FluentAssertions;

namespace Ceremony.Application.Tests.Domain;

/// <summary>
/// 鎖住 DEVMODE 紙張旗標的一致性規則。
/// </summary>
/// <remarks>
/// 這組測試存在的理由是 2026-08-05 那則客訴（「選了印表機卻跳『您的印表機已發生未預期的設定問題
/// 0x80010105』」）：真正寫入 DEVMODE 的 <c>Ceremony.PrintForm</c> 是 Windows-only exe，
/// macOS 開發機連跑都跑不了，於是「清了旗標卻留著值」這種一致性錯誤在 CI 上完全隱形。
/// 把純位元運算抽到 Domain 之後，它就跟 <c>PrinterFormMatcherTests</c> 一樣是每次 build 都會跑的。
/// </remarks>
public sealed class DevModePaperFieldsTests
{
    /// <summary>DEVMODE 裡與紙張無關、必須被原封不動保留的位元（方向 / 雙面 / 紙匣 / 份數）。</summary>
    private const uint DmOrientation = 0x00000001;
    private const uint DmDefaultSource = 0x00000200;
    private const uint DmCopies = 0x00000100;
    private const uint DmDuplex = 0x00001000;

    // ───────────────────────── ForFormSelection ─────────────────────────

    [Fact]
    public void Form_selection_sets_papersize_and_clears_custom_width_and_length()
    {
        var result = DevModePaperFields.ForFormSelection(
            DevModePaperFields.PaperWidth | DevModePaperFields.PaperLength);

        (result & DevModePaperFields.PaperSize).Should().NotBe(0, "驅動要吃 dmPaperSize 就得宣告它有效");
        (result & DevModePaperFields.PaperWidth).Should().Be(0);
        (result & DevModePaperFields.PaperLength).Should().Be(0);
    }

    [Fact]
    public void Form_selection_preserves_every_non_paper_bit()
    {
        var others = DmOrientation | DmCopies | DmDefaultSource | DmDuplex;

        var result = DevModePaperFields.ForFormSelection(others | DevModePaperFields.PaperWidth);

        (result & others).Should().Be(others,
            "我們只負責紙張那三格；動到方向／紙匣等於偷改使用者的列印喜好設定");
    }

    // ───────────────────────── ForRestore ─────────────────────────

    [Fact]
    public void Restore_puts_back_exactly_the_snapshot_paper_bits()
    {
        // 快照當時是「自訂寬高」，我們在 apply 時把它換成了表單 ID。
        var snapshot = DevModePaperFields.PaperWidth | DevModePaperFields.PaperLength;
        var current = DevModePaperFields.PaperSize;

        var result = DevModePaperFields.ForRestore(current, snapshot);

        (result & DevModePaperFields.Mask).Should().Be(snapshot);
    }

    [Fact]
    public void Restore_keeps_changes_the_user_made_while_the_viewer_was_open()
    {
        // apply 之後、視窗關閉之前，使用者在原生對話框按「內容」開了雙面列印——
        // 那個 UI 寫的是同一份每使用者預設 DEVMODE。整包 dmFields 蓋回去會清掉 DM_DUPLEX
        // 旗標卻留著 dmDuplex 的值 → 又是一份自相矛盾的 DEVMODE（0x80010105 的第二個入口）。
        var snapshot = DevModePaperFields.PaperSize | DmOrientation;
        var current = DevModePaperFields.PaperSize | DmOrientation | DmDuplex;

        var result = DevModePaperFields.ForRestore(current, snapshot);

        (result & DmDuplex).Should().NotBe(0, "使用者在檢視器開著時改的設定不該被還原動作抹掉");
        (result & DevModePaperFields.Mask).Should().Be(snapshot & DevModePaperFields.Mask);
    }

    [Fact]
    public void Restore_is_idempotent_when_nothing_changed()
    {
        var fields = DevModePaperFields.PaperSize | DmOrientation | DmCopies;

        DevModePaperFields.ForRestore(fields, fields).Should().Be(fields);
    }

    // ───────────────────────── AlreadySelected ─────────────────────────

    [Fact]
    public void Already_selected_when_kind_matches_and_no_custom_size_flags_remain()
    {
        DevModePaperFields.AlreadySelected(
            currentKind: 257, currentFields: DevModePaperFields.PaperSize | DmOrientation, wantedKind: 257)
            .Should().BeTrue();
    }

    [Fact]
    public void Not_already_selected_when_papersize_flag_is_absent()
    {
        // dmPaperSize 剛好等於我們要的值，但旗標沒設 → 驅動根本不會看那一格。
        // 少了這個條件就會誤判成「現況已正確」而跳過寫入，客訴原封不動。
        DevModePaperFields.AlreadySelected(currentKind: 257, currentFields: DmOrientation, wantedKind: 257)
            .Should().BeFalse();
    }

    [Fact]
    public void Not_already_selected_when_custom_width_flag_survives()
    {
        // 殘留的自訂寬高在實務上會贏過 dmPaperSize（v2.3.7 三種報表同時失準的形狀）。
        DevModePaperFields.AlreadySelected(
            currentKind: 257,
            currentFields: DevModePaperFields.PaperSize | DevModePaperFields.PaperWidth,
            wantedKind: 257)
            .Should().BeFalse();
    }

    [Fact]
    public void Not_already_selected_for_a_different_form()
    {
        DevModePaperFields.AlreadySelected(
            currentKind: 9 /* A4 */, currentFields: DevModePaperFields.PaperSize, wantedKind: 257)
            .Should().BeFalse();
    }

    // ───────────────────────── 不變式本身 ─────────────────────────

    [Fact]
    public void Form_selection_output_is_always_already_selected()
    {
        // 兩個函式必須是同一條規則的兩面：apply 寫出去的狀態，下一次進來要判定成「不必再寫」。
        // 任何一邊被單獨改動都會在這裡被抓到（無窮寫入 or 永遠不寫）。
        foreach (var before in new uint[]
                 {
                     0,
                     DevModePaperFields.PaperWidth | DevModePaperFields.PaperLength,
                     DevModePaperFields.Mask,
                     DmOrientation | DmDuplex | DevModePaperFields.PaperLength,
                 })
        {
            DevModePaperFields.AlreadySelected(257, DevModePaperFields.ForFormSelection(before), 257)
                .Should().BeTrue($"起始 dmFields = 0x{before:x}");
        }
    }

    [Fact]
    public void Mask_covers_exactly_the_three_paper_bits()
    {
        DevModePaperFields.Mask.Should().Be(
            DevModePaperFields.PaperSize | DevModePaperFields.PaperLength | DevModePaperFields.PaperWidth);

        // Win32 的 DM_PAPERSIZE / DM_PAPERLENGTH / DM_PAPERWIDTH 常數值，寫死當回歸鎖。
        DevModePaperFields.PaperSize.Should().Be(0x00000002u);
        DevModePaperFields.PaperLength.Should().Be(0x00000004u);
        DevModePaperFields.PaperWidth.Should().Be(0x00000008u);
    }
}
