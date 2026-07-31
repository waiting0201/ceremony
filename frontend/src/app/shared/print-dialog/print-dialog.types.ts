import type { PrinterInfo, ScaleMode } from '../../core/platform/electron';

/**
 * 'printer'      = Electron，能真的送印，顯示印表機 / 份數 / 縮放。
 * 'preview-only' = 瀏覽器（ng serve），沒有印表機能力，只能預覽 + 開新分頁自行列印。
 */
export type PrintDialogMode = 'printer' | 'preview-only';

export interface PrintDialogConfig {
  /** 報表中文名，如「資料卡」 */
  reportLabel: string;
  /** 唯讀顯示，如「21 × 14.8 cm」 */
  paperLabel: string;
  /** 附註，如「共 128 筆」 */
  detail?: string;
  printers: PrinterInfo[];
  deviceName?: string;
  copies: number;
  scaleMode: ScaleMode;
  mode: PrintDialogMode;
  /** blob: URL；null = 無預覽。建立與回收都在 PrintDialogService，元件只負責顯示。 */
  previewUrl: string | null;
  /** 無預覽時的說明，如「資料量大（共 1,200 筆），略過預覽」 */
  previewNotice?: string;
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
  scaleMode: ScaleMode;
  /** 勾了就把這次的選擇寫進 print-settings.json，下次同種報表直接沿用 */
  remember: boolean;
}
