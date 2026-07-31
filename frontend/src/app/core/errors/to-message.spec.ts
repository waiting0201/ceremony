import { ApiError } from '../http/api-error';
import { toMessage, UserFacingError } from './to-message';

/**
 * 行為鎖：列印通道的錯誤訊息曾經被無差別蓋成「操作失敗，請稍後再試」，
 * 使用者因此看不到 ENOENT / 列印逾時 / 尚未連線等唯一線索。
 */
describe('toMessage', () => {
  it('ApiError → 用後端給的中文訊息', () => {
    expect(toMessage(new ApiError(404, 'SIGNUP_NOT_FOUND', '找不到報名'))).toBe('找不到報名');
  });

  it('UserFacingError → 原樣透出（主行程回的列印錯誤走這條）', () => {
    expect(toMessage(new UserFacingError('列印逾時（印表機無回應）'))).toBe(
      '列印逾時（印表機無回應）',
    );
  });

  it('一般 Error → 不透出技術訊息，回 fallback', () => {
    expect(toMessage(new TypeError('x is not a function'))).toBe('操作失敗，請稍後再試');
  });

  it('非 Error 的東西也不會炸', () => {
    expect(toMessage('boom')).toBe('操作失敗，請稍後再試');
    expect(toMessage(null)).toBe('操作失敗，請稍後再試');
  });

  it('自訂 fallback 生效（登入 / 備份等頁面用）', () => {
    expect(toMessage(new Error('x'), '登入失敗，請稍後再試')).toBe('登入失敗，請稍後再試');
    // 但有訊息可用時仍以訊息優先
    expect(toMessage(new UserFacingError('尚未連線'), '登入失敗，請稍後再試')).toBe('尚未連線');
  });
});
