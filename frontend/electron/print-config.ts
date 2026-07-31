// %APPDATA%/Ceremony/print-settings.json — 每種報表記住上次用的印表機 / 份數 / 縮放。
//
// 為何不放 config.json：main.ts 的 bootstrap 每次啟動都用 default-config.json 種子覆寫 config
// （只保留 jwtKey），塞進去會被吃掉。而且印表機是「每台機器」的屬性，與「連線權威由出廠種子決定」
// 的語意衝突。決策見 docs/design/infrastructure.md、docs/blueprints/print-channel-electron.md。
//
// 壞檔 / 缺檔一律回空設定走系統預設，不阻斷列印——列印是櫃檯的主要動作，不能因為設定檔壞掉就不能印。
import { app } from 'electron';
import { promises as fs } from 'fs';
import path from 'path';

/** 'actual' = 100% 實際大小（預設，1:1 對位）；'fit' = 縮放至符合紙張（複製舊系統的拉伸行為）。 */
export type ScaleMode = 'actual' | 'fit';

export interface ReportPrintSetting {
  /** 空 / 缺 = 用系統預設印表機 */
  deviceName?: string;
  copies?: number;
  scaleMode?: ScaleMode;
}

export interface PrintSettings {
  version: 1;
  byReportType: Record<string, ReportPrintSetting>;
}

const EMPTY: PrintSettings = { version: 1, byReportType: {} };

function settingsPath(): string {
  return path.join(app.getPath('appData'), 'Ceremony', 'print-settings.json');
}

export async function readPrintSettings(): Promise<PrintSettings> {
  try {
    const raw = await fs.readFile(settingsPath(), 'utf-8');
    const parsed = JSON.parse(raw) as Partial<PrintSettings>;
    return {
      version: 1,
      byReportType:
        parsed.byReportType && typeof parsed.byReportType === 'object' ? parsed.byReportType : {},
    };
  } catch {
    return { ...EMPTY, byReportType: {} };
  }
}

/** 只覆寫指定報表那一格，其他報表的設定原樣保留。 */
export async function savePrintSetting(
  reportType: string,
  setting: ReportPrintSetting,
): Promise<PrintSettings> {
  const current = await readPrintSettings();
  const next: PrintSettings = {
    version: 1,
    byReportType: { ...current.byReportType, [reportType]: sanitize(setting) },
  };
  await fs.mkdir(path.dirname(settingsPath()), { recursive: true });
  await fs.writeFile(settingsPath(), JSON.stringify(next, null, 2), 'utf-8');
  return next;
}

function sanitize(s: ReportPrintSetting): ReportPrintSetting {
  const copies = Number(s.copies);
  return {
    deviceName: typeof s.deviceName === 'string' && s.deviceName ? s.deviceName : undefined,
    // 份數夾在 1–99：送印前擋掉手滑打成 999 份的情況（驅動不一定會擋）
    copies: Number.isFinite(copies) ? Math.min(99, Math.max(1, Math.round(copies))) : 1,
    scaleMode: s.scaleMode === 'fit' ? 'fit' : 'actual',
  };
}
