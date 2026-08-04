// 列印前預選驅動自訂表單 —— 子行程呼叫、refcount、還原 journal。
//
// 背景（docs/blueprints/print-channel-electron.md 決策 9）：
// 舊系統在跳原生列印對話框前，會拿報表的中文名去驅動的紙張清單比對，命中的表單帶著驅動自己的
// dmPaperSize ID，驅動因此自動套用該表單的尺寸與紙匣（SignupForm.cs:1770-1787，註解原文
// 「取得印表機尺寸設定」）。這就是客訴「舊系統送出列印會自動找到印表機的設定」的全部機制。
//
// 新系統的送印是 Chromium PDF 檢視器 → 使用者按 🖨 → Windows 原生 PrintDlgEx，JS 層沒有注入點
// （見 docs/gotchas.md「print({silent:false}) 不會把你傳的設定帶進系統列印對話框」）。唯一的注入點
// 是驅動的**每使用者預設 DEVMODE**，也就是 Ceremony.PrintForm.exe 的 SetPrinter Level 9。
//
// ⚠️ 那份 DEVMODE 是整個使用者工作階段共用的（改成「資料卡」之後 Word/Excel 開新文件也會變那張紙），
// 舊系統沒有這個副作用，所以我們必須自己收乾淨 → refcount + 還原 journal。
//
// **不變式：本模組的任何結果都不得影響列印成敗。** helper 缺檔／逾時／驅動拒絕一律照常開檢視器視窗。
import { app } from 'electron';
import { execFile } from 'child_process';
import { promises as fs, existsSync } from 'fs';
import path from 'path';
import { logPrintEvent } from './print-log';
import {
  FormApplyResult,
  RestoreSnapshot,
  needsRestore,
  parseHelperOutput,
  restoreArgs,
  skippedNotWindows,
} from './print-form-core';

/** 驅動正常時 <200ms；網路印表機離線時不能讓使用者以為程式當掉。 */
const TIMEOUT_MS = 3000;
/** execFile 的 timeout 在極端情況可能不觸發，外面再包一層硬逾時。 */
const HARD_TIMEOUT_MS = 3500;

/** 同時開著的檢視器視窗數。最後一個關掉才還原——否則會弄掉另一個視窗的紙。 */
let viewerCount = 0;

function helperPath(): string | null {
  if (process.env['CEREMONY_PRINTFORM_EXE']) return process.env['CEREMONY_PRINTFORM_EXE'];
  // dev 沒有這支 exe（macOS 上 System.Drawing.Common 連跑都跑不起來），刻意不做 dotnet run fallback。
  if (!app.isPackaged) return null;
  return path.join(process.resourcesPath, 'printform', 'Ceremony.PrintForm.exe');
}

/** 還原 journal：與 config.json 同目錄，app 崩潰時留下來給下次啟動撿。 */
function journalPath(): string {
  return path.join(app.getPath('appData'), 'Ceremony', 'print-form-restore.json');
}

// ───────────────────────── 對外 ─────────────────────────

/**
 * 開檢視器視窗**之前**呼叫：把預設印表機的紙張預選成該報表對應的驅動表單。
 * 絕不丟例外；失敗一律回一個帶 result 的物件，呼叫端只拿來寫 log 與決定視窗標題。
 */
export async function applyReportForm(reportType: string): Promise<FormApplyResult> {
  try {
    if (process.platform !== 'win32') return skippedNotWindows();

    const exe = helperPath();
    if (!exe || !existsSync(exe)) return { result: 'helper-missing' };

    const r = await run(exe, ['apply', reportType]);

    // 只有第一個視窗拍的快照才是「真正的原始值」——第二個視窗看到的已經是我們自己設的紙。
    if (viewerCount === 0 && needsRestore(r) && r.prev) await writeJournal(r.prev);

    return r;
  } catch (e) {
    return { result: 'helper-error', error: (e as Error).message };
  }
}

/** 檢視器視窗成功開起來之後呼叫。 */
export function noteViewerOpened(): void {
  viewerCount++;
}

/**
 * 檢視器視窗關閉（或開窗失敗）時呼叫。最後一個視窗關掉才真的還原。
 *
 * 時機安全：使用者按下 PrintDlgEx「確定」的瞬間 DEVMODE 已被複製進 spool job，之後改回來
 * 不影響已送出的工作；而對話框是該視窗的 modal 子視窗，不可能在對話框開著時視窗被 closed。
 */
export async function releaseReportForm(): Promise<void> {
  if (viewerCount > 0) viewerCount--;
  if (viewerCount === 0) await restoreFromJournal();
}

/**
 * 啟動時撿上次崩潰留下的還原 journal（掛在 sweepTempDir 旁，不另立機制）。
 */
export async function recoverPendingFormRestore(): Promise<void> {
  const snap = await readJournal();
  if (!snap) return;
  const r = await restoreNow(snap);
  void logPrintEvent({ event: 'form-restore-recovered', formResult: r });
}

// ───────────────────────── 內部 ─────────────────────────

async function restoreFromJournal(): Promise<void> {
  const snap = await readJournal();
  if (!snap) return;
  const r = await restoreNow(snap);
  // 還原成功不寫 log——那會讓每次列印變成兩行。
  if (r !== 'restored') void logPrintEvent({ event: 'form-restore-failed', formResult: r });
}

/**
 * 實際還原，並且**不論成敗都刪掉 journal**：留著只會讓永久性失敗每次啟動重試一次，
 * 而使用者早就自己去列印喜好設定改回來了。失敗有 log 可查。
 */
async function restoreNow(snap: RestoreSnapshot): Promise<string> {
  try {
    if (process.platform !== 'win32') return 'skipped-not-windows';
    const exe = helperPath();
    if (!exe || !existsSync(exe)) return 'helper-missing';
    const r = await run(exe, restoreArgs(snap));
    return r.result;
  } catch (e) {
    return `helper-error: ${(e as Error).message}`;
  } finally {
    await fs.unlink(journalPath()).catch(() => undefined);
  }
}

function run(exe: string, args: string[]): Promise<FormApplyResult> {
  return new Promise((resolve) => {
    let settled = false;
    const done = (r: FormApplyResult): void => {
      if (settled) return;
      settled = true;
      resolve(r);
    };

    const timer = setTimeout(() => done({ result: 'helper-timeout' }), HARD_TIMEOUT_MS);
    timer.unref?.();

    try {
      execFile(
        exe,
        args,
        { timeout: TIMEOUT_MS, killSignal: 'SIGKILL', windowsHide: true, encoding: 'utf8', maxBuffer: 64 * 1024 },
        (err, stdout) => {
          clearTimeout(timer);
          if (err && !stdout) {
            done({ result: err.killed ? 'helper-timeout' : 'helper-error', error: err.message });
            return;
          }
          done(parseHelperOutput(stdout));
        },
      );
    } catch (e) {
      clearTimeout(timer);
      done({ result: 'helper-error', error: (e as Error).message });
    }
  });
}

async function writeJournal(snap: RestoreSnapshot): Promise<void> {
  try {
    const file = journalPath();
    await fs.mkdir(path.dirname(file), { recursive: true });
    await fs.writeFile(file, JSON.stringify({ ...snap, ts: new Date().toISOString() }), 'utf-8');
  } catch {
    // 寫不出來就沒有還原保險，但不能因此擋掉列印
  }
}

async function readJournal(): Promise<RestoreSnapshot | null> {
  try {
    const raw = JSON.parse(await fs.readFile(journalPath(), 'utf-8')) as Partial<RestoreSnapshot>;
    if (
      typeof raw.kind !== 'number' ||
      typeof raw.fields !== 'number' ||
      typeof raw.w !== 'number' ||
      typeof raw.h !== 'number'
    ) {
      return null;
    }
    return { kind: raw.kind, fields: raw.fields, w: raw.w, h: raw.h, printer: raw.printer };
  } catch {
    return null;
  }
}
