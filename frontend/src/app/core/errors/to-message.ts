import { ApiError } from '../http/api-error';

/**
 * 「訊息本身就是要給使用者看的」錯誤。
 *
 * 為什麼需要這個 marker：toMessage 不能無條件透出 Error.message——TypeError / ChunkLoadError
 * 之類的技術訊息丟到 UI 只會製造客訴。但列印通道回的「列印逾時（印表機無回應）」「尚未連線」
 * 「找不到報名」是使用者唯一的線索，被吞成「操作失敗，請稍後再試」等於把診斷資訊丟掉。
 */
export class UserFacingError extends Error {
  constructor(message: string) {
    super(message);
    this.name = 'UserFacingError';
  }
}

/**
 * 例外 → 可顯示訊息。認得 {@link ApiError}（後端錯誤碼）與 {@link UserFacingError}（本地已成文的訊息），
 * 其餘一律回 fallback 並把原始錯誤留在 devtools。
 *
 * 這份邏輯原本在 13 個 feature 各複製一份 → 新錯誤型別要透出時沒有單一改點，
 * 列印失敗的真正原因就是這樣被吞掉的。
 */
export function toMessage(err: unknown, fallback = '操作失敗，請稍後再試'): string {
  if (err instanceof ApiError || err instanceof UserFacingError) return err.message;
  console.error(err);
  return fallback;
}
