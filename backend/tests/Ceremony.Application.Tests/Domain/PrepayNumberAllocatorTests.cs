using Ceremony.Domain.Services;
using FluentAssertions;

namespace Ceremony.Application.Tests.Domain;

public sealed class PrepayNumberAllocatorTests
{
    [Fact]
    public void SingleNonFixed_gets_maxPlusOne()
    {
        var a = PrepayNumberAllocator.Allocate(maxNumber: 50, fixedPreservedNumbers: [], nonFixedCount: 1);

        a.FixedNumbers.Should().BeEmpty();
        a.NonFixedNumbers.Should().Equal(51);
        a.FilledGaps.Should().BeEmpty();
    }

    [Fact]
    public void SingleFixed_preserves_sourceNumber()
    {
        var a = PrepayNumberAllocator.Allocate(maxNumber: 0, fixedPreservedNumbers: [7], nonFixedCount: 0);

        a.FixedNumbers.Should().Equal(7);
        a.NonFixedNumbers.Should().BeEmpty();
    }

    [Fact]
    public void FixedGaps_filledBy_nonFixed_thenContinues()
    {
        // 固定 [2,5]（max=0，nextNo 從 1 起）→ gaps [1] 後 [3,4]，共 [1,3,4]
        // 非固定 5 筆 → 先取 gaps 1,3,4，再續 6,7
        var a = PrepayNumberAllocator.Allocate(maxNumber: 0, fixedPreservedNumbers: [2, 5], nonFixedCount: 5);

        a.FixedNumbers.Should().Equal(2, 5);
        a.NonFixedNumbers.Should().Equal(1, 3, 4, 6, 7);
        a.FilledGaps.Should().Equal(1, 3, 4);
    }

    [Fact]
    public void LegacyBackwardSet_fixedBelowCounter_resetsCounter_notMax()
    {
        // 對齊舊 LoadPrepayForm：目標已有資料 (max=50)，固定號 5 落在既存範圍內。
        // 舊系統把計數器「往回設」成 6（非 Math.Max 保持 51），非固定接著拿 6。
        var a = PrepayNumberAllocator.Allocate(maxNumber: 50, fixedPreservedNumbers: [5], nonFixedCount: 1);

        // 非固定拿到 6（舊系統固定號小於計數器時 oneno = Number+1 往回設），而非 Math.Max 保留的 51。
        a.FixedNumbers.Should().Equal(5);
        a.NonFixedNumbers.Should().Equal(new[] { 6 });
        a.FilledGaps.Should().BeEmpty();
    }

    [Fact]
    public void NullPreservedNumber_fallsBackTo_sequential()
    {
        // 防禦性：固定候選缺 Number 時退回續序（不應在正常資料出現）。
        var a = PrepayNumberAllocator.Allocate(maxNumber: 10, fixedPreservedNumbers: [null], nonFixedCount: 0);

        a.FixedNumbers.Should().Equal(11);
    }

    [Fact]
    public void NoCandidates_returns_empty()
    {
        var a = PrepayNumberAllocator.Allocate(maxNumber: 0, fixedPreservedNumbers: [], nonFixedCount: 0);

        a.FixedNumbers.Should().BeEmpty();
        a.NonFixedNumbers.Should().BeEmpty();
        a.FilledGaps.Should().BeEmpty();
    }
}
