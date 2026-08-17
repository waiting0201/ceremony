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
        if (parsed.shown) settle('printed');
      }
    });

    const timer = setTimeout(() => {
      void logPrintEvent({ event: 'print-dialog-still-open', reportType: opt.reportType });
    }, STILL_OPEN_LOG_MS);
    timer.unref();

    child.on('error', (e) => {
      clearTimeout(timer);
      dialogOpen = false;
      acc.result = 'helper-error';
      acc.error = e.message;
      void logPrintEvent({ reportType: opt.reportType, ...printDialogLogFields(acc) });
      settle('helper-error');
    });

    child.on('close', () => {
      clearTimeout(timer);
      dialogOpen = false;

      // 最後一行的 result 才是真正的結果；沒讀到就是 helper 沒把話講完。
      if (!acc.result || acc.result === 'error') acc.result ??= 'error';
      void logPrintEvent({ reportType: opt.reportType, ...printDialogLogFields(acc) });

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
