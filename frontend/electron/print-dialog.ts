// 決策 11 的送印路徑（執行面）：spawn helper 的 print 子命令，逐行讀它的 NDJSON。
//
// 背景見 print-dialog-core.ts 檔頭。這一層只負責三件事：
//   1. 把 helper 叫起來、逐行解析（純函式在 core）
//   2. **讀到第一行 dialog-shown 就 resolve** —— 決策 8 的守法，見下
//   3. busy guard：一次只准有一個列印對話框
import { spawn } from 'child_process';
import { existsSync } from 'fs';
import type { BrowserWindow } from 'electron';
import { logPrintEvent } from './print-log';
import { helperExePath } from './print-form';
import {
  parsePrintLine,
  printDialogArgs,
  printDialogLogFields,
  type PrintDialogOutcome,
  type PrintResult,
} from './print-dialog-core';

/**
 * 對話框開著時不再開第二個。
 *
 * modal 疊 modal 會讓現場完全搞不清楚在跟哪一個視窗講話，而且第二個的 owner 已經被
 * 第一個 disable 掉了。這與 print-form.ts 的 `inFlight` 是不同的東西——那一個防的是
 * 「疊第二個驅動呼叫」，這一個防的是「疊第二個視窗」。
 */
let dialogOpen = false;

/**
 * 現在跑著的 helper 行程。只為了那顆止血鍵（`abortPrintDialog`）而存在。
 *
 * ⚠️ 決策 9d 說「逾時**絕不** kill」，這裡不是推翻它而是它的例外，兩點理由都要成立才准 kill：
 * 1. **是使用者主動按的**，不是我們替他決定的逾時——現場的替代方案是關掉整個程式（2026-08-18 客訴），
 *    砍一個 helper 顯然比砍整個 app 溫和
 * 2. **這條路徑不寫任何共用系統狀態**（決策 11 的不變式：零 SetPrinter、零還原 journal），
 *    所以「砍在半路留下壞掉的共用狀態」這個 9d 真正在擔心的後果，在這裡不存在
 */
let current: import('child_process').ChildProcess | null = null;

/**
 * 逾時只寫一行紀錄，**絕不 kill**。
 *
 * 決策 9d 的教訓：在 Win32／COM 呼叫進行到一半 TerminateProcess，對方留在什麼狀態不由我們
 * 決定——那正是 2026-08-10「按下列印鈕整個 app 卡死」的成因。而且這裡的行程壽命本來就等於
 * 「使用者盯著對話框看多久」，十分鐘沒動作是完全正常的人類行為，不是故障。
 */
const STILL_OPEN_LOG_MS = 10 * 60_000;

export interface DialogPrintOptions {
  reportType: string;
  pdfPath: string;
  owner: BrowserWindow | null;
  /** 使用者把「自動選紙」關掉時傳 true（＝ helper 不去改我們手上那份 DEVMODE 的紙張）。 */
  noForm: boolean;
  jobName?: string | null;
  /**
   * helper 行程退出時回呼一次，帶最終結果。
   *
   * ⚠️ **這不是 resolve 的時機**（那永遠是 `dialog-shown`，見下）。它純粹是「事後告知」：
   * 2026-08-18 現場回報「按了列印，印表機沒有反應」時我們才發現，對話框之後的四種結局
   * （printed / driver-rejected / render-failed / error）在畫面上完全無法區分，
   * 全都只進診斷紀錄 ⇒ 現場講得出來的只有「沒有反應」。
   * 回呼裡**只准顯示訊息**，不得讓任何按鈕回到 disabled。
   */
  onFinal?: (outcome: PrintDialogOutcome) => void;
  /**
   * 對話框出現的那一刻回呼一次（helper 的第一行 NDJSON）。
   *
   * 存在的唯一理由是**自動選紙的結果要在對話框還開著的時候講**——那是使用者唯一能補救
   * （自己去對話框選紙）的時機。等到 `onFinal` 才說，紙已經印出去了。
   */
  onShown?: (outcome: PrintDialogOutcome) => void;
}

/**
 * 叫出列印對話框並送印。
 *
 * ⚠️ **回傳的時機是「對話框已經在螢幕上」，不是「印完了」。**
 *
 * 這是 blueprint 決策 8 的直接後果：舊版的 `webContents.print(options, callback)` 有時候
 * callback 不會回來（紙已經印出、job 已進 spooler，但 callback 沒觸發），呼叫端的 await
 * 就永遠掛著，UI 的 `printing` signal 永久卡在「列印中」，只能換頁重建元件。
 * 教訓是「**不要把 UI 的可用狀態綁在 spooler 的回應上**」。
 *
 * 所以：第一行 `dialog-shown` 一到就 resolve；之後的 printed / cancelled / 逾時 / 行程退出
 * **只寫診斷紀錄，不碰 UI**。
 */
export async function printViaDialog(opt: DialogPrintOptions): Promise<{ result: PrintResult }> {
  if (process.platform !== 'win32') return { result: 'skipped-not-windows' };
  if (dialogOpen) return { result: 'helper-busy' };

  const exe = helperExePath();
  if (!exe || !existsSync(exe)) return { result: 'helper-missing' };

  const ownerHandle = readHandle(opt.owner);
  const args = printDialogArgs(opt.pdfPath, {
    owner: ownerHandle,
    reportType: opt.reportType,
    noForm: opt.noForm,
    jobName: opt.jobName,
  });

  dialogOpen = true;

  return new Promise((resolve) => {
    let settled = false;
    const acc: PrintDialogOutcome = { result: 'error' };

    const settle = (result: PrintResult): void => {
      if (settled) return;
      settled = true;
      resolve({ result });
    };

    const child = spawn(exe, args, { windowsHide: true });
    current = child;

    let buffer = '';
    child.stdout.setEncoding('utf8');
    child.stdout.on('data', (chunk: string) => {
      buffer += chunk;
      // 逐行處理；最後一段可能不完整，留在 buffer 等下一次。
      const lines = buffer.split(/\r?\n/);
      buffer = lines.pop() ?? '';
      for (const line of lines) {
        const parsed = parsePrintLine(line);
        if (!parsed) continue;
        Object.assign(acc, parsed);

        // ⚠️ 這就是決策 8 的守法：對話框一出現就放行 UI，不等 spooler。
        if (parsed.shown) {
          settle('printed');
          try {
            opt.onShown?.(acc);
          } catch {
            // 呼叫端的顯示邏輯出事不得影響列印通道
          }
        }
      }
    });

    const timer = setTimeout(() => {
      void logPrintEvent({ event: 'print-dialog-still-open', reportType: opt.reportType });
    }, STILL_OPEN_LOG_MS);
    timer.unref();

    child.on('error', (e) => {
      clearTimeout(timer);
      dialogOpen = false;
      current = null;
      acc.result = 'helper-error';
      acc.error = e.message;
      void logPrintEvent({ reportType: opt.reportType, ...printDialogLogFields(acc) });
      settle('helper-error');
    });

    child.on('close', () => {
      clearTimeout(timer);
      dialogOpen = false;
      current = null;

      // 最後一行的 result 才是真正的結果；沒讀到就是 helper 沒把話講完。
      if (!acc.result || acc.result === 'error') acc.result ??= 'error';
      void logPrintEvent({ reportType: opt.reportType, ...printDialogLogFields(acc) });

      // 事後告知（見 onFinal 的註解）。放在 settle 之前或之後都可以——
      // 對話框早就 resolve 過了，這裡的 settle 只對「對話框根本沒開起來」那條路有意義。
      try {
        opt.onFinal?.(acc);
      } catch {
        // 呼叫端的顯示邏輯出事不得影響列印通道
      }

      // 行程在對話框出現之前就退出（no-default-printer / render-failed）⇒ 這才輪到 UI 收到失敗。
      settle(acc.result);
    });
  });
}

/**
 * 取視窗的原生 HWND。
 *
 * 綁 owner 有兩個作用：對話框壓在預覽視窗上面，以及 modal 期間 `EnableWindow(FALSE)` owner——
 * 後者正好讓「對話框開著時預覽窗被關掉、temp 檔被刪」這個競態**不可能發生**。
 */
function readHandle(win: BrowserWindow | null): bigint | null {
  if (!win || win.isDestroyed()) return null;
  try {
    const buf = win.getNativeWindowHandle();
    return buf.length >= 8 ? buf.readBigUInt64LE(0) : BigInt(buf.readUInt32LE(0));
  } catch {
    return null;
  }
}

/**
 * 使用者按下的止血鍵：把還開著的列印對話框連同 helper 一起結束掉。
 *
 * 為什麼需要（2026-08-18 客訴）：對話框是 modal 且 owner 綁著預覽視窗，helper 一旦卡住，
 * 預覽視窗就一直是 `EnableWindow(FALSE)`，現場**唯一**的出路是關掉整個程式
 * （然後連預覽都來不及正常收尾）。這顆鍵讓那個出路變成「只砍 helper」。
 *
 * 放在主視窗的排障列而不是預覽視窗：後者正是被 disable 的那一個，按不到。
 *
 * @returns 有沒有東西可砍（false ＝ 本來就沒有對話框開著）
 */
export function abortPrintDialog(): boolean {
  const child = current;
  if (!child) return false;

  void logPrintEvent({ event: 'print-dialog-aborted', path: 'dialog' });
  // SIGKILL 在 Windows 上一律走 TerminateProcess；這裡是刻意的（見 `current` 的註解）。
  child.kill('SIGKILL');
  return true;
}
