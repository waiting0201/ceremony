using System.Runtime.ExceptionServices;

namespace Ceremony.PrintForm;

/// <summary>
/// 把工作丟到一條 <b>STA</b> 執行緒上跑，等它結束。
/// </summary>
/// <remarks>
/// <para>
/// <b>2026-08-18 現場客訴的根因</b>：「按下列印後印表機沒反應，**而且對話框卡住關不掉**，
/// 換一台印表機也一樣，最後只能關掉整個程式」。三個症狀合起來指向同一件事——
/// 卡住的是**我們自己這條執行緒**，不是某一台驅動。
/// </para>
/// <para>
/// <c>comdlg32</c> 的對話框與印表機驅動的 UI／設定元件都是 <b>COM，要求 STA</b>
/// （WinForms 的 <c>PrintDialog.ShowDialog</c> 因此規定 <c>Main</c> 要標 <c>[STAThread]</c>，
/// 舊系統 <c>SignupForm</c> 走的就是那條）。而 <b>top-level statements 的 console app
/// 主執行緒預設是 MTA</b>——建立對話框視窗不需要 STA，所以**對話框照樣顯示得出來**；
/// 但使用者按下「列印」之後，comdlg32 要去問驅動（v4 驅動的 DEVMODE⇄PrintTicket 轉換整條都是 COM），
/// 跨 apartment 呼叫要靠訊息幫浦轉手，MTA 執行緒沒有 ⇒ **就停在那裡不回來**。
/// 對話框因此沒被銷毀、owner（預覽視窗）也一直保持 <c>EnableWindow(FALSE)</c>，
/// 使用者看到的正是「對話框關不掉、整個程式動不了」。
/// </para>
/// <para>
/// <b>為什麼 CI 綠燈驗不到</b>：<see cref="SelfTest"/> 走 <c>PD_RETURNDEFAULT</c>——
/// 同一個進入點、同一個結構，但**不畫 UI、也不會有人按確定**，於是完全走不到驅動的 COM 那一段。
/// 那格 PASS 只證明 struct 版面與進入點對，不證明互動路徑活著。
/// </para>
/// <para>
/// <c>apply</c>／<c>restore</c> 刻意**不**改：它們走的是 <c>DocumentProperties</c>／<c>SetPrinter</c>，
/// 現場已有大量成功紀錄，沒有理由跟著動。
/// </para>
/// </remarks>
internal static class StaRunner
{
    internal static T Run<T>(Func<T> work)
    {
        // 非 Windows 只會發生在開發機誤呼；那裡沒有 STA 這回事，直接跑完讓它自己噴。
        if (!OperatingSystem.IsWindows()) return work();

        T? result = default;
        Exception? failure = null;

        var t = new Thread(() =>
        {
            try
            {
                result = work();
            }
            catch (Exception e)
            {
                failure = e;
            }
        });

        t.SetApartmentState(ApartmentState.STA);
        t.Start();
        t.Join();   // 沒有逾時：這條執行緒的壽命＝使用者盯著對話框看多久（見 print-dialog.ts）

        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
        return result!;
    }
}
