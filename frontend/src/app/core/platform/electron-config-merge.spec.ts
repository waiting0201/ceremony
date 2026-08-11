import { mergeConfig } from '../../../../electron/config-merge';
import type { CeremonyConfig } from '../../../../electron/config';

/**
 * electron/config-merge.ts 的純函式測試（spec 放 src/ 是因為 unit-test builder 只掃 src/）。
 *
 * 鎖的是 2026-08-11 的現場回歸：bootstrap 每次啟動以出廠種子覆寫 config.json，舊寫法整包換掉
 * ⇒ 使用者關掉的「自動選紙」每次重開又變回開（診斷紀錄連著三行 form-preselect-toggled
 * {enabled:false}）。兩個方向都要鎖：種子講的一定覆蓋、種子沒講的一定留著。
 */
describe('config-merge', () => {
  const prev: CeremonyConfig = {
    dbHost: '(local)',
    dbPort: 1433,
    dbName: 'Ceremony',
    dbUser: 'sa',
    dbPassword: 'old',
    apiPort: 5210,
    jwtKey: 'k'.repeat(44),
    printFormPreselect: false,
  };

  const seed = {
    dbHost: '192.168.1.151',
    dbPort: 1433,
    dbName: 'Ceremony',
    dbUser: 'sa',
    dbPassword: 'new',
  };

  it('種子的連線欄位覆蓋既有值（改種子後立即生效、清掉殘留舊測試連線）', () => {
    const r = mergeConfig(prev, seed);

    expect(r.dbHost).toBe('192.168.1.151');
    expect(r.dbPassword).toBe('new');
  });

  it('種子沒講的本機欄位一律留著（自動選紙不會被重開機沖回開）', () => {
    const r = mergeConfig(prev, seed);

    expect(r.printFormPreselect).toBe(false);
    expect(r.jwtKey).toBe('k'.repeat(44));
    expect(r.apiPort).toBe(5210);
  });

  it('明寫的 false / 0 仍然覆蓋（不可被當成「沒有意見」）', () => {
    const r = mergeConfig(prev, { ...seed, apiPort: 0, printFormPreselect: true });

    expect(r.apiPort).toBe(0);
    expect(r.printFormPreselect).toBe(true);
  });

  it('undefined 不算意見（JSON 沒有這個 key 與明寫 undefined 同義）', () => {
    const r = mergeConfig(prev, { ...seed, printFormPreselect: undefined });

    expect(r.printFormPreselect).toBe(false);
  });

  it('沒有既有 config（首次啟動）就只剩種子本身', () => {
    const r = mergeConfig(null, seed);

    expect(r.dbHost).toBe('192.168.1.151');
    expect(r.jwtKey).toBeUndefined();
    expect(r.printFormPreselect).toBeUndefined();
  });
});
