using Ceremony.Domain.Reports;
using FluentAssertions;

namespace Ceremony.Application.Tests.Domain;

/// <summary>
/// <see cref="PrinterContactPolicy"/> ——「這台驅動還要不要碰」的閘門。
/// </summary>
/// <remarks>
/// 這條回歸鎖的是 2026-08-10 客訴（KYOCERA PA2000 按下列印鈕後整個 app 卡死）的處置：
/// 失敗過的印表機不再接觸、呼叫端不等了就不寫入。決策見 blueprint 決策 9d。
/// </remarks>
public class PrinterContactPolicyTests
{
    [Fact]
    public void ParseBlocked_空值一律回空清單()
    {
        PrinterContactPolicy.ParseBlocked(null).Should().BeEmpty();
        PrinterContactPolicy.ParseBlocked("").Should().BeEmpty();
        PrinterContactPolicy.ParseBlocked("   ").Should().BeEmpty();
        PrinterContactPolicy.ParseBlocked(",,").Should().BeEmpty();
    }

    [Fact]
    public void ParseBlocked_去空白去重轉小寫()
    {
        PrinterContactPolicy.ParseBlocked(" A1B2C3D4 , a1b2c3d4 ,ff00ee11")
            .Should().Equal("a1b2c3d4", "ff00ee11");
    }

    [Theory]
    [InlineData("a1b2c3d4")]
    [InlineData("A1B2C3D4")]   // 現場可能手動編輯過那份 JSON
    [InlineData(" a1b2c3d4 ")]
    public void IsBlocked_大小寫與前後空白都要擋得住(string hash)
    {
        PrinterContactPolicy.IsBlocked(hash, ["a1b2c3d4"]).Should().BeTrue();
    }

    [Fact]
    public void IsBlocked_名單外的印表機照常放行()
    {
        PrinterContactPolicy.IsBlocked("ff00ee11", ["a1b2c3d4"]).Should().BeFalse();
        PrinterContactPolicy.IsBlocked("a1b2c3d4", []).Should().BeFalse();
        PrinterContactPolicy.IsBlocked(null, ["a1b2c3d4"]).Should().BeFalse();
    }

    [Fact]
    public void WithinBudget_沒給預算就一律放行()
    {
        // 打錯參數不該把整個功能靜默關掉——Program.ParseBudget 也把壞值當成「沒給」。
        PrinterContactPolicy.WithinBudget(99_999, null).Should().BeTrue();
        PrinterContactPolicy.WithinBudget(99_999, 0).Should().BeTrue();
        PrinterContactPolicy.WithinBudget(99_999, -1).Should().BeTrue();
    }

    [Theory]
    [InlineData(0, 3000, true)]
    [InlineData(2999, 3000, true)]
    [InlineData(3000, 3000, false)]   // 到點的那一刻呼叫端已經 resolve 了，邊界寧可不寫
    [InlineData(9000, 3000, false)]
    public void WithinBudget_逾時後就不該再寫入(long elapsed, int budget, bool expected)
    {
        PrinterContactPolicy.WithinBudget(elapsed, budget).Should().Be(expected);
    }
}
