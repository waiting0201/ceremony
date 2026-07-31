import { describe, expect, it } from 'vitest';
import {
  REPORT_PAGE_MICRONS,
  formatPageSizeCm,
  parsePageSizeHeader,
  resolvePageSize,
  // electron/ 主行程的純函式；刻意不 import 'electron' 才能在此環境載入。
  // 測試放在 src/ 是因為 @angular/build:unit-test 只掃 src/**/*.spec.ts。
} from '../../../../electron/paper';

/**
 * 紙張尺寸解析的行為鎖。
 *
 * 這段是「PDF 正確但實印縮放跑掉」的最後一道關卡：header 壞掉時必須退回已知的 fallback 表，
 * 而不是把垃圾值送進印表機驅動。fallback 表本身則要與後端 ReportPageSizes 一致
 * （後端有 ReportPageSizeConsistencyTests 鎖 renderer 側）。
 */
describe('paper', () => {
  it('fallback 表與後端 ReportPageSizes 的微米值一致', () => {
    expect(REPORT_PAGE_MICRONS).toEqual({
      datacard: { width: 210000, height: 148000 },
      receipt: { width: 210000, height: 297000 },
      tablet: { width: 115000, height: 255000 },
      text: { width: 365000, height: 262000 },
      worship: { width: 210000, height: 296000 },
      worshipcard: { width: 210000, height: 148000 },
    });
  });

  it('解析合法 header', () => {
    expect(parsePageSizeHeader('210000x148000')).toEqual({ width: 210000, height: 148000 });
    expect(parsePageSizeHeader('  115000X255000  ')).toEqual({ width: 115000, height: 255000 });
  });

  it.each([
    ['null', null],
    ['undefined', undefined],
    ['空字串', ''],
    ['缺分隔', '210000'],
    ['非數字', 'axb'],
    ['負值', '-210000x148000'],
    ['小數', '210000.5x148000'],
    ['過小（< 1cm）', '5000x5000'],
    ['過大（> 100cm）', '2000000x2000000'],
  ])('壞 header（%s）回 null', (_label, raw) => {
    expect(parsePageSizeHeader(raw)).toBeNull();
  });

  it('resolvePageSize：header 優先、其次 fallback 表、都沒有回 none', () => {
    expect(resolvePageSize('datacard', '300000x200000')).toEqual({
      size: { width: 300000, height: 200000 },
      source: 'header',
    });
    expect(resolvePageSize('datacard', 'garbage')).toEqual({
      size: { width: 210000, height: 148000 },
      source: 'fallback',
    });
    expect(resolvePageSize('unknown-report', null)).toEqual({ size: null, source: 'none' });
  });

  it('formatPageSizeCm 給對話框顯示用', () => {
    expect(formatPageSizeCm({ width: 210000, height: 148000 })).toBe('21 × 14.8 cm');
    expect(formatPageSizeCm({ width: 115000, height: 255000 })).toBe('11.5 × 25.5 cm');
    expect(formatPageSizeCm(null)).toBe('印表機預設');
  });
});
