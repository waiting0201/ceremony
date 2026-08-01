// contextBridge：把受控的 main 能力暴露給 renderer 的 window.ceremony。
// contextIsolation + nodeIntegration:false → renderer 拿不到 Node，只能走這些 IPC。
import { contextBridge, ipcRenderer } from 'electron';

contextBridge.exposeInMainWorld('ceremony', {
  /** 取得目前狀態：prereq / 是否已設定 / 是否已連線 / apiBase / 已存設定（不含密碼） */
  getStatus: () => ipcRenderer.invoke('ceremony:getStatus'),
  /** 重新偵測 prereq（使用者裝完軟體後按「重新檢查」） */
  recheckPrereqs: () => ipcRenderer.invoke('ceremony:recheckPrereqs'),
  /** 測試連線：用給定設定 spawn sidecar 並 ping /health（不寫 config） */
  testConnection: (cfg: unknown) => ipcRenderer.invoke('ceremony:testConnection', cfg),
  /** 儲存設定並連線：寫 config.json → spawn sidecar → 成功則載入主程式 */
  saveConfigAndConnect: (cfg: unknown) => ipcRenderer.invoke('ceremony:saveConfigAndConnect', cfg),
  /** 用既有設定重試連線 */
  connect: () => ipcRenderer.invoke('ceremony:connect'),
  /** 下載備份檔到本機另存（原生對話框 + 串流寫檔；目前 UI 未掛，屬備用能力） */
  downloadBackup: (fileName: string, token: string) =>
    ipcRenderer.invoke('ceremony:downloadBackup', fileName, token),
  /** 列印：可用印表機清單（含 isDefault） */
  listPrinters: () => ipcRenderer.invoke('ceremony:listPrinters'),
  /** 列印：讀每種報表記住的印表機 / 份數 */
  getPrintSettings: () => ipcRenderer.invoke('ceremony:getPrintSettings'),
  /** 列印：記住某種報表的設定（只覆寫該報表那一格） */
  savePrintSetting: (reportType: string, setting: unknown) =>
    ipcRenderer.invoke('ceremony:savePrintSetting', reportType, setting),
  /** 列印：renderer 手上既有的 PDF bytes；pageSizeHeader 是 renderer 讀到的 X-Report-Page-Size */
  printPdfBuffer: (
    reportType: string,
    bytes: Uint8Array,
    overrides: unknown,
    pageSizeHeader?: string | null,
  ) => ipcRenderer.invoke('ceremony:printPdfBuffer', reportType, bytes, overrides, pageSizeHeader),
  /** 診斷：把 PDF 開在檢視器視窗，使用者自己按工具列列印鈕 → 原生對話框（有「印表機內容」） */
  openPdfInViewer: (reportType: string, bytes: Uint8Array) =>
    ipcRenderer.invoke('ceremony:openPdfInViewer', reportType, bytes),
  /** 診斷：在檔案總管中選取今天的列印紀錄 */
  openPrintLogFolder: () => ipcRenderer.invoke('ceremony:openPrintLogFolder'),
  /** 開外部連結（官方下載頁等） */
  openExternal: (url: string) => ipcRenderer.invoke('ceremony:openExternal', url),
  /** 執行 bundle 的 prereq installer（缺檔則開官方下載頁） */
  launchInstaller: (key: string) => ipcRenderer.invoke('ceremony:launchInstaller', key),
});
