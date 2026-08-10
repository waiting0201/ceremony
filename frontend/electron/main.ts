// Electron main：生命週期 + prereq 偵測 + sidecar 啟動 + 首次設定導流 + IPC handlers。
// 啟動流程：偵測 prereq → 讀 config → (prereq ok && 有 config) 則自動連線載入主程式，
// 否則載入殼讓 renderer 走 /prereq 或 /setup。詳見 docs/design/infrastructure.md。
import { app, BrowserWindow, ipcMain, shell, Menu } from 'electron';
import path from 'path';
import fs from 'fs';
import { spawn } from 'child_process';
import { readConfig, writeConfig, readDefaultConfig, CeremonyConfig } from './config';
import { detectPrereqs, PrereqReport } from './prereq';
import { startSidecar, stopSidecar } from './sidecar';
import { downloadBackup } from './download';
import { openPdfInViewer, openReportInViewer, sweepTempDir } from './print';
import { printLogPath } from './print-log';
import {
  printFormState,
  recoverPendingFormRestore,
  releaseReportForm,
  setPrintFormEnabled,
} from './print-form';
import { returnFocusOnClose } from './window-focus';

let mainWindow: BrowserWindow | null = null;
let prereqs: PrereqReport;
let config: CeremonyConfig | null = null;
let apiBase: string | null = null;

function createWindow(): void {
  // 移除預設 application menu（File/Edit/View…）；保留視窗標題列與最小化/關閉鈕。
  Menu.setApplicationMenu(null);
  mainWindow = new BrowserWindow({
    width: 1280,
    height: 860,
    show: false,
    title: '寶覺寺法會報名系統',
    autoHideMenuBar: true,
    webPreferences: {
      preload: path.join(__dirname, 'preload.js'),
      contextIsolation: true,
      nodeIntegration: false,
      // Chromium 內建 PDF viewer 需要 plugins:true 才會啟用（Electron 預設 false）。
      // 沒開的話報表預覽頁的 <iframe src="blob:...pdf"> 是空白、window.open(blob:) 會變成下載——
      // 使用者看到的就是「按列印卻叫不出印表機」。見 docs/gotchas.md。
      plugins: true,
    },
  });
  // 啟動即最大化；width/height 保留為還原（un-maximize）後的預設尺寸。
  mainWindow.once('ready-to-show', () => {
    mainWindow?.maximize();
    mainWindow?.show();
  });
  // window.open 開出的子視窗（PDF 預覽）預設不繼承 parent 的 webPreferences，尤其 noopener 更是全新視窗；
  // 這裡明示補上 plugins:true，否則子視窗一樣看不到 PDF。同時把非 blob:/file: 的外開導向系統瀏覽器。
  mainWindow.webContents.setWindowOpenHandler(({ url }) => {
    if (url.startsWith('blob:') || url.startsWith('file:')) {
      return {
        action: 'allow',
        overrideBrowserWindowOptions: {
          autoHideMenuBar: true,
          webPreferences: { plugins: true, contextIsolation: true, nodeIntegration: false },
        },
      };
    }
    void shell.openExternal(url);
    return { action: 'deny' };
  });
  // renderer 自己 window.open 出來的預覽視窗走的不是 print.ts，焦點交還要在這裡補一次。
  mainWindow.webContents.on('did-create-window', (child) => returnFocusOnClose(child, mainWindow));
  mainWindow.on('closed', () => {
    mainWindow = null;
  });
}

async function loadRenderer(extraQuery?: string): Promise<void> {
  if (!mainWindow) return;
  // dev：CEREMONY_RENDERER_URL 指向 ng serve；prod：載入打包後的 index.html。
  const devUrl = process.env.CEREMONY_RENDERER_URL;
  if (devUrl) {
    const u = new URL(devUrl);
    if (extraQuery) u.search = extraQuery;
    await mainWindow.loadURL(u.toString());
    return;
  }
  const indexPath = path.join(__dirname, '../../dist/frontend/browser/index.html');
  await mainWindow.loadFile(indexPath, extraQuery ? { search: extraQuery } : undefined);
}

/** 連線成功 → 帶 apiBase 重新載入 renderer（main.ts 會讀 query 覆寫 environment.apiBaseUrl）。 */
async function loadAppWithApi(base: string): Promise<void> {
  apiBase = base;
  await loadRenderer(`apiBase=${encodeURIComponent(base)}`);
}

async function bootstrap(): Promise<void> {
  void sweepTempDir(); // 清上次崩潰留下的列印暫存 PDF
  // 上次崩潰時可能還停在「某報表的紙張」——那份設定是使用者工作階段共用的（Word/Excel 也會吃到），
  // 所以開機先把它還原回去。見 docs/blueprints/print-channel-electron.md 決策 9。
  void recoverPendingFormRestore();
  prereqs = await detectPrereqs();
  config = await readConfig();
  // default-config.json 為「連線權威」：每次啟動以出廠種子覆寫 config 的連線（保留既有 jwtKey），
  // 確保改種子後 config.json 立即跟進、也避免殘留舊測試連線（如先前的 (local)）。
  // 無種子才沿用既有 config（缺種子 + 無 config → 退回 /setup）。writeConfig 會自動補每機隨機 jwtKey。
  const seed = await readDefaultConfig();
  if (seed?.dbHost) {
    config = await writeConfig({ ...(seed as CeremonyConfig), jwtKey: config?.jwtKey });
  }
  createWindow();

  if (prereqs.ok && config) {
    const r = await startSidecar(config);
    if (r.ok && r.apiBase) {
      await loadAppWithApi(r.apiBase);
      return;
    }
    // 連線失敗 → 載入殼，renderer 依 getStatus 走 /setup 並顯示錯誤。
  }
  await loadRenderer();
}

app.whenReady().then(bootstrap);

app.on('window-all-closed', () => {
  stopSidecar();
  if (process.platform !== 'darwin') app.quit();
});
app.on('before-quit', () => {
  stopSidecar();
  void sweepTempDir();
  // 正常關閉時檢視器視窗的 closed 通常已經還原過了；這裡補一次，接住「視窗還開著就關 app」。
  void releaseReportForm();
});
app.on('activate', () => {
  if (BrowserWindow.getAllWindows().length === 0) bootstrap();
});

// ───────────────────────── IPC ─────────────────────────

ipcMain.handle('ceremony:getStatus', () => ({
  isElectron: true,
  prereqs,
  prereqsOk: prereqs?.ok ?? true,
  hasConfig: !!config,
  connected: !!apiBase,
  apiBase,
  // 回顯既有設定供 /setup 預填（不含密碼）
  config: config
    ? {
        dbHost: config.dbHost,
        dbPort: config.dbPort,
        dbName: config.dbName,
        dbUser: config.dbUser,
        apiPort: config.apiPort ?? 0,
      }
    : null,
}));

ipcMain.handle('ceremony:recheckPrereqs', async () => {
  prereqs = await detectPrereqs();
  return prereqs;
});

ipcMain.handle('ceremony:testConnection', async (_e, cfg: CeremonyConfig) => {
  // 只 ping /health（匿名），不需 jwtKey、不寫 config。
  return startSidecar(cfg);
});

ipcMain.handle('ceremony:saveConfigAndConnect', async (_e, cfg: CeremonyConfig) => {
  // 先寫檔（產生並持久化 jwtKey），再用含 key 的 config 啟動，確保 token 簽章一致。
  config = await writeConfig({ ...cfg, jwtKey: config?.jwtKey });
  const r = await startSidecar(config);
  if (!r.ok || !r.apiBase) return r;
  await loadAppWithApi(r.apiBase);
  return { ok: true, apiBase: r.apiBase };
});

ipcMain.handle('ceremony:connect', async () => {
  if (!config) return { ok: false, error: '尚未設定資料庫連線' };
  const r = await startSidecar(config);
  if (r.ok && r.apiBase) await loadAppWithApi(r.apiBase);
  return r;
});

ipcMain.handle('ceremony:downloadBackup', async (_e, fileName: string, token: string) => {
  if (!mainWindow || !apiBase) return { ok: false, error: '尚未連線' };
  return downloadBackup(mainWindow, apiBase, fileName, token);
});

// ── 列印通道 ──
// 主行程只做兩件事：把 PDF 弄到本機檔案、開檢視器視窗。送印本身（印表機／份數／紙張／方向／
// 頁面範圍）全部由 Windows 原生列印對話框接手——與舊系統相同。
// 契約見 docs/blueprints/print-channel-electron.md。

/** 主力路徑：main 自己向 sidecar 串流取 PDF，數百 MB 的批次也不會經過 renderer。 */
ipcMain.handle(
  'ceremony:openReportInViewer',
  async (_e, reportType: string, apiPath: string, token: string) => {
    if (!apiBase) return { ok: false, error: '尚未連線' };
    return openReportInViewer(reportType, apiBase, apiPath, token, mainWindow);
  },
);

/** PDF 已在 renderer 手上（報表預覽頁）才走 IPC 傳 bytes。 */
ipcMain.handle('ceremony:openPdfInViewer', (_e, reportType: string, bytes: Uint8Array) =>
  openPdfInViewer(reportType, bytes, mainWindow),
);

/** 診斷：在檔案總管中選取今天的列印紀錄，讓使用者直接把檔案傳回來。 */
ipcMain.handle('ceremony:openPrintLogFolder', () => {
  shell.showItemInFolder(printLogPath());
  return { ok: true };
});

/**
 * 自動選紙的現場開關（決策 9d）。
 *
 * 存在的理由是 2026-08-10 客訴：某些驅動（KYOCERA PA2000）被自動選紙碰過之後，原生列印對話框
 * 會整個卡死，只能重啟程式。在那之前唯一的止血手段是 `CEREMONY_PRINTFORM_EXE` 環境變數——
 * 寺方按不到，等於每次都要遠端協助。
 */
ipcMain.handle('ceremony:getPrintFormState', () => printFormState());

ipcMain.handle('ceremony:setPrintFormEnabled', (_e, enabled: boolean) =>
  setPrintFormEnabled(enabled === true),
);

ipcMain.handle('ceremony:openExternal', async (_e, url: string) => {
  await shell.openExternal(url);
  return { ok: true };
});

ipcMain.handle('ceremony:launchInstaller', async (_e, key: string) => {
  const item = prereqs?.items.find((i) => i.key === key);
  if (!item) return { ok: false, error: '未知項目' };
  // 安裝包若 bundle 了 installer（resources/prereqs/）則直接執行，否則開官方下載頁。
  if (item.installerFile && app.isPackaged) {
    const p = path.join(process.resourcesPath, 'prereqs', item.installerFile);
    try {
      if (fs.existsSync(p)) {
        spawn(p, [], { detached: true, stdio: 'ignore' }).unref();
        return { ok: true, launched: true };
      }
    } catch {
      // 落到開連結
    }
  }
  await shell.openExternal(item.downloadUrl);
  return { ok: true, launched: false };
});
