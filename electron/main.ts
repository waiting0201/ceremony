/* eslint-disable @typescript-eslint/no-require-imports */
const { app, BrowserWindow, ipcMain } = require('electron');
import * as path from 'path';
import * as dotenv from 'dotenv';
import { getPool, closePool } from './db/connection';
import { registerAllIpc } from './ipc';

dotenv.config({ path: path.join(__dirname, '..', '.env') });

let mainWindow: any = null;

// Window state persistence
const stateFile = path.join(app.getPath('userData'), 'window-state.json');
function loadWindowState(): { width: number; height: number; x?: number; y?: number; maximized?: boolean } {
  try {
    const fs = require('fs');
    return JSON.parse(fs.readFileSync(stateFile, 'utf-8'));
  } catch {
    return { width: 1280, height: 800 };
  }
}

function saveWindowState(): void {
  if (!mainWindow) return;
  const fs = require('fs');
  const bounds = mainWindow.getBounds();
  const state = {
    ...bounds,
    maximized: mainWindow.isMaximized(),
  };
  try { fs.writeFileSync(stateFile, JSON.stringify(state)); } catch {}
}

function createWindow(): void {
  const state = loadWindowState();

  mainWindow = new BrowserWindow({
    width: state.width,
    height: state.height,
    x: state.x,
    y: state.y,
    minWidth: 1024,
    minHeight: 600,
    title: '法會報名系統',
    frame: false,
    webPreferences: {
      preload: path.join(__dirname, 'preload.js'),
      contextIsolation: true,
      nodeIntegration: false,
    },
  });

  if (state.maximized) mainWindow.maximize();

  // Save state on resize/move
  mainWindow.on('resize', saveWindowState);
  mainWindow.on('move', saveWindowState);
  mainWindow.on('maximize', saveWindowState);
  mainWindow.on('unmaximize', saveWindowState);

  const isDev = !app.isPackaged;
  if (isDev) {
    mainWindow.loadURL('http://localhost:4200');
    mainWindow.webContents.openDevTools();
  } else {
    mainWindow.loadFile(
      path.join(__dirname, '..', 'dist', 'Ceremony', 'browser', 'index.html'),
    );
  }

  mainWindow.on('closed', () => {
    mainWindow = null;
  });
}

app.whenReady().then(() => {
  // DB test
  ipcMain.handle('db:test', async () => {
    try {
      const pool = await getPool();
      const result = await pool.request().query('SELECT 1 AS ok');
      return { success: true, data: result.recordset[0] };
    } catch (error: any) {
      return { success: false, message: error.message };
    }
  });

  // Window controls (for frameless window)
  ipcMain.handle('window:minimize', () => mainWindow?.minimize());
  ipcMain.handle('window:maximize', () => {
    if (mainWindow?.isMaximized()) {
      mainWindow.unmaximize();
    } else {
      mainWindow?.maximize();
    }
  });
  ipcMain.handle('window:close', () => mainWindow?.close());

  // Register all domain IPC handlers
  registerAllIpc(ipcMain, () => mainWindow);

  createWindow();
});

app.on('window-all-closed', async () => {
  await closePool();
  if (process.platform !== 'darwin') {
    app.quit();
  }
});

app.on('activate', () => {
  if (mainWindow === null) {
    createWindow();
  }
});
