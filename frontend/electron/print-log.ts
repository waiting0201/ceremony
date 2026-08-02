// %APPDATA%/Ceremony/logs/print-YYYYMMDD.log — 每次開列印預覽視窗一行 JSON。
//
// 為什麼需要：packaged 版沒有 console。2026-08-01 那輪客訴（「格式不對」）查不出症狀細節，
// 就是因為現場什麼證據都留不下來。左側選單的「開啟診斷紀錄」讓使用者直接把檔案傳回來。
//
// 2026-08-02 起送印本身由 Windows 原生對話框接手，我們不再決定任何送印參數，
// 所以這裡記的是「哪一種報表、多大、從哪條路來、開得起來嗎」——**印歪的第一個線索
// 變成 pageSizeHeader（PDF 的實際頁面尺寸）對不對得上驅動裡選的紙**。
//
// 刻意放 appData/Ceremony/logs，不用 app.getPath('logs')——那會落到另一個以 productName
// 命名的目錄，現場找不到。
//
// 隱私：這裡不得出現 signupId、姓名、堂號、任何報表內容、token，或 temp 檔完整路徑。
// 見 docs/design/security.md。
import { app } from 'electron';
import { promises as fs } from 'fs';
import path from 'path';

const MAX_BYTES = 1024 * 1024;
const KEEP_DAYS = 7;

export function logDir(): string {
  return path.join(app.getPath('appData'), 'Ceremony', 'logs');
}

/** YYYYMMDD（本機時區）——現場回報時間對得上他們的時鐘，比 UTC 有用。 */
function stamp(d: Date): string {
  const p = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}${p(d.getMonth() + 1)}${p(d.getDate())}`;
}

export function printLogPath(now: Date = new Date()): string {
  return path.join(logDir(), `print-${stamp(now)}.log`);
}

/**
 * 追加一行 JSON。永遠不丟例外——診斷紀錄壞掉不能讓列印失敗。
 */
export async function logPrintEvent(record: Record<string, unknown>): Promise<void> {
  try {
    const file = printLogPath();
    await fs.mkdir(path.dirname(file), { recursive: true });
    await rotateIfNeeded(file);
    const line = JSON.stringify({
      ts: new Date().toISOString(),
      appVersion: app.getVersion(),
      ...record,
    });
    await fs.appendFile(file, `${line}\n`, 'utf-8');
  } catch {
    // 磁碟滿 / 唯讀 / 權限 → 放棄這一行
  }
}

async function rotateIfNeeded(file: string): Promise<void> {
  const stat = await fs.stat(file).catch(() => null);
  if (stat && stat.size > MAX_BYTES) {
    await fs.rename(file, `${file}.1`).catch(() => undefined);
  }
}

/**
 * 刪掉 KEEP_DAYS 天前的紀錄。掛在 sweepTempDir 的啟動 / 離開兩次掃描上，不另立機制。
 */
export async function sweepOldLogs(): Promise<void> {
  try {
    const dir = logDir();
    const cutoff = Date.now() - KEEP_DAYS * 24 * 60 * 60_000;
    for (const name of await fs.readdir(dir)) {
      if (!name.startsWith('print-')) continue;
      const full = path.join(dir, name);
      const stat = await fs.stat(full).catch(() => null);
      if (stat && stat.mtimeMs < cutoff) await fs.unlink(full).catch(() => undefined);
    }
  } catch {
    // 目錄不存在 = 沒東西要清
  }
}
