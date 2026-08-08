using Ceremony.Domain.Reports;
using FluentAssertions;

namespace Ceremony.Application.Tests.Domain;

/// <summary>
/// 鎖住寫入每使用者預設 DEVMODE 之前那道 PrintTicket 預檢的判定規則。
/// </summary>
/// <remarks>
/// 起點是 2026-08-08 客訴：KYOCERA PA2000 在 v2.4.2 修好旗標／值一致性之後**仍然**噴
/// <c>0x80010105</c>。結論是 <c>DevModePaperFields</c> 只能保證「沒違反已知的不變式」，
/// 驅動吃不吃終究是猜的——所以改成寫入前先自己跑一次列印 UI 會做的那個轉換。
///
/// 這組測試存在的理由同 <c>PrinterFormPolicyTests</c>：真正呼叫 prntvpt.dll 的
/// Ceremony.PrintForm 是 net10.0-windows 的 exe，macOS 開發機與 CI 都跑不到，
/// 判定邏輯留在那裡等於沒有測試。
/// </remarks>
public sealed class PrintTicketPreflightTests
{
    [Fact]
    public void Only_a_successful_conversion_may_write()
    {
        var hrs = new int?[] { null, unchecked((int)0x80010105), unchecked((int)0x80070057), -1, 0, 1 };

        var writable = (from open in hrs
                        from convert in hrs
                        where PrintTicketPreflight.MayWrite(PrintTicketPreflight.Classify(open, convert))
                        select (open, convert)).ToList();

        // 核心不變式：兩段 HRESULT 都必須成功（>= 0）才准動那份共用狀態。
        writable.Should().OnlyContain(c => c.open >= 0 && c.convert >= 0,
            "檢查跑不起來時我們並不知道寫入會不會變好，而不知道時的預設值是不動");
        writable.Should().NotBeEmpty("否則就是整個功能被關掉了，不是預檢");
    }

    [Fact]
    public void Driver_rejecting_the_conversion_is_reported_apart_from_the_check_not_running()
    {
        // 兩者都不寫入，分兩格純粹是為了診斷紀錄查得下去：
        // 「這台驅動不接受」要改的是我們的做法，「連檢查都做不了」要查的是那台機器。
        PrintTicketPreflight.Classify(0, unchecked((int)0x80010105))
            .Should().Be(PrintTicketPreflight.PreflightOutcome.Rejected);

        PrintTicketPreflight.Classify(unchecked((int)0x80004005), null)
            .Should().Be(PrintTicketPreflight.PreflightOutcome.Unavailable);

        // provider 開得起來但轉換那步根本沒跑到（例外／建 stream 失敗）＝ 沒驗到，同樣不寫。
        PrintTicketPreflight.Classify(0, null)
            .Should().Be(PrintTicketPreflight.PreflightOutcome.Unavailable);
    }

    [Fact]
    public void Result_strings_match_the_cross_language_contract()
    {
        // ⚠️ 這兩個字串必須與 electron/print-form-core.ts 的 HELPER_RESULTS 一字不差，
        // 少一個 → parseHelperOutput 把整包退成 helper-error。
        PrintTicketPreflight.ToResult(PrintTicketPreflight.PreflightOutcome.Rejected)
            .Should().Be("skipped-printticket-reject");
        PrintTicketPreflight.ToResult(PrintTicketPreflight.PreflightOutcome.Unavailable)
            .Should().Be("skipped-printticket-unavailable");
    }

    [Fact]
    public void Pass_has_no_skip_string()
    {
        // Pass 之後還可能被 SetPrinter 拒絕，結果字串只能由實際寫入成敗決定。
        var act = () => PrintTicketPreflight.ToResult(PrintTicketPreflight.PreflightOutcome.Pass);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
