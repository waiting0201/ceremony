// Electron 主行程列印通道：把後端產的 PDF 開在可見的 PDF 檢視器視窗，讓使用者走 Windows 原生列印。
//
// 為什麼是這個形狀（2026-08-02 起）：**逐位元對齊舊系統**。
// 舊系統 SignupForm.cs 的流程是 LocalReport.Render → PrintPreviewDialog（預覽視窗）
// → PrintDialog（Windows 原生）→ printDocument.Print()——程式只負責產內容與預覽，
// 印表機 / 份數 / 紙張 / 方向全部由原生對話框決定，不記任何列印偏好。
//
// 這裡的對應：後端 QuestPDF 產 PDF → 本檔開 Chromium PDF 檢視器視窗（＝ PrintPreviewDialog）
// → 使用者按工具列的列印鈕 → Electron build 不含 print preview WebUI（enable_print_preview=false）
// → 落到 Windows 原生 PrintDlgEx（**有「內容」按鈕、有「頁面範圍」**）。
//
// ⚠️ 紙張尺寸不在這一層。舊系統「RDLC 只是排版，真正尺寸由 DeviceInfo 決定」，新系統對應的是
// 後端 ReportPageSizes.cs → QuestPDF page.Size()：**產 PDF 那一刻就定案**。送印端不指定任何
// 紙張 / 縮放 / 方向參數（那正是 v2.3.7/v2.3.8 客訴的來源）。現場要做的是在驅動裡建對的自訂紙張。
// 決策見 docs/blueprints/print-channel-electron.md。
import { BrowserWindow } from 'electron';
import { promises as fs } from 'fs';
import os from 'os';
import path from 'path';
import crypto from 'crypto';
import { streamApiToFile } from './api-stream';
import { logPrintEvent, sweepOldLogs } from './print-log';

export interface PrintResult {
  ok: boolean;
  error?: string;
}

/**
 * 從 sidecar 串流取一份報表 PDF，開在檢視器視窗。
 *
 * PDF **完全不經過 renderer**：net.request 直接串流落檔，所以一次印一千筆（合併成一份數百 MB
 * 的 PDF）也不會有 renderer + main 各一份 bytes 的記憶體問題。這是既有 streamApiToFile 的
 * 第二個使用者（第一個是備份下載）。
 *
 * @param apiPath 例如 `/api/v1/reports/datacard?signupId=…` 或 `/api/v1/reports/batch/jobs/{id}/file`
 */
export async function openReportInViewer(
  reportType: string,
  apiBase: string,
  apiPath: string,
  token: string,
  parent: BrowserWindow | null,
): Promise<PrintResult> {
  const pdfPath = tempPdfPath(reportType);

  const r = await streamApiToFile(apiBase, apiPath, token, pdfPath);
  if (!r.ok) {
    void safeUnlink(pdfPath);
    void logPrintEvent({ reportType, via: 'stream', result: 'error', error: r.error });
    return { ok: false, error: r.error ?? '取得報表失敗' };
  }

  const bytes = await fileSize(pdfPath);
  return showViewerWindow(reportType, pdfPath, parent, {
    via: 'stream',
    bytes,
    pageSizeHeader: r.headers['x-report-page-size'] ?? null,
    signupCount: r.headers['x-signup-count'] ?? null,
  });
}

/**
 * 開檢視器視窗印 renderer 手上既有的 PDF bytes（報表預覽頁：blob 已經在前端）。
 * 只用於單筆／已在畫面上的 PDF，大小可控，IPC 複製成本可忽略。
 */
export async function openPdfInViewer(
  reportType: string,
  bytes: Uint8Array,
  parent: BrowserWindow | null,
): Promise<PrintResult> {
  const pdfPath = tempPdfPath(reportType);
  try {
    await fs.mkdir(path.dirname(pdfPath), { recursive: true });
    await fs.writeFile(pdfPath, bytes);
  } catch (e) {
    void safeUnlink(pdfPath);
    return { ok: false, error: (e as Error).message };
  }

  return showViewerWindow(reportType, pdfPath, parent, { via: 'buffer', bytes: bytes.length });
}

/**
 * 把 PDF 開在可見的 Chromium PDF 檢視器視窗，然後什麼都不做——使用者按工具列的列印鈕。
 *
 * 幾個要點：
 * - `plugins: true` 是必要條件：沒開的話 Chromium 內建 PDF viewer 不啟用，視窗是空白的
 *   （或整份被當成下載）。見 docs/gotchas.md。
 * - 刻意 **不** 加 `#toolbar=0`：那顆工具列列印鈕正是整條通道的入口。
 * - temp 檔在 `closed` 才刪：提早刪會讓使用者按列印時檔案已不在。
 * - 視窗 parent 綁主視窗：原生列印對話框才不會躲到主視窗後面（看起來像「按了沒反應」）。
 */
async function showViewerWindow(
  reportType: string,
  pdfPath: string,
  parent: BrowserWindow | null,
  log: { via: string; bytes: number; pageSizeHeader?: string | null; signupCount?: string | null },
): Promise<PrintResult> {
  try {
    const win = new BrowserWindow({
      width: 900,
      height: 1000,
      title: '列印預覽 — 請按工具列的列印鈕',
      autoHideMenuBar: true,
      ...(parent ? { parent } : {}),
      webPreferences: { plugins: true, contextIsolation: true, nodeIntegration: false },
    });

    win.on('closed', () => void safeUnlink(pdfPath));
    await win.loadFile(pdfPath);
    win.focus();

    void logPrintEvent({ reportType, ...log, result: 'opened' });
    return { ok: true };
  } catch (e) {
    void safeUnlink(pdfPath);
    const error = (e as Error).message;
    void logPrintEvent({ reportType, ...log, result: 'error', error });
    return { ok: false, error };
  }
}

// ───────────────────────── temp 檔管理 ─────────────────────────

function tempDir(): string {
  return path.join(os.tmpdir(), 'ceremony-print');
}

function tempPdfPath(reportType: string): string {
  return path.join(tempDir(), `${reportType}-${crypto.randomUUID()}.pdf`);
}

async function fileSize(p: string): Promise<number> {
  try {
    return (await fs.stat(p)).size;
  } catch {
    return 0;
  }
}

async function safeUnlink(p: string): Promise<void> {
  try {
    await fs.unlink(p);
  } catch {
    // 檔案可能已被清掉，或仍被檢視器持有 → 交給 sweepTempDir 下次處理
  }
}

/**
 * 清理殘留的列印暫存檔（app 崩潰時會留下）與過期的診斷紀錄。
 * 啟動與離開各掃一次；temp 檔只刪 1 小時前的，避免誤刪另一個視窗正開著的檔。
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
