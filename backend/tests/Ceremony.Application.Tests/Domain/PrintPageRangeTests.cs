using Ceremony.Domain.Reports;
using FluentAssertions;

namespace Ceremony.Application.Tests.Domain;

/// <summary>
/// <see cref="PrintPageRange"/> —— 列印對話框的頁面範圍。
/// </summary>
/// <remarks>
/// 這組測試最重要的一條是「**任何說不通的輸入都不得回空集合**」：
/// 使用者按了確定就是要印東西，靜默印出零頁是最難查的失敗
/// （沒有錯誤訊息、沒有紙、診斷紀錄還顯示 printed）。
/// 續印能力的來由見 blueprint 決策 4。
/// </remarks>
public class PrintPageRangeTests
{
    private const uint All = 0;
    private const uint Range = PrintPageRange.PageNums;

    [Fact]
    public void 沒勾頁面範圍就是全部()
    {
        PrintPageRange.Resolve(All, 0, 0, 3).Should().Equal(1, 2, 3);
    }

    [Fact]
    public void 沒勾頁面範圍時就算帶了起迄也照樣全部()
    {
        // 對話框沒設 PD_PAGENUMS 時 nFromPage/nToPage 的內容是不保證的，不可採信。
        PrintPageRange.Resolve(All, 2, 2, 5).Should().Equal(1, 2, 3, 4, 5);
    }

    [Fact]
    public void 勾了頁面範圍就照範圍()
    {
        PrintPageRange.Resolve(Range, 2, 4, 10).Should().Equal(2, 3, 4);
    }

    [Fact]
    public void 單頁()
    {
        PrintPageRange.Resolve(Range, 3, 3, 10).Should().Equal(3);
    }

    [Fact]
    public void 起迄填反了要修正而不是回空()
    {
        // 現場手誤常見：想印 600-1200 卻填成 1200-600
        PrintPageRange.Resolve(Range, 1200, 600, 5000).Should().HaveCount(601).And.StartWith([600]);
    }

    [Fact]
    public void 超出總頁數要夾回範圍內()
    {
        PrintPageRange.Resolve(Range, 8, 999, 10).Should().Equal(8, 9, 10);
    }

    [Fact]
    public void 只給其中一邊_視為只印那一頁()
    {
        PrintPageRange.Resolve(Range, 0, 4, 10).Should().Equal(4);
        PrintPageRange.Resolve(Range, 4, 0, 10).Should().Equal(4);
    }

    [Fact]
    public void 勾了範圍卻兩邊都沒給_退回全部而不是空的()
    {
        PrintPageRange.Resolve(Range, 0, 0, 3).Should().Equal(1, 2, 3);
    }

    [Fact]
    public void 總頁數為零才是唯一的空集合()
    {
        PrintPageRange.Resolve(All, 0, 0, 0).Should().BeEmpty();
        PrintPageRange.Resolve(Range, 1, 5, 0).Should().BeEmpty();
        PrintPageRange.Resolve(All, 0, 0, -1).Should().BeEmpty();
    }

    [Fact]
    public void 掃過所有組合_只要有頁數就永遠印得到東西()
    {
        // 這是本檔的主張：除了「這份 PDF 根本沒有頁」以外，不存在會回空集合的輸入。
        foreach (var flags in new[] { All, Range })
        {
            foreach (var from in new[] { -5, 0, 1, 3, 99 })
            {
                foreach (var to in new[] { -5, 0, 1, 3, 99 })
                {
                    PrintPageRange.Resolve(flags, from, to, 10)
                        .Should().NotBeEmpty($"flags={flags} from={from} to={to} 不該印出零頁");
                }
            }
        }
    }

    [Fact]
    public void 回傳的頁碼永遠落在1到總頁數之間()
    {
        foreach (var from in new[] { -5, 0, 1, 7, 99 })
        {
            foreach (var to in new[] { -5, 0, 2, 50 })
            {
                PrintPageRange.Resolve(Range, from, to, 10)
                    .Should().OnlyContain(p => p >= 1 && p <= 10);
            }
        }
    }
}
