// 子視窗（PDF 檢視器／另開的預覽視窗）關閉後，把焦點交還主視窗。
//
// 為什麼需要（客訴 2026-08-10）：同時開著 Word／檔案總管時，列印完把預覽關掉，整個系統會沉到
// 別的程式後面。Windows 關閉一個視窗時焦點交給 **z-order 的下一個視窗**，而 owner 關係只保證
// 子視窗壓在主視窗上面，不保證關掉後焦點回到 owner——中間又隔了一個 modal 的 PrintDlgEx，
// 焦點鏈更容易斷在別的程式上。使用者看到的是「按完列印，系統不見了」。
//
// 只有在子視窗關閉的**當下自己持有焦點**時才把焦點搶回來：使用者若已經切到別的程式、再從
// 工作列把預覽關掉，那是他刻意離開，硬把畫面拉回來反而是打斷人。
// 只用到型別 → `import type`，spec（跑在 karma、沒有 electron runtime）才 import 得動。
import type { BrowserWindow } from 'electron';

export function returnFocusOnClose(child: BrowserWindow, parent: BrowserWindow | null): void {
  if (!parent) return;

  // 焦點狀態只有 `close` 問得到（`closed` 時原生視窗已銷毀）；交還焦點則必須等 `closed`，
  // 太早呼叫會被視窗銷毀時系統自己的焦點轉移蓋掉。
  let wasFocused = false;
  child.on('close', () => {
    wasFocused = child.isFocused();
  });
  child.on('closed', () => {
    if (!wasFocused) return;
    if (parent.isDestroyed() || parent.isMinimized()) return; // 主視窗最小化＝使用者本來就沒在看它
    parent.focus();
  });
}
