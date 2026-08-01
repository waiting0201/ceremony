import { describe, expect, it } from 'vitest';
// electron/ 主行程的純函式；刻意不 import 'electron' 才能在此環境載入。
// 測試放在 src/ 是因為 @angular/build:unit-test 只掃 src/**/*.spec.ts。
import { buildPrintOptions } from '../../../../electron/print-options';

const A5 = { width: 210000, height: 148000 };

/**
 * 送印選項的行為鎖。
 *
 * 2026-08-01 客訴「跟之前我們調好的位置都跑掉了」的成因，是 v2.3.7 在送印時無條件多傳了
 * margins:'none' + scaleFactor:100 + pageSize——而 printing-reports-positions.md 那套
 * ±0.05cm 的座標全部是在「什麼都不傳」的基準下驗收的。
 *
 * 所以最重要的一條是「三軸皆 driver → key 集合**恰好**只有四個」，用 Object.keys 而不是
 * toMatchObject：任何人日後順手補一個格式選項，都必須先來這裡改測試、也就必須先讀為什麼。
 */
describe('buildPrintOptions', () => {
  const BASE_KEYS = ['copies', 'deviceName', 'printBackground', 'silent'];

  describe('預設（三軸皆 driver）＝ 什麼都不指定，交回驅動', () => {
    it('key 集合恰好只有四個——沒有 margins / scaleFactor / pageSize / landscape', () => {
      const o = buildPrintOptions({ copies: 3, deviceName: 'HP-1', pageSize: A5 });

      expect(Object.keys(o).sort()).toEqual(BASE_KEYS);
      expect(o).toEqual({ silent: true, printBackground: true, copies: 3, deviceName: 'HP-1' });
    });

    it('明確傳 driver 與整個省略等價（省略不是「未定義行為」）', () => {
      const explicit = buildPrintOptions({
        copies: 1,
        scale: 'driver',
        orientation: 'driver',
        paper: 'driver',
        pageSize: A5,
      });

      expect(explicit).toEqual(buildPrintOptions({ copies: 1 }));
    });

    it('沒指定印表機時連 deviceName 這個 key 都不出現（傳 undefined 會被 Electron 當成有值）', () => {
      expect(Object.keys(buildPrintOptions({ copies: 1 })).sort()).toEqual([
        'copies',
        'printBackground',
        'silent',
      ]);
    });
  });

  describe('scale', () => {
    it("'actual' → margins:none + scaleFactor:100（v2.3.7 的行為，現在要明確選才會發生）", () => {
      const o = buildPrintOptions({ copies: 1, scale: 'actual' });

      expect(o.margins).toEqual({ marginType: 'none' });
      expect(o.scaleFactor).toBe(100);
    });

    it("'fit' → 只給 margins:printableArea，不給 scaleFactor（讓 Chromium 自己縮）", () => {
      const o = buildPrintOptions({ copies: 1, scale: 'fit' });

      expect(o.margins).toEqual({ marginType: 'printableArea' });
      expect('scaleFactor' in o).toBe(false);
    });
  });

  describe('orientation', () => {
    it.each([
      ['portrait', false],
      ['landscape', true],
    ] as const)("'%s' → landscape:%s", (orientation, expected) => {
      expect(buildPrintOptions({ copies: 1, orientation }).landscape).toBe(expected);
    });

    it("'driver' 不送 landscape——送 false 會覆寫驅動 DEVMODE 的 dmOrientation", () => {
      expect('landscape' in buildPrintOptions({ copies: 1, orientation: 'driver' })).toBe(false);
    });
  });

  describe('paper', () => {
    it("'report' 才帶 pageSize", () => {
      expect(buildPrintOptions({ copies: 1, paper: 'report', pageSize: A5 }).pageSize).toEqual(A5);
    });

    it("'report' 但沒有尺寸可用時退回驅動紙張，而不是送垃圾值進驅動", () => {
      expect('pageSize' in buildPrintOptions({ copies: 1, paper: 'report', pageSize: null })).toBe(
        false,
      );
    });
  });

  it('v2.3.7 的「實際大小」可以完整重建（現場真的需要 1:1 時的退路）', () => {
    const o = buildPrintOptions({ copies: 1, scale: 'actual', paper: 'report', pageSize: A5 });

    expect(o).toMatchObject({
      margins: { marginType: 'none' },
      scaleFactor: 100,
      pageSize: A5,
    });
  });
});
