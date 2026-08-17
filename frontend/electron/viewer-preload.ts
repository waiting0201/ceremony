// 決策 11 的預覽視窗 preload —— 只暴露兩件事，別再加。
//
// 這個視窗載入的是我們自己產生的 wrapper 頁（工具列 + <iframe> 包住 PDF），
// 而不是像舊路徑那樣直接 loadFile 一份裸 PDF。理由：
//   comdlg32 的舊版列印對話框**完全沒有預覽區**（那是 Windows 95 時代的對話框），
//   而 Windows 11 新版對話框的預覽區要由 app 用 PrintTicket／XPS 那套 API 餵——
//   正是我們要繞開的那一層。所以預覽必須由我們自己出，就像舊系統自己開 PrintPreviewDialog。
//
// ⚠️ 這支 preload 掛在一個會載入 file:// 內容的視窗上，暴露面要壓到最小：
// 沒有參數、沒有路徑、沒有任何可以被頁面內容影響的東西。要印哪一份由主行程自己記著。
import { contextBridge, ipcRenderer } from 'electron';

contextBridge.exposeInMainWorld('ceremonyViewer', {
  /** 叫出 Windows 舊版列印對話框。回傳時代表「對話框已在螢幕上」，不是「印完了」。 */
  print: (): Promise<{ ok: boolean; error?: string }> => ipcRenderer.invoke('ceremony:viewerPrint'),
  /** 關掉這個預覽視窗。 */
  close: (): void => ipcRenderer.send('ceremony:viewerClose'),
});
