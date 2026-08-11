// 列印前預選驅動自訂表單 —— 子行程呼叫、refcount、還原 journal、失敗印表機黑名單。
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
// **不變式一：本模組的任何結果都不得影響列印成敗。** helper 缺檔／逾時／驅動拒絕一律照常開檢視器視窗。
// **不變式二：有寫入就一定要有還原 journal。** 否則我們選的紙會永久留在使用者的 Word/Excel 上。
//
// 2026-08-10（決策 9d，KYOCERA PA2000「按下列印鈕後整個 app 卡死」）加上第三條：
// **不變式三：這台驅動只要出過一次事，就再也不碰。** 而且逾時不再 kill helper——
// 在 Win32／COM 呼叫中途 TerminateProcess，對方留在什麼狀態不由我們決定，
// 之後 PrintDlgEx 去問同一個 provider 就可能永遠等不到（見 C# 的 PrinterContactPolicy）。
import { app } from 'electron';
import { execFile } from 'child_process';
import { promises as fs, existsSync } from 'fs';
import path from 'path';
import { logPrintEvent } from './print-log';
import { readConfig, writeConfig } from './config';
import {
  FormApplyResult,
  RestoreSnapshot,
  applyArgs,
  blockScope,
  needsRestore,
  parseHelperOutput,
  restoreArgs,
  skippedNotWindows,
} from './print-form-core';

/**
 * 我們願意等多久，同時也是傳給 helper 的寫入預算（`--budget-ms`）。
 * 驅動正常時 <200ms；網路印表機離線時不能讓使用者以為程式當掉。
 */
const TIMEOUT_MS = 3000;

/**
 * 預算到點之後再多給的緩衝，讓 helper 有機會把「我沒寫」這句話講完（`skipped-over-budget`）。
 * 講不完也沒關係——逾時之後**我們不殺它**，只是不再等；它的結果由 `onLate` 收尾。
 */
const GRACE_MS = 500;

/** 同時開著的檢視器視窗數。最後一個關掉才還原——否則會弄掉另一個視窗的紙。 */
let viewerCount = 0;

/** 還沒結束的 helper 行程數。逾時不殺 ⇒ 必須自己記得別再疊第二個驅動呼叫上去。 */
let inFlight = 0;

/**
 * ⚠️ `CEREMONY_PRINTFORM_EXE` 同時是**現場的緊急關閉開關**，不要「順手」改成只在檔案存在時採用：
 * 指到一個不存在的路徑 → 下面的 `existsSync` 為 false → `helper-missing` → 整段紙張預選跳過，
 * 列印本身完全不受影響（只是回到每次手動選紙）。這是 0x80010105 那類「驅動被我們寫壞」的客訴
 * 在不重新出版本的前提下唯一的止血手段。見 docs/design/infrastructure.md 列印排障段。
 *
 * 2026-08-10 起還有第二個開關，而且**使用者自己按得到**：報表預覽頁的「自動選紙」
 * （`printFormPreselect: false` 寫進 config.json），不必進環境變數。
 */
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

/** 失敗印表機黑名單：與 config.json 同目錄，刪掉即可重新啟用（現場的重置方式之一）。 */
function memoPath(): string {
  return path.join(app.getPath('appData'), 'Ceremony', 'print-form-printers.json');
}

// ───────────────────────── 對外 ─────────────────────────

/**
 * 開檢視器視窗**之前**呼叫：把預設印表機的紙張預選成該報表對應的驅動表單。
 * 絕不丟例外；失敗一律回一個帶 result 的物件，呼叫端只拿來寫 log 與決定視窗標題。
 */
export async function applyReportForm(reportType: string): Promise<FormApplyResult> {
  try {
    if (process.platform !== 'win32') return skippedNotWindows();
    if (!(await preselectEnabled())) return { result: 'skipped-disabled' };

    const exe = helperPath();
    if (!exe || !existsSync(exe)) return { result: 'helper-missing' };

    // 這台機器上曾經有一次逾時／helper 壞掉（我們不知道是哪台印表機）⇒ 整台停用，連 exe 都不啟動。
    const memo = await readMemo();
    if (memo.all) return { result: 'skipped-printer-blocked' };

    // 已經有 journal ＝ 我們動過那份共用 DEVMODE 而且還沒還原（多半是另一個檢視器視窗開著）。
    // 這時候**完全不呼叫 helper**：
    //   - 再預選一次會把前一個視窗正在等使用者按列印的紙換掉；
    //   - 而且第二次拍到的「原始值」其實是我們自己設的，寫回 journal 等於永久弄丟使用者的原始設定。
    //
    // 判準刻意用 journal 而不是 viewerCount：journal 才是「共用狀態被動過」的憑證。
    // 舊版用 `viewerCount === 0` 當寫 journal 的條件，於是「第一個視窗 not-found（沒寫入）、
    // 第二個視窗寫入成功」的組合會寫進 DEVMODE 卻不留還原紀錄——那張紙就永遠留在使用者的
    // Word/Excel 上了。本函式與 releaseReportForm 都跑在主行程的 event loop，兩次列印之間
    // 不會有第三方插進來改 journal。
    if (await readJournal()) return { result: 'skipped-viewer-open' };

    // 上一次的 helper 還卡在驅動裡（逾時之後我們不殺它）⇒ 不要再疊一個驅動呼叫上去。
    if (inFlight > 0) return { result: 'skipped-helper-busy' };

    const r = await run(exe, applyArgs(reportType, TIMEOUT_MS, blockedPrinters(memo)), onLateApply);

    await noteOutcome(r);
    if (needsRestore(r) && r.prev) await writeJournal(r.prev);

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

// ───────────────────────── 現場開關（UI 用） ─────────────────────────

export interface PrintFormState {
  /** 使用者有沒有把自動選紙關掉（config.json 的 printFormPreselect）。 */
  enabled: boolean;
  /** 我們自己因為逾時／helper 壞掉而整台停用。 */
  blockedAll: boolean;
  /** 被個別停用的印表機數量。 */
  blockedPrinters: number;
}

export async function printFormState(): Promise<PrintFormState> {
  const memo = await readMemo();
  return {
    enabled: await preselectEnabled(),
    blockedAll: memo.all !== undefined,
    blockedPrinters: blockedPrinters(memo).length,
  };
}

/**
 * 報表預覽頁那顆「自動選紙」開關。
 *
 * **打開的同時清空黑名單**：那顆開關在現場的真實語意是「我知道它壞過，現在再試一次」——
 * 留著黑名單會讓使用者按了開卻什麼都沒變，然後回報「開關沒作用」。
 * 關掉時不清，因為關掉之後黑名單本來就用不到，而重新打開時才是該重試的時機。
 */
export async function setPrintFormEnabled(enabled: boolean): Promise<PrintFormState> {
  const cfg = await readConfig();
  if (cfg) await writeConfig({ ...cfg, printFormPreselect: enabled });
  if (enabled) await fs.unlink(memoPath()).catch(() => undefined);

  // 沒有 config 就寫不進去，而回傳的狀態會是「預設值」＝按了等於沒按。
  // 這種按鈕靜靜地沒作用最難查，所以留一行證據（現場只會說「按了沒用」）。
  void logPrintEvent({
    event: 'form-preselect-toggled',
    enabled,
    ...(cfg ? {} : { error: 'no config; not persisted' }),
  });
  return printFormState();
}

// ───────────────────────── 內部 ─────────────────────────

async function preselectEnabled(): Promise<boolean> {
  // 沒有 config（首次啟動）或沒有這個欄位（既有安裝）一律視為開啟——這是預設行為，
  // 現場不會因為升級就突然少掉自動選紙。
  return (await readConfig())?.printFormPreselect !== false;
}

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
 *
 * ⚠️ 還原刻意**不看黑名單**：黑名單擋的是「主動去改使用者的設定」，而還原是把我們改過的東西
 * 放回去——不做才是留下永久副作用。這也是為什麼 restore 路徑同樣沒有 `--budget-ms`。
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

/**
 * 呼叫 helper。
 *
 * ⚠️ **刻意不帶 `timeout` / `killSignal`**：那會在逾時的瞬間 TerminateProcess，而 helper 這時多半正卡在
 * `DocumentProperties` 或 `PTConvertDevModeToPrintTicket` 裡面——把 COM client 殺在半路，對方
 * （v4 驅動的設定模組，多半跑在 PrintIsolationHost.exe）留在什麼狀態不由我們決定，之後
 * `PrintDlgEx` 去問同一個 provider 就可能永遠等不到回應。**那正是 2026-08-10 客訴的卡死畫面。**
 * 逾時只代表「我們不等了」，不代表「它必須立刻死」。
 *
 * 代價是它可能在我們 resolve 之後才寫完 —— 由兩道防線接住：
 *   1. helper 自己在 `SetPrinter` 之前檢查 `--budget-ms`，超過就不寫（`skipped-over-budget`）；
 *   2. 真的還是寫成功了（剛好卡在檢查與寫入之間），`onLate` 會補記還原 journal。
 *
 * @param onLate 逾時之後才回來的結果。**不得**用來改 UI，那個視窗早就開了。
 */
function run(
  exe: string,
  args: string[],
  onLate?: (r: FormApplyResult) => void,
): Promise<FormApplyResult> {
  return new Promise((resolve) => {
    let settled = false;
    const done = (r: FormApplyResult): void => {
      if (settled) {
        onLate?.(r);
        return;
      }
      settled = true;
      resolve(r);
    };

    const timer = setTimeout(() => done({ result: 'helper-timeout' }), TIMEOUT_MS + GRACE_MS);
    timer.unref?.();

    inFlight++;
    let counted = true;
    const release = (): void => {
      if (counted) {
        counted = false;
        inFlight--;
      }
    };

    try {
      execFile(
        exe,
        args,
        { windowsHide: true, encoding: 'utf8', maxBuffer: 64 * 1024 },
        (err, stdout) => {
          release();
          clearTimeout(timer);
          if (err && !stdout) {
            done({ result: 'helper-error', error: err.message });
            return;
          }
          done(parseHelperOutput(stdout));
        },
      );
    } catch (e) {
      release();
      clearTimeout(timer);
      done({ result: 'helper-error', error: (e as Error).message });
    }
  });
}

/**
 * 逾時之後才回來的 apply 結果。只做一件事：如果它**真的寫進去了**，補一份還原 journal，
 * 否則那張紙會永久留在使用者的列印喜好設定裡（不變式二）。
 *
 * 這裡不寫黑名單：逾時當下 `noteOutcome` 已經記了 `all`，而遲到的結果多半是成功的
 * （慢，不是壞），拿它去覆蓋反而會把停用洗掉。
 */
function onLateApply(r: FormApplyResult): void {
  void (async () => {
    if (!needsRestore(r) || !r.prev) return;
    if (await readJournal()) return; // 已經有人記了，別覆蓋
    await writeJournal(r.prev);
    void logPrintEvent({ event: 'form-late-write', formResult: r.result });
  })();
}

// ───────────────────────── 失敗印表機黑名單 ─────────────────────────

interface MemoEntry {
  result: string;
  ts: string;
  appVersion?: string;
}

interface PrinterMemo {
  /** 整台機器停用（逾時／helper 壞掉時我們不知道是哪台印表機）。 */
  all?: MemoEntry;
  /** printerHash → 為什麼被停用。 */
  printers?: Record<string, MemoEntry>;
}

function blockedPrinters(memo: PrinterMemo): string[] {
  return Object.keys(memo.printers ?? {});
}

/**
 * 記帳。判斷規則在 `blockScope`（純函式、測得到），這裡只負責落檔。
 *
 * 寫檔失敗不影響列印，但會讓下一次再碰一次那台驅動——所以留一行 log，
 * 現場「每次都卡」而黑名單卻是空的時候查得下去。
 */
async function noteOutcome(r: FormApplyResult): Promise<void> {
  const scope = blockScope(r);
  if (scope === 'none') return;

  const entry: MemoEntry = {
    result: r.result,
    ts: new Date().toISOString(),
    appVersion: app.getVersion(),
  };
  const memo = await readMemo();

  if (scope === 'all') memo.all = entry;
  else memo.printers = { ...memo.printers, [r.printerHash!]: entry };

  const ok = await writeMemo(memo);
  void logPrintEvent({
    event: 'form-printer-blocked',
    scope,
    formResult: r.result,
    ...(r.printerHash ? { printerHash: r.printerHash } : {}),
    ...(ok ? {} : { error: 'memo write failed' }),
  });
}

async function readMemo(): Promise<PrinterMemo> {
  try {
    const raw = JSON.parse(await fs.readFile(memoPath(), 'utf-8')) as PrinterMemo;
    // 壞檔／被手動編歪一律當成空的：黑名單讀不出來只是少擋一次，讀出垃圾卻會把好印表機也擋掉。
    const printers =
      raw.printers && typeof raw.printers === 'object' && !Array.isArray(raw.printers)
        ? raw.printers
        : undefined;
    return { ...(raw.all ? { all: raw.all } : {}), ...(printers ? { printers } : {}) };
  } catch {
    return {};
  }
}

async function writeMemo(memo: PrinterMemo): Promise<boolean> {
  try {
    const file = memoPath();
    await fs.mkdir(path.dirname(file), { recursive: true });
    await fs.writeFile(file, JSON.stringify({ v: 1, ...memo }, null, 2), 'utf-8');
    return true;
  } catch {
    return false;
  }
}

// ───────────────────────── 還原 journal ─────────────────────────

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
