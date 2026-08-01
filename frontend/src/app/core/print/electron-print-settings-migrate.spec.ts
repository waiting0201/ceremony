import { describe, expect, it } from 'vitest';
// electron/ 主行程的純函式；刻意不 import 'electron' 才能在此環境載入。
import { migrate, sanitizeSetting } from '../../../../electron/print-settings-migrate';

/**
 * v1 → v2 遷移的行為鎖。
 *
 * 這支測試存在的理由：v2.3.7 的列印對話框「記住」預設是勾的，所以現場的 print-settings.json
 * 幾乎都已經落地 `scaleMode:'actual'`——那個模式正是「位置全跑掉」的來源。
 * 只改程式的預設值救不到已經落地的設定，遷移必須真的發生在 read 端。
 */
describe('print-settings migrate', () => {
  const DRIVER = { scale: 'driver', orientation: 'driver', paper: 'driver' } as const;

  it('v1 的 scaleMode 被丟棄、三軸重設為 driver；deviceName / copies 保留（那是使用者明確的選擇）', () => {
    const { settings, changed } = migrate({
      version: 1,
      byReportType: { datacard: { deviceName: 'HP-1', copies: 3, scaleMode: 'actual' } },
    });

    expect(changed).toBe(true);
    expect(settings).toEqual({
      version: 2,
      byReportType: { datacard: { deviceName: 'HP-1', copies: 3, ...DRIVER } },
    });
  });

  it("v1 的 'fit' 也一起重設——無法分辨「刻意選 fit」與「印歪了亂試」", () => {
    const { settings } = migrate({
      version: 1,
      byReportType: { tablet: { copies: 1, scaleMode: 'fit' } },
    });

    expect(settings.byReportType['tablet']).toEqual({
      deviceName: undefined,
      copies: 1,
      ...DRIVER,
    });
  });

  it('v2 使用者自己選的三軸要留住（遷移只針對 v1，不能每次讀檔都清掉）', () => {
    const v2 = {
      version: 2,
      byReportType: {
        text: {
          deviceName: 'EPSON',
          copies: 2,
          scale: 'actual',
          orientation: 'landscape',
          paper: 'report',
        },
      },
    };

    expect(migrate(v2)).toEqual({ settings: v2, changed: false });
  });

  it('已經是 v2 就不再改寫（changed=false → 不必多寫一次檔）', () => {
    const v2 = {
      version: 2,
      byReportType: { datacard: { deviceName: 'HP-1', copies: 2, ...DRIVER } },
    };

    expect(migrate(v2)).toEqual({ settings: v2, changed: false });
  });

  it.each([null, undefined, 'nope', 42, [], { byReportType: 'nope' }])(
    '壞檔 %s 一律回安全預設而不是丟例外（列印不能因為設定檔壞掉就停擺）',
    (bad) => {
      const { settings, changed } = migrate(bad);

      expect(settings).toEqual({ version: 2, byReportType: {} });
      expect(changed).toBe(true);
    },
  );

  it('缺 version 視同舊檔，內容照樣正規化', () => {
    const { settings, changed } = migrate({
      byReportType: { text: { deviceName: 'EPSON', copies: 2, scaleMode: 'actual' } },
    });

    expect(changed).toBe(true);
    expect(settings).toEqual({
      version: 2,
      byReportType: { text: { deviceName: 'EPSON', copies: 2, ...DRIVER } },
    });
  });

  describe('sanitizeSetting', () => {
    it.each([
      [{ copies: 250 }, 99],
      [{ copies: 0 }, 1],
      [{ copies: 2.6 }, 3],
      [{ copies: 'x' }, 1],
      [{}, 1],
    ])('份數 %o 夾回 %i（驅動不一定會擋手滑打成 999 份）', (input, expected) => {
      expect(sanitizeSetting(input).copies).toBe(expected);
    });

    it('空字串印表機名視同未指定，走系統預設', () => {
      expect(sanitizeSetting({ deviceName: '' }).deviceName).toBeUndefined();
    });

    it.each([
      ['scale', 'nope'],
      ['orientation', 'sideways'],
      ['paper', 'a4'],
      ['scale', 42],
    ] as const)('手改成不認得的 %s=%o 一律退回 driver（安全的那一端）', (key, bad) => {
      expect(sanitizeSetting({ [key]: bad })[key]).toBe('driver');
    });
  });
});
