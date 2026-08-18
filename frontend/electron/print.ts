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
// 紙張 / 縮放 / 方向參數（那正是 v2.3.7/v2.3.8 客訴的來源）。
//
// 唯一的例外是 print-form.ts：開視窗前把**驅動的每使用者預設紙張**選成該報表對應的自訂表單。
// 那不是送印參數（我們沒有多傳任何東西給 Chromium），而是 PrintDlgEx 開啟時的初值，
// 也正是舊系統唯一會主動設定的那一格。決策見 docs/blueprints/print-channel-electron.md 決策 9。
import { BrowserWindow } from 'electron';
import { promises as fs } from 'fs';
import os from 'os';
import path from 'path';
import crypto from 'crypto';
import { streamApiToFile } from './api-stream';
import { logPrintEvent, sweepOldLogs } from './print-log';
import {
  applyReportForm,
  dialogPathEnabled,
  noteViewerOpened,
  printFormState,
  releaseReportForm,
} from './print-form';
import { logFields, viewerTitle } from './print-form-core';
import { printViaDialog } from './print-dialog';
import { printDialogFinalMessage, printDialogMessage } from './print-dialog-core';
import { viewerPageHtml } from './viewer-page';
import { returnFocusOnClose } from './window-focus';

export interface PrintResult {
  ok: boolean;
  error?: string;
}

/**
 * 開著的決策 11 預覽視窗。key 是 webContents id——preload 那條 IPC **不帶任何參數**，
 * 要印哪一份由主行程自己記著（暴露面壓到最小，頁面內容影響不了它）。
 */
const viewers = new Map<number, { reportType: string; pdfPath: string; win: BrowserWindow }>();

/**
 * 預覽頁那顆「列印」鈕。回傳時代表**對話框已經在螢幕上**，不是印完了（決策 8）。
 *
 * 同一個視窗可以按很多次：取消之後重印、卡紙續印、換一台印表機重印全部走這裡，
 * 而且是對**同一份既有 temp PDF** 再叫一次 helper，不重跑渲染。
 */
export async function printFromViewer(webContentsId: number): Promise<PrintResult> {
  const v = viewers.get(webContentsId);
  if (!v) return { ok: false, error: '找不到這個預覽視窗' };

  const r = await printViaDialog({
    reportType: v.reportType,
    pdfPath: v.pdfPath,
    owner: v.win,
    // 「自動選紙」關掉時，helper 就不去改我們手上那份 DEVMODE 的紙張。
    // 使用者可見語意完全不變，只是底層從「不寫入每使用者預設」變成「不改我們自己那份」。
    noForm: !(await printFormState()).enabled,
    jobName: `寶覺寺法會報名系統 — ${v.reportType}`,
    // 送印結束後把結果推回預覽頁（見 printDialogFinalMessage）。這是純顯示，
    // **不碰按鈕狀態**——UI 早在對話框出現時就放行了（決策 8）。
    onFinal: (outcome) => {
      const msg = printDialogFinalMessage(outcome);
      // 使用者可能在 spooler 還在忙的時候就把預覽關掉了：那不是錯誤，安靜跳過。
      if (msg && !v.win.isDestroyed()) v.win.webContents.send('ceremony:viewerPrintResult', msg);
    },
  });

  const message = printDialogMessage(r.result);
  return message ? { ok: false, error: message } : { ok: true };
}

/** 預覽頁那顆「關閉」鈕。 */
export function closeViewer(webContentsId: number): void {
  viewers.get(webContentsId)?.win.close();
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
 *   但 parent 只管「壓在上面」，**關掉之後焦點不會自動回來**，那條要自己接（`returnFocusOnClose`）。
 * - 紙張預選（`applyReportForm`）必須在**開窗之前**完成：使用者有可能一開窗就按 🖨。
 *   它是 best-effort，任何結果都不影響回傳的 ok——helper 失敗只會讓紙張回到驅動預設。
 */
async function showViewerWindow(
  reportType: string,
  pdfPath: string,
  parent: BrowserWindow | null,
  log: { via: string; bytes: number; pageSizeHeader?: string | null; signupCount?: string | null },
): Promise<PrintResult> {
  // 決策 11 的路徑自己帶 DEVMODE 進對話框 ⇒ 這裡一定要拿到 skipped-dialog-path（互斥不變式）。
  const dialogPath = await dialogPathEnabled().catch(() => false);
  const form = await applyReportForm(reportType).catch(() => ({ result: 'helper-error' }) as const);

  try {
    const win = new BrowserWindow({
      width: 900,
      height: 1000,
      title: viewerTitle(form),
      autoHideMenuBar: true,
      ...(parent ? { parent } : {}),
      webPreferences: {
        plugins: true,
        contextIsolation: true,
        nodeIntegration: false,
        // 新路徑的預覽頁要有我們自己的「列印」鈕 ⇒ 需要一條最小的 IPC 橋。
        ...(dialogPath ? { preload: path.join(__dirname, 'viewer-preload.js') } : {}),
      },
    });

    const htmlPath = dialogPath ? `${pdfPath}.html` : null;
    // `closed` 當下 win / win.webContents 都已銷毀，碰到就是 `Object has been destroyed`
    // （未捕捉 ⇒ 主行程跳錯誤對話框，temp 檔與紙張設定也一起漏掉還原）。id 先抄下來。
    const wcId = win.webContents.id;

    // temp 檔與紙張設定的生命週期一模一樣，共用同一個 hook 最不容易失聯。
    win.on('closed', () => {
      viewers.delete(wcId);
      void safeUnlink(pdfPath);
      if (htmlPath) void safeUnlink(htmlPath);
      void releaseReportForm();
    });
    // 關掉預覽後主視窗要回到前面，否則會沉到其他應用程式後面（見 window-focus.ts）。
    returnFocusOnClose(win, parent);

    if (htmlPath) {
      // 舊版列印對話框沒有預覽區，所以預覽必須由我們自己出（＝舊系統的 PrintPreviewDialog）。
      await fs.writeFile(htmlPath, viewerPageHtml(pdfPath, viewerTitle(form)), 'utf8');
      viewers.set(wcId, { reportType, pdfPath, win });
      await win.loadFile(htmlPath);
    } else {
      await win.loadFile(pdfPath);
    }

    noteViewerOpened();
    win.focus();

    void logPrintEvent({
      reportType,
      ...log,
      ...logFields(form),
      ...(dialogPath ? { path: 'dialog' } : {}),
      result: 'opened',
    });
    return { ok: true };
  } catch (e) {
    void safeUnlink(pdfPath);
    void releaseReportForm(); // 開窗失敗也要把驅動設定還原回去
    const error = (e as Error).message;
    void logPrintEvent({ reportType, ...log, ...logFields(form), result: 'error', error });
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
