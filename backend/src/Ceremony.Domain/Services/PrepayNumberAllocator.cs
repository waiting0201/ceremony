namespace Ceremony.Domain.Services;

/// <summary>
/// 預繳載入的編號分配演算法（純函式）。從 <c>LoadPrepayForm.btnConfirm_Click</c> 每個 case 內
/// 「固定編號保留 + 跳號收集 + 非固定填補空號 + 續序」的邏輯萃取而來。
/// </summary>
/// <remarks>
/// 對照舊系統 LoadPrepayForm.cs:81-196（case 1，其餘 5 case 結構相同）。
/// 逐條對齊：
/// <list type="bullet">
///   <item>起始號 <c>nextNo = maxNumber + 1</c>（舊 <c>oneno = lastsignup==null ? 1 : Number+1</c>；ISNULL(MAX,0)=0 時亦得 1）。</item>
///   <item>固定編號保留原號；跳號區間收集成 gaps。</item>
///   <item>計數器每處理一個固定編號後一律設為 <c>該固定號 + 1</c>（對齊舊 line 132/136；
///         注意舊系統在固定號小於當前計數器時會把計數器「往回設」，此處刻意保留同一行為，不用 Math.Max）。</item>
///   <item>非固定：先依序取 gaps，取完才續 <c>nextNo</c>（舊 line 184-193）。</item>
/// </list>
/// </remarks>
public static class PrepayNumberAllocator
{
    /// <param name="maxNumber">目標 (Year, Ceremony, SignupType) 現有最大 Number；無資料傳 0。</param>
    /// <param name="fixedPreservedNumbers">固定編號信眾的來源 Number（依 Number 升冪；理論上非 null）。</param>
    /// <param name="nonFixedCount">非固定編號信眾筆數（依來源 Number 升冪處理）。</param>
    public static PrepayAllocation Allocate(
        int maxNumber,
        IReadOnlyList<int?> fixedPreservedNumbers,
        int nonFixedCount)
    {
        var nextNo = maxNumber + 1;
        var gaps = new List<int>();
        var fixedNumbers = new List<int>(fixedPreservedNumbers.Count);

        // 第一批：固定編號 — 保留原號、收集跳號（對齊舊 line 82-138）
        foreach (var preserved in fixedPreservedNumbers)
        {
            // 固定編號理論上非 null；防禦性地在缺號時退回續序。
            var n = preserved ?? nextNo;
            fixedNumbers.Add(n);

            if (nextNo < n)
            {
                for (var x = nextNo; x < n; x++) gaps.Add(x);
            }

            // 舊系統一律 oneno = Number + 1（含固定號 < 計數器時往回設），刻意不使用 Math.Max 以完全對齊。
            nextNo = n + 1;
        }

        // 第二批：非固定編號 — 先填 gaps，取完才續 nextNo（對齊舊 line 143-193）
        var nonFixedNumbers = new List<int>(nonFixedCount);
        var gapIdx = 0;
        for (var i = 0; i < nonFixedCount; i++)
        {
            if (gapIdx < gaps.Count)
            {
                nonFixedNumbers.Add(gaps[gapIdx]);
                gapIdx++;
            }
            else
            {
                nonFixedNumbers.Add(nextNo);
                nextNo++;
            }
        }

        return new PrepayAllocation(fixedNumbers, nonFixedNumbers, gaps.Take(gapIdx).ToList());
    }
}

/// <summary>編號分配結果。<see cref="FixedNumbers"/> 與 <see cref="NonFixedNumbers"/> 各自對齊輸入順序。</summary>
public sealed record PrepayAllocation(
    IReadOnlyList<int> FixedNumbers,
    IReadOnlyList<int> NonFixedNumbers,
    IReadOnlyList<int> FilledGaps);
