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
  /**
   * 列印：main 直接向 sidecar 串流取報表 PDF，開在檢視器視窗（PDF 不經 renderer）。
   * 使用者按工具列列印鈕 → Windows 原生對話框。
   */
  openReportInViewer: (reportType: string, apiPath: string, token: string) =>
    ipcRenderer.invoke('ceremony:openReportInViewer', reportType, apiPath, token),
  /** 列印：renderer 手上已有的 PDF bytes（報表預覽頁），同樣開檢視器視窗 */
  openPdfInViewer: (reportType: string, bytes: Uint8Array) =>
    ipcRenderer.invoke('ceremony:openPdfInViewer', reportType, bytes),
  /** 診斷：在檔案總管中選取今天的列印紀錄 */
  openPrintLogFolder: () => ipcRenderer.invoke('ceremony:openPrintLogFolder'),
  /** 自動選紙：目前狀態（含我們自己停用掉的印表機數量） */
  getPrintFormState: () => ipcRenderer.invoke('ceremony:getPrintFormState'),
  /** 自動選紙：開／關；打開時同時清掉失敗印表機黑名單（＝現場的「再試一次」） */
  setPrintFormEnabled: (enabled: boolean) =>
    ipcRenderer.invoke('ceremony:setPrintFormEnabled', enabled),
  /** 排障：叫出 Windows 的「列印喜好設定」，讓使用者改一次紙覆寫掉壞掉的驅動設定 */
  openPrinterPreferences: () => ipcRenderer.invoke('ceremony:openPrinterPreferences'),
  /** 列印方式：檢視器（舊）／對話框（決策 11）。per-machine，不是 per-click */
  getPrintPath: () => ipcRenderer.invoke('ceremony:getPrintPath'),
  setPrintPath: (viaDialog: boolean) => ipcRenderer.invoke('ceremony:setPrintPath', viaDialog),
  /** 排障：中止卡住的列印對話框（決策 11 路徑；預覽視窗被 modal disable 時按不到，所以放主視窗） */
  abortPrintDialog: () => ipcRenderer.invoke('ceremony:abortPrintDialog'),
  /** 開外部連結（官方下載頁等） */
  openExternal: (url: string) => ipcRenderer.invoke('ceremony:openExternal', url),
  /** 執行 bundle 的 prereq installer（缺檔則開官方下載頁） */
  launchInstaller: (key: string) => ipcRenderer.invoke('ceremony:launchInstaller', key),
});
