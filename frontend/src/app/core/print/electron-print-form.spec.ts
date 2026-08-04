import {
  FormApplyResult,
  logFields,
  needsRestore,
  parseHelperOutput,
  restoreArgs,
  viewerTitle,
} from '../../../../electron/print-form-core';

/**
 * electron/print-form-core.ts 的純函式測試（spec 放 src/ 是因為 unit-test builder 只掃 src/）。
 *
 * 重點是 parseHelperOutput：Ceremony.PrintForm.exe 的 exit code 一律是 0，成敗只看 stdout 的
 * result 欄位，所以這個函式是唯一的判讀點——它壞掉會連累整條列印。任何非預期輸入都必須
 * 安靜地退成 helper-error，不能丟例外。
 */
describe('print-form-core', () => {
  describe('parseHelperOutput', () => {
    it('讀得懂 exact', () => {
      const r = parseHelperOutput(
        '{"result":"exact","form":"資料卡","kind":257,"printerHash":"a1b2c3d4","virtual":false,' +
          '"prev":{"kind":9,"fields":2,"w":0,"h":0,"printer":"EPSON LQ-310"},"ms":42}',
      );

      expect(r.result).toBe('exact');
      expect(r.form).toBe('資料卡');
      expect(r.kind).toBe(257);
      expect(r.printerHash).toBe('a1b2c3d4');
      expect(r.virtual).toBe(false);
      expect(r.ms).toBe(42);
      expect(r.prev).toEqual({ kind: 9, fields: 2, w: 0, h: 0, printer: 'EPSON LQ-310' });
    });

    it('把 mismatchMm 攤成單一字串', () => {
      const r = parseHelperOutput(
        '{"result":"mismatch","form":"資料卡","kind":257,"mismatchMm":{"w":-8.32,"h":-5.76}}',
      );

      expect(r.result).toBe('mismatch');
      expect(r.mismatchMm).toBe('-8.32x-5.76');
    });

    it('只取最後一行 JSON（驅動可能往 stdout 吐東西）', () => {
      const r = parseHelperOutput('some driver noise\n\n{"result":"not-found","form":"文牒"}\n');

      expect(r.result).toBe('not-found');
      expect(r.form).toBe('文牒');
    });

    it.each([
      ['', 'empty'],
      ['   \n  ', 'blank'],
      ['not json', 'garbage'],
      ['{}', 'no result'],
      ['{"result":"weird"}', 'unknown result'],
      ['{"result":123}', 'non-string result'],
    ])('非預期輸出 %s 一律回 helper-error 而非丟例外', (stdout) => {
      expect(parseHelperOutput(stdout).result).toBe('helper-error');
    });

    it('欄位型別不對就整格丟掉，不會污染結果', () => {
      const r = parseHelperOutput('{"result":"exact","kind":"257","prev":{"kind":9},"virtual":"no"}');

      expect(r.result).toBe('exact');
      expect(r.kind).toBeUndefined();
      expect(r.prev).toBeUndefined();
      expect(r.virtual).toBeUndefined();
    });
  });

  describe('viewerTitle', () => {
    it('尺寸不符要在標題警告使用者去重建表單', () => {
      const t = viewerTitle({ result: 'mismatch', form: '資料卡' });

      expect(t).toContain('⚠');
      expect(t).toContain('資料卡');
      expect(t).toContain('請按工具列的列印鈕');
    });

    it('驅動裡沒有這張紙也要警告（那正是客訴的狀態）', () => {
      expect(viewerTitle({ result: 'not-found', form: '文牒' })).toContain('⚠');
    });

    it.each<FormApplyResult['result']>([
      'exact',
      'unchanged',
      'helper-missing',
      'helper-timeout',
      'helper-error',
      'driver-rejected',
      'no-default-printer',
      'skipped-not-windows',
    ])('%s 不警告——不是使用者能處理的事，而且列印照常可用', (result) => {
      expect(viewerTitle({ result })).toBe('列印預覽 — 請按工具列的列印鈕');
    });
  });

  describe('logFields', () => {
    it('是白名單：印表機原始名稱絕不進診斷紀錄', () => {
      const fields = logFields({
        result: 'exact',
        form: '資料卡',
        kind: 257,
        printerHash: 'a1b2c3d4',
        prev: { kind: 9, fields: 2, w: 0, h: 0, printer: '\\\\PC-王小明\\HP LaserJet 1020' },
      });

      expect(JSON.stringify(fields)).not.toContain('王小明');
      expect(JSON.stringify(fields)).not.toContain('LaserJet');
      expect(fields).toEqual({
        formResult: 'exact',
        formTarget: '資料卡',
        formKind: 257,
        printerHash: 'a1b2c3d4',
      });
    });

    it('缺的欄位不會變成 undefined 鍵', () => {
      expect(logFields({ result: 'helper-missing' })).toEqual({ formResult: 'helper-missing' });
    });

    it('帶出印歪時要看的三個線索', () => {
      const fields = logFields({
        result: 'mismatch',
        form: '資料卡',
        mismatchMm: '-8.32x-5.76',
        ms: 42,
        virtual: true,
      });

      expect(fields['formMismatchMm']).toBe('-8.32x-5.76');
      expect(fields['formMs']).toBe(42);
      expect(fields['printerVirtual']).toBe(true);
    });
  });

  describe('needsRestore', () => {
    it('只有真的動到驅動設定才要記還原快照', () => {
      const prev = { kind: 9, fields: 2, w: 0, h: 0 };

      expect(needsRestore({ result: 'exact', prev })).toBe(true);
      expect(needsRestore({ result: 'mismatch', prev })).toBe(true);
      expect(needsRestore({ result: 'unchanged', prev })).toBe(false);
      expect(needsRestore({ result: 'not-found' })).toBe(false);
      expect(needsRestore({ result: 'exact' })).toBe(false);
    });
  });

  describe('restoreArgs', () => {
    it('印表機名稱以獨立參數傳遞（名稱含空白也不會被拆開）', () => {
      expect(restoreArgs({ kind: 9, fields: 2, w: 0, h: 0, printer: 'EPSON LQ-310 ESC/P' })).toEqual([
        'restore',
        '9',
        '2',
        '0',
        '0',
        'EPSON LQ-310 ESC/P',
      ]);
    });

    it('沒有印表機名稱時交給 helper 用預設印表機', () => {
      expect(restoreArgs({ kind: 9, fields: 2, w: 0, h: 0 })).toEqual(['restore', '9', '2', '0', '0']);
    });
  });
});
