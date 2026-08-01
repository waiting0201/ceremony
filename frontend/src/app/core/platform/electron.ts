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
// 送印基準是「什麼都不指定」：紙張 / 邊界 / 縮放交回印表機驅動，主行程只指定印表機與份數。
// 瀏覽器沒有印表機能力，非 Electron 環境會退回開新分頁預覽。

export interface PrinterInfo {
  name: string;
  displayName: string;
  isDefault: boolean;
  status: number;
}

/**
 * 送印的三個獨立軸，預設全部 'driver'ㄧ「什麼都不指定，交回印表機驅動」。
 * 權威定義與理由在 electron/print-options.ts，這裡只是 renderer 側的鏡射。
 */
export type ScaleMode = 'driver' | 'actual' | 'fit';
export type OrientationMode = 'driver' | 'portrait' | 'landscape';
export type PaperMode = 'driver' | 'report';

export interface ReportPrintSetting {
  deviceName?: string;
  copies?: number;
  scale?: ScaleMode;
  orientation?: OrientationMode;
  paper?: PaperMode;
}

/** v1 的 `scaleMode` 在主行程讀取時就地遷移成三軸（見 electron/print-settings-migrate.ts）。 */
export interface PrintSettings {
  version: 2;
  byReportType: Record<string, ReportPrintSetting>;
}

export interface PrintResult {
  ok: boolean;
  canceled?: boolean;
  error?: string;
}

export interface CeremonyBridge {
  getStatus(): Promise<CeremonyStatus>;
  recheckPrereqs(): Promise<PrereqReport>;
  testConnection(cfg: DbConfigInput): Promise<ConnectResult>;
  saveConfigAndConnect(cfg: DbConfigInput): Promise<ConnectResult>;
  connect(): Promise<ConnectResult>;
  downloadBackup(fileName: string, token: string): Promise<DownloadResult>;
  listPrinters(): Promise<PrinterInfo[]>;
  getPrintSettings(): Promise<PrintSettings>;
  savePrintSetting(reportType: string, setting: ReportPrintSetting): Promise<PrintSettings>;
  /**
   * 送印 renderer 手上的 PDF bytes。這是**唯一**的送印通道——大量列印在前端切成
   * ≤200 筆的段，每段約 27 MB，IPC 傳 bytes 沒有成本問題。
   */
  printPdfBuffer(
    reportType: string,
    bytes: Uint8Array,
    overrides: ReportPrintSetting,
    /** X-Report-Page-Size 原字串；送印不用它，但會記進診斷紀錄 */
    pageSizeHeader?: string | null,
  ): Promise<PrintResult>;
  /**
   * 診斷 / 逃生門：把 PDF 開在 Chromium 檢視器視窗，使用者自己按工具列的列印鈕。
   * 那條路會落到 Windows 原生列印對話框，**有「印表機內容」按鈕**，也是改版前的行為。
   */
  openPdfInViewer(reportType: string, bytes: Uint8Array): Promise<PrintResult>;
  /** 診斷：在檔案總管中選取今天的列印紀錄，讓使用者把檔案傳回來。 */
  openPrintLogFolder(): Promise<{ ok: boolean }>;
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
