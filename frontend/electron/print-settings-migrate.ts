// print-settings.json 的資料模型與版本遷移（純函式，不 import 'electron' → 測得到）。
//
// I/O 在 print-config.ts；資料模型放這裡，是為了讓「檔案長什麼樣、壞掉怎麼救」有單一改點。
//
// v1 → v2（2026-08-01）：`scaleMode: 'actual'|'fit'` 換成 scale / orientation / paper 三個獨立軸，
// 且**舊值一律丟棄**、重設為 'driver'。
//
// v2.3.7 的列印對話框「記住」預設是勾的（print-dialog.component.ts），所以現場的設定檔幾乎都已經
// 落地 scaleMode:'actual' —— 那個模式正是「位置全跑掉」的來源。只改程式預設值對這些機器完全無效，
// 遷移必須做在 read 端，否則改了等於沒改。
//
// 舊的 'fit' 也一起重設：無法分辨「刻意選 fit」與「印歪了亂試」，而這次改版的目的就是先回到基準線。
// 使用者要的話可以在對話框重新選（v2.3.7 的「實際大小」= scale:'actual' + paper:'report'）。
// deviceName / copies 保留：那是使用者明確的選擇。
// 背景見 docs/blueprints/print-channel-electron.md。
import type { OrientationMode, PaperMode, ScaleMode } from './print-options';

export const SETTINGS_VERSION = 2;

const SCALES: readonly ScaleMode[] = ['driver', 'actual', 'fit'];
const ORIENTATIONS: readonly OrientationMode[] = ['driver', 'portrait', 'landscape'];
const PAPERS: readonly PaperMode[] = ['driver', 'report'];

export interface ReportPrintSetting {
  /** 空 / 缺 = 用系統預設印表機 */
  deviceName?: string;
  copies?: number;
  scale?: ScaleMode;
  orientation?: OrientationMode;
  paper?: PaperMode;
}

export interface PrintSettings {
  version: typeof SETTINGS_VERSION;
  byReportType: Record<string, ReportPrintSetting>;
}

export function emptySettings(): PrintSettings {
  return { version: SETTINGS_VERSION, byReportType: {} };
}

/** 白名單：認不得的值一律回第一項（'driver'，安全的那一端），不是丟例外。 */
function pick<T extends string>(value: unknown, allowed: readonly T[]): T {
  return allowed.includes(value as T) ? (value as T) : allowed[0];
}

/**
 * 單筆設定正規化：只留認得的欄位，值不合法就退回 'driver'。
 * 份數夾在 1–99——送印前擋掉手滑打成 999 份的情況（驅動不一定會擋）。
 */
export function sanitizeSetting(raw: unknown): ReportPrintSetting {
  const s = (raw && typeof raw === 'object' ? raw : {}) as Record<string, unknown>;
  const deviceName =
    typeof s['deviceName'] === 'string' && s['deviceName'] ? s['deviceName'] : undefined;
  const copies = Number(s['copies']);
  return {
    deviceName,
    copies: Number.isFinite(copies) ? Math.min(99, Math.max(1, Math.round(copies))) : 1,
    // v1 的 scaleMode 不在白名單裡，也沒有對應欄位可讀 → 一律變 'driver'。遷移就發生在這三行。
    scale: pick(s['scale'], SCALES),
    orientation: pick(s['orientation'], ORIENTATIONS),
    paper: pick(s['paper'], PAPERS),
  };
}

/**
 * 把任何讀進來的東西（舊版、壞檔、被手改過）正規化成目前版本。
 *
 * @returns changed = 內容與輸入不同，呼叫端該寫回一次。壞檔也會回 changed:true（順手修好）。
 */
export function migrate(parsed: unknown): { settings: PrintSettings; changed: boolean } {
  const root = (parsed && typeof parsed === 'object' ? parsed : null) as Record<
    string,
    unknown
  > | null;
  if (!root) return { settings: emptySettings(), changed: true };

  const rawMap =
    root['byReportType'] && typeof root['byReportType'] === 'object'
      ? (root['byReportType'] as Record<string, unknown>)
      : null;

  const byReportType: Record<string, ReportPrintSetting> = {};
  for (const [type, value] of Object.entries(rawMap ?? {})) {
    byReportType[type] = sanitizeSetting(value);
  }

  const settings: PrintSettings = { version: SETTINGS_VERSION, byReportType };
  const changed =
    root['version'] !== SETTINGS_VERSION ||
    !rawMap ||
    JSON.stringify(rawMap) !== JSON.stringify(byReportType);

  return { settings, changed };
}
