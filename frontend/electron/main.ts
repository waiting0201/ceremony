// Electron main：生命週期 + prereq 偵測 + sidecar 啟動 + 首次設定導流 + IPC handlers。
// 啟動流程：偵測 prereq → 讀 config → (prereq ok && 有 config) 則自動連線載入主程式，
// 否則載入殼讓 renderer 走 /prereq 或 /setup。詳見 docs/design/infrastructure.md。
import { app, BrowserWindow, ipcMain, shell } from 'electron';
import path from 'path';
import fs from 'fs';
import { spawn } from 'child_process';
import { readConfig, writeConfig, CeremonyConfig, DEFAULT_CONFIG } from './config';
import { detectPrereqs, PrereqReport } from './prereq';
import { startSidecar, stopSidecar } from './sidecar';
import { downloadBackup } from './download';

let mainWindow: BrowserWindow | null = null;
let prereqs: PrereqReport;
let config: CeremonyConfig | null = null;
let apiBase: string | null = null;

function createWindow(): void {
  mainWindow = new BrowserWindow({
    width: 1280,
    height: 860,
    show: false,
    title: '寶覺寺法會報名系統',
    webPreferences: {
      preload: path.join(__dirname, 'preload.js'),
      contextIsolation: true,
      nodeIntegration: false,
    },
  });
  mainWindow.once('ready-to-show', () => mainWindow?.show());
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
  prereqs = await detectPrereqs();
  config = await readConfig();
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
app.on('before-quit', () => stopSidecar());
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
  // 首次啟動（無 config）時，/setup 以此打包預設預填（含密碼，方便直接按「測試連線」）
  defaults: {
    dbHost: DEFAULT_CONFIG.dbHost,
    dbPort: DEFAULT_CONFIG.dbPort,
    dbName: DEFAULT_CONFIG.dbName,
    dbUser: DEFAULT_CONFIG.dbUser,
    dbPassword: DEFAULT_CONFIG.dbPassword,
    apiPort: DEFAULT_CONFIG.apiPort ?? 0,
  },
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
