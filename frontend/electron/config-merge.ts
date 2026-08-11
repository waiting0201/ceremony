// config.json 的合併規則 —— 純函式（無 electron / fs 相依，可單元測試）。
//
// 為什麼要有這一層：出廠種子 `default-config.json` 是**連線**的權威（決策見
// docs/blueprints/electron-packaging.md 第 2 點），bootstrap 每次啟動都拿它覆寫 config.json。
// 舊寫法是把整包換掉、只手動保留 `jwtKey`，於是任何**只存在於本機**的欄位每次開機都被沖掉。
//
// 2026-08-11 現場症狀：使用者把報表頁的「自動選紙」關掉（`printFormPreselect: false`），
// 當次列印確實是 `skipped-disabled`，但重開程式又變回「開」——同一天的診斷紀錄裡
// 連著三行 `form-preselect-toggled {enabled:false}`，就是他關了三次。
// 種子只有連線五欄，`printFormPreselect` 屬於「種子沒有意見」的欄位卻被一起清掉了。
//
// 規則：**種子有講的欄位由種子作主，種子沒講的一律留著。**
import type { CeremonyConfig } from './config';

/**
 * 把一份權威來源（出廠種子 / `/setup` 表單）疊到既有 config 上。
 *
 * `undefined` 代表「這份來源對這個欄位沒有意見」→ 保留既有值。JSON 解出來的種子只有
 * 連線五欄，其餘（`jwtKey`、`apiPort`、`printFormPreselect`）都是本機自己長出來的，
 * 不能被種子的沉默清掉。
 *
 * ⚠️ 反過來說，只要種子**有寫**該欄位就一定覆蓋——「改種子後立即生效、清掉殘留舊測試連線」
 * 是這條路徑存在的理由，不可為了保本機值而放棄。
 */
export function mergeConfig(
  prev: CeremonyConfig | null,
  incoming: Partial<CeremonyConfig>,
): CeremonyConfig {
  const defined = Object.fromEntries(
    Object.entries(incoming).filter(([, v]) => v !== undefined),
  ) as Partial<CeremonyConfig>;
  return { ...(prev ?? {}), ...defined } as CeremonyConfig;
}
