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
// 紙張 / 邊界 / 縮放由 Electron 主行程指定；瀏覽器沒有這些能力，非 Electron 環境會退回開新分頁預覽。

export interface PrinterInfo {
  name: string;
  displayName: string;
  isDefault: boolean;
  status: number;
}

/** 'actual' = 100% 實際大小（1:1 對位）；'fit' = 縮放至符合紙張（等同舊系統的拉伸行為）。 */
export type ScaleMode = 'actual' | 'fit';

export interface ReportPrintSetting {
  deviceName?: string;
  copies?: number;
  scaleMode?: ScaleMode;
}

export interface PrintSettings {
  version: 1;
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
  printReport(
    reportType: string,
    apiPath: string,
    token: string,
    overrides: ReportPrintSetting,
  ): Promise<PrintResult>;
  printBatchJob(
    reportType: string,
    jobId: string,
    token: string,
    overrides: ReportPrintSetting,
  ): Promise<PrintResult>;
  printPdfBuffer(
    reportType: string,
    bytes: Uint8Array,
    overrides: ReportPrintSetting,
  ): Promise<PrintResult>;
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
