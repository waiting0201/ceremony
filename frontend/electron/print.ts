// Electron 主行程列印通道：把後端產的 PDF 直接送到印表機，紙張 / 邊界 / 縮放由程式指定。
//
// 為什麼要有這條路：舊做法是 renderer 做 window.open(blob:...) 就結束，紙張與縮放全交給
// 不知名的 PDF 檢視器與驅動 → 同一份 PDF 在三台機器有三種結果（有的正常、有的要手動調、有的
// 因為 plugins 未開而變成下載，看起來像「讀不到印表機」）。詳見 docs/gotchas.md。
//
// 為什麼是 silent:true + 自建對話框：Electron 的 print({silent:false}) 在 Windows 走原生 PrintDlgEx，
// 建立對話框時 hDevMode/hDevNames 為 null → 初值來自驅動預設，我們傳的 pageSize/deviceName
// 沒有注入點（官方型別註解也只對 silent:true 保證設定生效）。要「預設值一定正確」只能自己畫對話框，
// 再用 silent:true 把完整設定送出去。決策見 docs/blueprints/print-channel-electron.md。
import { BrowserWindow } from 'electron';
import { promises as fs } from 'fs';
import os from 'os';
import path from 'path';
import crypto from 'crypto';
import { streamApiToFile } from './api-stream';
import { resolvePageSize, PageSizeMicrons } from './paper';
import { readPrintSettings, ScaleMode } from './print-config';

export interface PrintResult {
  ok: boolean;
  canceled?: boolean;
  error?: string;
}

export interface PrintOverrides {
  deviceName?: string;
  copies?: number;
  scaleMode?: ScaleMode;
}

/** 單筆列印：主行程自己去 sidecar 抓 PDF（不經 renderer，避免大檔在 IPC 複製一份）。 */
export async function printReport(
  apiBase: string,
  token: string,
  reportType: string,
  apiPath: string,
  overrides: PrintOverrides,
): Promise<PrintResult> {
  const pdfPath = tempPdfPath(reportType);
  try {
    const r = await streamApiToFile(apiBase, apiPath, token, pdfPath);
    if (!r.ok) return { ok: false, error: r.error ?? '取得報表失敗' };
    return await printResolved(reportType, pdfPath, r.headers['x-report-page-size'], overrides);
  } finally {
    void safeUnlink(pdfPath);
  }
}

/**
 * 批次列印：renderer 先跑完 job（進度 / 取消 UI 保留在前端），只把 jobId 交給 main 取檔。
 * 注意 GET batch/jobs/{id}/file 是 one-shot（取走即釋放），renderer 不能先取一次再叫 main 取第二次。
 */
export async function printBatchJob(
  apiBase: string,
  token: string,
  reportType: string,
  jobId: string,
  overrides: PrintOverrides,
): Promise<PrintResult> {
  return printReport(apiBase, token, reportType, `/reports/batch/jobs/${jobId}/file`, overrides);
}

/** 列印 renderer 手上既有的 PDF bytes（報表預覽頁專用：blob 已在前端且 job 已消耗）。 */
export async function printPdfBuffer(
  reportType: string,
  bytes: Uint8Array,
  overrides: PrintOverrides,
): Promise<PrintResult> {
  const pdfPath = tempPdfPath(reportType);
  try {
    await fs.mkdir(path.dirname(pdfPath), { recursive: true });
    await fs.writeFile(pdfPath, bytes);
    return await printResolved(reportType, pdfPath, null, overrides);
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
): Promise<PrintResult> {
  const saved = (await readPrintSettings()).byReportType[reportType] ?? {};
  const deviceName = overrides.deviceName ?? saved.deviceName;
  const copies = overrides.copies ?? saved.copies ?? 1;
  const scaleMode: ScaleMode = overrides.scaleMode ?? saved.scaleMode ?? 'actual';

  const { size, source } = resolvePageSize(reportType, pageSizeHeader);
  if (source !== 'header') {
    // 走到這裡代表 sidecar 沒帶 X-Report-Page-Size（版本不合）或 report type 未知。
    // 不是致命錯誤（fallback 表通常是對的），但值得留痕：印歪時這行 log 是第一個線索。
    console.warn(`[print] ${reportType} 未取得 X-Report-Page-Size，改用 ${source}`);
  }

  return printPdfFile(pdfPath, { deviceName, copies, scaleMode, pageSize: size });
}

interface PdfPrintOptions {
  deviceName?: string;
  copies: number;
  scaleMode: ScaleMode;
  pageSize: PageSizeMicrons | null;
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
export function printPdfFile(pdfPath: string, opts: PdfPrintOptions): Promise<PrintResult> {
  return new Promise<PrintResult>((resolve) => {
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
    const finish = (r: PrintResult) => {
      if (settled) return;
      settled = true;
      clearTimeout(guard);
      if (!win.isDestroyed()) win.destroy();
      resolve(r);
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
        const r = await printOnce(win, opts);
        if (r.ok || r.canceled) return finish(r);
        if (attempt === MAX_ATTEMPTS || settled) return finish(r);
        await delay(500);
      }
    };

    run().catch((e: Error) => finish({ ok: false, error: e.message }));
  });
}

function printOnce(win: BrowserWindow, opts: PdfPrintOptions): Promise<PrintResult> {
  return new Promise<PrintResult>((resolve) => {
    // actual：頁面尺寸 = 實體紙張尺寸、邊界 0、100% → 1:1 對位（座標表才有意義）。
    // fit：不指定 pageSize，用驅動預設紙張 + 可列印範圍，Chromium 會把 PDF 縮到符合——
    //      這是舊系統 DrawImage(PageBounds) 拉伸行為的等價替代（Electron 未暴露 fitToPage 選項）。
    const base = {
      silent: true,
      printBackground: true,
      copies: opts.copies,
      ...(opts.deviceName ? { deviceName: opts.deviceName } : {}),
    } as const;

    const options =
      opts.scaleMode === 'fit'
        ? { ...base, margins: { marginType: 'printableArea' as const } }
        : {
            ...base,
            margins: { marginType: 'none' as const },
            scaleFactor: 100,
            ...(opts.pageSize ? { pageSize: opts.pageSize } : {}),
          };

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
 * 清理殘留的列印暫存檔（app 崩潰 / spooler 卡住時會留下）。
 * 啟動與離開各掃一次；只刪 1 小時前的，避免誤刪另一個 instance 正在送印的檔。
 */
export async function sweepTempDir(): Promise<void> {
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
