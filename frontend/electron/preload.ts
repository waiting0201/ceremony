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
  /** 下載備份檔到本機另存（原生對話框 + 串流寫檔） */
  downloadBackup: (fileName: string, token: string) =>
    ipcRenderer.invoke('ceremony:downloadBackup', fileName, token),
  /** 開外部連結（官方下載頁等） */
  openExternal: (url: string) => ipcRenderer.invoke('ceremony:openExternal', url),
  /** 執行 bundle 的 prereq installer（缺檔則開官方下載頁） */
  launchInstaller: (key: string) => ipcRenderer.invoke('ceremony:launchInstaller', key),
});
