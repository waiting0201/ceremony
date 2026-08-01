import type {
  OrientationMode,
  PaperMode,
  PrinterInfo,
  ScaleMode,
} from '../../core/platform/electron';

/**
 * 'printer'      = Electron，能真的送印，顯示印表機 / 份數。
 * 'preview-only' = 瀏覽器（ng serve），沒有印表機能力，只能預覽 + 開新分頁自行列印。
 */
export type PrintDialogMode = 'printer' | 'preview-only';

/**
 * 診斷區的兩個動作。
 * 'viewer' = 把 PDF 開在檢視器視窗自己按列印（＝改版前的路徑，有原生「印表機內容」）
 * 'log'    = 開啟診斷紀錄所在資料夾
 */
export type PrintDiagnosticAction = 'viewer' | 'log';

export interface PrintDialogConfig {
  /** 報表中文名，如「資料卡」 */
  reportLabel: string;
  /** 報表尺寸，如「21 × 14.8 cm」；當成「紙張」下拉的選項文字 */
  paperLabel: string;
  /** 附註，如「共 128 筆」 */
  detail?: string;
  printers: PrinterInfo[];
  deviceName?: string;
  copies: number;
  scale: ScaleMode;
  orientation: OrientationMode;
  paper: PaperMode;
  mode: PrintDialogMode;
  /** blob: URL；null = 無預覽。建立與回收都在 PrintDialogService，元件只負責顯示。 */
  previewUrl: string | null;
  /** 無預覽時的說明，如「資料量大（共 1,200 筆），略過預覽」 */
  previewNotice?: string;
  /** 有值才顯示診斷區（瀏覽器沒有這些能力）。 */
  onDiagnose?: (action: PrintDiagnosticAction) => void;
}

/**
 * ask() 的參數：呼叫端只給 Blob，object URL 的生命週期綁在 overlayRef 上由 service 管——
 * 這是唯一能保證「取消 / 列印 / 例外三條路都成對 revoke」的位置。
 */
export type PrintDialogRequest = Omit<PrintDialogConfig, 'previewUrl'> & {
  previewBlob?: Blob | null;
};

export interface PrintDialogResult {
  deviceName?: string;
  copies: number;
  scale: ScaleMode;
  orientation: OrientationMode;
  paper: PaperMode;
  /** 勾了就把這次的選擇寫進 print-settings.json，下次同種報表直接沿用 */
  remember: boolean;
}
