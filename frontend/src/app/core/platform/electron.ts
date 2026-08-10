// Renderer 端 Electron 橋接型別與偵測。preload.ts 透過 contextBridge 暴露 window.ceremony。
// 非 Electron（瀏覽器 / ng serve）時 window.ceremony 為 undefined → isElectron() = false，
// 所有 Electron 專屬流程（prereq / setup / 原生下載）自動略過。

export interface PrereqItem {
  key: 'vcredist' | 'dotnet';
  name: string;
  ok: boolean;
  detail?: string;
  downloadUrl: string;
  installerFile?: string;
}

export interface PrereqReport {
  ok: boolean;
  skipped: boolean;
  platform: string;
  items: PrereqItem[];
}

export interface CeremonyStatus {
  isElectron: boolean;
  prereqs: PrereqReport;
  prereqsOk: boolean;
  hasConfig: boolean;
  connected: boolean;
  apiBase: string | null;
  config: {
    dbHost: string;
    dbPort: number;
    dbName: string;
    dbUser: string;
    apiPort: number;
  } | null;
}

export interface DbConfigInput {
  dbHost: string;
  dbPort: number;
  dbName: string;
  dbUser: string;
  dbPassword: string;
  apiPort?: number;
}

export interface ConnectResult {
  ok: boolean;
  apiBase?: string;
  error?: string;
}

export interface DownloadResult {
  ok: boolean;
  canceled?: boolean;
  path?: string;
  error?: string;
}

// ── 列印通道（見 docs/blueprints/print-channel-electron.md）──
// 程式只負責把 PDF 開在檢視器視窗（＝舊系統的 PrintPreviewDialog）；印表機、份數、紙張、
// 方向、頁面範圍全部由 Windows 原生列印對話框決定，不記任何列印偏好。
// 瀏覽器沒有這個能力，非 Electron 環境退回開新分頁。

export interface PrintResult {
  ok: boolean;
  error?: string;
}

/**
 * 自動選紙（開檢視器視窗前把驅動的紙張預選成該報表的表單）的現場狀態。
 *
 * `blockedAll` / `blockedPrinters` 是**我們自己**停用的：某台驅動在自動選紙時出過事就不再碰它
 * （2026-08-10 KYOCERA PA2000 卡死客訴，決策 9d）。使用者按「開啟」等於說「我知道，再試一次」。
 */
export interface PrintFormState {
  enabled: boolean;
  blockedAll: boolean;
  blockedPrinters: number;
}

export interface CeremonyBridge {
  getStatus(): Promise<CeremonyStatus>;
  recheckPrereqs(): Promise<PrereqReport>;
  testConnection(cfg: DbConfigInput): Promise<ConnectResult>;
  saveConfigAndConnect(cfg: DbConfigInput): Promise<ConnectResult>;
  connect(): Promise<ConnectResult>;
  downloadBackup(fileName: string, token: string): Promise<DownloadResult>;
  /**
   * 主力列印路徑：main 自己向 sidecar 串流取報表 PDF，開在檢視器視窗。
   *
   * PDF **不經過 renderer**——批次合併成一份可達數百 MB，走 IPC 會 renderer + main 各一份。
   * @param apiPath 例如 `/api/v1/reports/datacard?signupId=…`
   */
  openReportInViewer(reportType: string, apiPath: string, token: string): Promise<PrintResult>;
  /** PDF 已在 renderer 手上（報表預覽頁）時走這條，一樣開檢視器視窗。 */
  openPdfInViewer(reportType: string, bytes: Uint8Array): Promise<PrintResult>;
  /** 診斷：在檔案總管中選取今天的列印紀錄，讓使用者把檔案傳回來。 */
  openPrintLogFolder(): Promise<{ ok: boolean }>;
  /** 自動選紙的現場開關（決策 9d）：讀狀態 / 開關；打開時一併清掉失敗印表機黑名單。 */
  getPrintFormState(): Promise<PrintFormState>;
  setPrintFormEnabled(enabled: boolean): Promise<PrintFormState>;
  /**
   * 排障：叫出 Windows 的「列印喜好設定」（預設印表機）。
   *
   * 現場的復位步驟必須是按鈕——請客戶自己去 `%APPDATA%` 找檔案在實務上等於做不到。
   */
  openPrinterPreferences(): Promise<{ ok: boolean; error?: string }>;
  openExternal(url: string): Promise<{ ok: boolean }>;
  launchInstaller(key: string): Promise<{ ok: boolean; launched?: boolean; error?: string }>;
}

declare global {
  interface Window {
    ceremony?: CeremonyBridge;
  }
}

export function isElectron(): boolean {
  return typeof window !== 'undefined' && !!window.ceremony;
}

export function ceremony(): CeremonyBridge | null {
  return (typeof window !== 'undefined' && window.ceremony) || null;
}
