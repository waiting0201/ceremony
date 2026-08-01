// Electron 主行程列印通道：把後端產的 PDF 送到指定印表機。
//
// 為什麼要有這條路：更早的做法是 renderer 做 window.open(blob:...) 就結束，連「印到哪一台」
// 都無法指定，而且 plugins 未開時會變成下載，看起來像「讀不到印表機」。詳見 docs/gotchas.md。
//
// 為什麼是 silent:true + 自建對話框：Electron 的 print({silent:false}) 在 Windows 走原生 PrintDlgEx，
// 建立對話框時 hDevMode/hDevNames 為 null → 我們傳的 deviceName 沒有注入點（官方型別註解也只對
// silent:true 保證設定生效）；而且大量列印分段會每段跳一次。所以自己畫對話框（印表機 / 份數 /
// 預覽）再用 silent:true 送出。
//
// ⚠️ 送印的**預設**是「什麼都不指定」——紙張 / 邊界 / 縮放 / 方向全交回驅動 DEVMODE。
// 對話框把它攤成 scale / orientation / paper 三個獨立軸讓使用者可以自救，預設全部 'driver'。
// 見 print-options.ts 的說明與 docs/blueprints/print-channel-electron.md。
// 要調驅動本身的紙匣 / 自訂紙張，走對話框的「用 PDF 檢視器列印」（openPdfInViewerWindow）——
// 那條路會落到原生對話框，有「內容」按鈕。
import { BrowserWindow } from 'electron';
import { promises as fs } from 'fs';
import os from 'os';
import path from 'path';
import crypto from 'crypto';
import { resolvePageSize } from './paper';
import { buildPrintOptions } from './print-options';
import type { BuiltPrintOptions, PrintModes } from './print-options';
import { logPrintEvent, sweepOldLogs } from './print-log';
import { readPrintSettings } from './print-config';

export interface PrintResult {
  ok: boolean;
  canceled?: boolean;
  error?: string;
}

export interface PrintOverrides extends PrintModes {
  deviceName?: string;
  copies?: number;
}

// 曾經有一條「main 自己 net.request 去 sidecar 取 PDF 再送印」的路徑（printReport /
// printBatchJob），用來避免數百 MB 的批次成品在 IPC 複製一份。大量列印改成前端分段之後，
// 每次送印最多一段（200 筆 ≈ 27 MB），IPC 傳 bytes 已經沒有成本問題，那條路徑因此整條移除——
// 留著就是永遠不會被執行、也不會被測到的死碼。見 docs/blueprints/chunked-batch-printing.md

/**
 * 列印 renderer 手上既有的 PDF bytes（預覽用：blob 已在前端，或 job 的 /file 已被取走）。
 *
 * pageSizeHeader 由 renderer 從 X-Report-Page-Size 讀出後傳進來。送印本身不再使用它
 * （紙張交回驅動），但它會被記進診斷紀錄——「驅動有沒有選對紙」的第一個線索就是那裡。
 */
export async function printPdfBuffer(
  reportType: string,
  bytes: Uint8Array,
  overrides: PrintOverrides,
  pageSizeHeader?: string | null,
): Promise<PrintResult> {
  const pdfPath = tempPdfPath(reportType);
  try {
    await fs.mkdir(path.dirname(pdfPath), { recursive: true });
    await fs.writeFile(pdfPath, bytes);
    return await printResolved(
      reportType,
      pdfPath,
      pageSizeHeader ?? null,
      overrides,
      bytes.length,
    );
  } catch (e) {
    return { ok: false, error: (e as Error).message };
  } finally {
    void safeUnlink(pdfPath);
  }
}

async function printResolved(
  reportType: string,
  pdfPath: string,
  pageSizeHeader: string | null | undefined,
  overrides: PrintOverrides,
  bytes: number,
): Promise<PrintResult> {
  const saved = (await readPrintSettings()).byReportType[reportType] ?? {};
  const deviceName = overrides.deviceName ?? saved.deviceName;
  const copies = overrides.copies ?? saved.copies ?? 1;
  const scale = overrides.scale ?? saved.scale ?? 'driver';
  const orientation = overrides.orientation ?? saved.orientation ?? 'driver';
  const paper = overrides.paper ?? saved.paper ?? 'driver';

  // 只有 paper:'report' 會真的用到 size；其餘情況仍解析一次是為了留痕——
  // source !== 'header' 代表 sidecar 版本不合或 report type 未知，
  // 而印歪時第一個要問的就是「驅動當時用的是哪張紙」。
  const { size, source } = resolvePageSize(reportType, pageSizeHeader);

  const options = buildPrintOptions({
    copies,
    deviceName,
    scale,
    orientation,
    paper,
    pageSize: size,
  });
  const startedAt = Date.now();
  const { result, attempts } = await printPdfFile(pdfPath, options);

  void logPrintEvent({
    reportType,
    deviceName,
    modes: { scale, orientation, paper },
    pageSizeSource: source,
    pageSizeMicrons: size,
    pageSizeHeaderRaw: pageSizeHeader ?? null,
    options,
    bytes,
    attempts,
    durationMs: Date.now() - startedAt,
    result: result.ok ? 'ok' : result.canceled ? 'canceled' : 'error',
    error: result.error,
  });

  return result;
}

const LOAD_TIMEOUT_MS = 30_000;
const PRINT_TIMEOUT_MS = 10 * 60_000;
const MAX_ATTEMPTS = 3;

/**
 * 用隱藏視窗載入 PDF 再送印。
 *
 * 幾個踩過的點：
 * - 必須 plugins:true，否則 Chromium 內建 PDF viewer 不啟用，載進去是空白（或直接被當成下載）。
 * - backgroundThrottling:false：隱藏視窗會被 Chromium 節流，沒關掉可能印出白紙。
 *   若某些機器仍印白紙，退路是改成畫面外顯示（setPosition(-20000,-20000) + showInactive()）。
 * - did-finish-load 早於 PDF plugin 渲染完成（OOPIF 是另一個 frame），要再等子 frame 掛上。
 * - 一定要等 print 的 callback 才關窗：callback 代表 job 已交給 spooler，提早關窗會殺掉列印。
 */
function printPdfFile(
  pdfPath: string,
  options: BuiltPrintOptions,
): Promise<{ result: PrintResult; attempts: number }> {
  return new Promise((resolve) => {
    const win = new BrowserWindow({
      show: false,
      webPreferences: {
        plugins: true,
        contextIsolation: true,
        nodeIntegration: false,
        backgroundThrottling: false,
      },
    });

    let settled = false;
    let attempts = 0;
    const finish = (result: PrintResult) => {
      if (settled) return;
      settled = true;
      clearTimeout(guard);
      if (!win.isDestroyed()) win.destroy();
      resolve({ result, attempts });
    };
    const guard = setTimeout(
      () => finish({ ok: false, error: '列印逾時（印表機無回應）' }),
      PRINT_TIMEOUT_MS,
    );

    const run = async () => {
      await win.loadFile(pdfPath);
      await waitForPdfFrame(win);
      if (settled) return;

      for (let attempt = 1; attempt <= MAX_ATTEMPTS; attempt++) {
        attempts = attempt;
        const r = await printOnce(win, options);
        if (r.ok || r.canceled) return finish(r);
        if (attempt === MAX_ATTEMPTS || settled) return finish(r);
        await delay(500);
      }
    };

    run().catch((e: Error) => finish({ ok: false, error: e.message }));
  });
}

function printOnce(win: BrowserWindow, options: BuiltPrintOptions): Promise<PrintResult> {
  return new Promise<PrintResult>((resolve) => {
    win.webContents.print(options, (success, failureReason) => {
      if (success) return resolve({ ok: true });
      const reason = failureReason ?? '';
      if (/cancel/i.test(reason)) return resolve({ ok: false, canceled: true });
      resolve({ ok: false, error: reason || '列印失敗（印表機未回應或設定不支援）' });
    });
  });
}

/**
 * 等 PDF plugin 的子 frame 掛上。did-finish-load 只保證外層 document 載完，
 * PDF 內容在另一個 frame 非同步渲染；沒等到就 print 會印出白紙。
 */
async function waitForPdfFrame(win: BrowserWindow): Promise<void> {
  const deadline = Date.now() + LOAD_TIMEOUT_MS;
  while (Date.now() < deadline) {
    if (win.isDestroyed()) return;
    try {
      if (win.webContents.mainFrame.framesInSubtree.length > 1) break;
    } catch {
      // frame 正在換頁 → 下一輪再看
    }
    await delay(100);
  }
  // plugin 掛上後仍需一小段時間完成首次 paint；實測 250ms 足夠且不影響體感。
  await delay(250);
}

/**
 * 把 PDF 開在一個可見的 Chromium PDF 檢視器視窗，然後什麼都不做。
 *
 * 這是 v2.3.6 以前那條路，逐位元：使用者按檢視器工具列的列印鈕 → Electron build 不含
 * print preview WebUI → 落到 Windows 原生 PrintDlgEx（**有「內容」按鈕**）。
 *
 * 三個用途：
 * 1. 現場對照組的基準線產生器——不必為了比對而回裝舊版
 * 2. 萬一新的送印基準在某台機器仍不對，這是逃生門
 * 3. 在 Phase 2 的「印表機內容」按鈕做出來之前，這是唯一能進驅動設定的路
 *
 * 刻意 **不** 加 `#toolbar=0`：那顆工具列列印鈕正是本功能的重點（列印對話框裡的預覽 iframe
 * 才需要藏它，避免使用者誤按而繞過通道）。
 * temp 檔在 closed 才刪——提早刪會讓使用者按列印時檔案已不在。
 */
export async function openPdfInViewerWindow(
  reportType: string,
  bytes: Uint8Array,
  parent: BrowserWindow | null,
): Promise<PrintResult> {
  const pdfPath = tempPdfPath(reportType);
  try {
    await fs.mkdir(path.dirname(pdfPath), { recursive: true });
    await fs.writeFile(pdfPath, bytes);

    const win = new BrowserWindow({
      width: 900,
      height: 1000,
      title: '列印預覽（請用工具列的列印鈕）',
      autoHideMenuBar: true,
      ...(parent ? { parent } : {}),
      webPreferences: { plugins: true, contextIsolation: true, nodeIntegration: false },
    });
    win.on('closed', () => void safeUnlink(pdfPath));
    await win.loadFile(pdfPath);

    void logPrintEvent({ reportType, via: 'viewer-window', bytes: bytes.length, result: 'opened' });
    return { ok: true };
  } catch (e) {
    void safeUnlink(pdfPath);
    return { ok: false, error: (e as Error).message };
  }
}

/** 列印設定 UI 用的印表機清單。需要一個既有 webContents（getPrintersAsync 掛在 webContents 上）。 */
export async function listPrinters(win: BrowserWindow | null) {
  if (!win || win.isDestroyed()) return [];
  const printers = await win.webContents.getPrintersAsync();
  return printers.map((p) => ({
    name: p.name,
    displayName: p.displayName || p.name,
    isDefault: p.isDefault,
    status: p.status,
  }));
}

// ───────────────────────── temp 檔管理 ─────────────────────────

function tempDir(): string {
  return path.join(os.tmpdir(), 'ceremony-print');
}

function tempPdfPath(reportType: string): string {
  return path.join(tempDir(), `${reportType}-${crypto.randomUUID()}.pdf`);
}

async function safeUnlink(p: string): Promise<void> {
  try {
    await fs.unlink(p);
  } catch {
    // 檔案可能已被清掉，或仍被 spooler 持有 → 交給 sweepTempDir 下次處理
  }
}

/**
 * 清理殘留的列印暫存檔（app 崩潰 / spooler 卡住時會留下）與過期的診斷紀錄。
 * 啟動與離開各掃一次；temp 檔只刪 1 小時前的，避免誤刪另一個 instance 正在送印的檔。
 */
export async function sweepTempDir(): Promise<void> {
  void sweepOldLogs();
  try {
    const dir = tempDir();
    const cutoff = Date.now() - 60 * 60_000;
    for (const name of await fs.readdir(dir)) {
      const full = path.join(dir, name);
      const stat = await fs.stat(full).catch(() => null);
      if (stat && stat.mtimeMs < cutoff) await safeUnlink(full);
    }
  } catch {
    // 目錄不存在 = 沒東西要清
  }
}

function delay(ms: number): Promise<void> {
  return new Promise((r) => setTimeout(r, ms));
}
