using Ceremony.Domain.Reports;
using FluentAssertions;

namespace Ceremony.Application.Tests.Domain;

/// <summary>
/// <see cref="PrintDialogResults"/> —— 跨語言契約的 C# 這一半。
/// </summary>
/// <remarks>
/// TS 端 <c>frontend/electron/print-dialog-core.ts</c> 的 <c>PRINT_RESULTS</c> 有一支對稱的測試。
/// 兩份清單沒有 codegen，只能人工同步 ⇒ 改一邊就要讓另一邊紅。
/// </remarks>
public class PrintDialogResultsTests
{
    [Fact]
    public void 集合恰好是這七個值()
    {
        PrintDialogResults.All.Should().BeEquivalentTo(
        [
            "printed",
            "cancelled",
            "no-default-printer",
            "dialog-failed",
            "render-failed",
            "driver-rejected",
            "error",
        ]);
    }

    [Fact]
    public void 沒有重複值()
    {
        PrintDialogResults.All.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void 全部是小寫kebab_與既有HELPER_RESULTS同一套命名()
    {
        PrintDialogResults.All.Should().OnlyContain(r => r == r.ToLowerInvariant() && !r.Contains(' '));
    }

    [Fact]
    public void cancelled與printed都在清單裡_它們是唯二的正常結束()
    {
        PrintDialogResults.All.Should().Contain(PrintDialogResults.Printed)
            .And.Contain(PrintDialogResults.Cancelled);
    }
}
