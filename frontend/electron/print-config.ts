// %APPDATA%/Ceremony/print-settings.json — 每種報表記住上次用的印表機與份數。
//
// 為何不放 config.json：main.ts 的 bootstrap 每次啟動都用 default-config.json 種子覆寫 config
// （只保留 jwtKey），塞進去會被吃掉。而且印表機是「每台機器」的屬性，與「連線權威由出廠種子決定」
// 的語意衝突。決策見 docs/design/infrastructure.md、docs/blueprints/print-channel-electron.md。
//
// 壞檔 / 缺檔一律回空設定走系統預設，不阻斷列印——列印是櫃檯的主要動作，不能因為設定檔壞掉就不能印。
//
// 資料模型與版本遷移在 print-settings-migrate.ts（純函式，測得到）；本檔只負責 I/O。
import { app } from 'electron';
import { promises as fs } from 'fs';
import path from 'path';
import { logPrintEvent } from './print-log';
import { emptySettings, migrate, sanitizeSetting } from './print-settings-migrate';
import type { PrintSettings, ReportPrintSetting } from './print-settings-migrate';

export type { PrintSettings, ReportPrintSetting };

function settingsPath(): string {
  return path.join(app.getPath('appData'), 'Ceremony', 'print-settings.json');
}

/**
 * 讀設定並就地遷移。
 *
 * 遷移一定要在 read 端：v2.3.7 的對話框「記住」預設勾選，現場設定檔幾乎都已經落地
 * `scaleMode:'actual'`（就是位置跑掉的來源），只改程式預設值救不到它們。
 * 寫回是 best-effort——寫不進去（唯讀磁碟 / 權限）也要能繼續印。
 */
export async function readPrintSettings(): Promise<PrintSettings> {
  let parsed: unknown;
  try {
    parsed = JSON.parse(await fs.readFile(settingsPath(), 'utf-8'));
  } catch {
    return emptySettings();
  }

  const { settings, changed } = migrate(parsed);
  if (changed) {
    void logPrintEvent({ event: 'settings-migrated', to: settings.version });
    void writeSettings(settings).catch(() => {
      // 寫不回去就下次再遷移一次；不能因此擋住列印
    });
  }
  return settings;
}

/** 只覆寫指定報表那一格，其他報表的設定原樣保留。 */
export async function savePrintSetting(
  reportType: string,
  setting: ReportPrintSetting,
): Promise<PrintSettings> {
  const current = await readPrintSettings();
  const next: PrintSettings = {
    version: current.version,
    byReportType: { ...current.byReportType, [reportType]: sanitizeSetting(setting) },
  };
  await writeSettings(next);
  return next;
}

async function writeSettings(s: PrintSettings): Promise<void> {
  await fs.mkdir(path.dirname(settingsPath()), { recursive: true });
  await fs.writeFile(settingsPath(), JSON.stringify(s, null, 2), 'utf-8');
}
