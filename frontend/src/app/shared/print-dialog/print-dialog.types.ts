import type { PrinterInfo, ScaleMode } from '../../core/platform/electron';

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
}

export interface PrintDialogResult {
  deviceName?: string;
  copies: number;
  scaleMode: ScaleMode;
  /** 勾了就把這次的選擇寫進 print-settings.json，下次同種報表直接沿用 */
  remember: boolean;
}
