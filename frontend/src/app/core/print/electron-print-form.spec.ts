import {
  FormApplyResult,
  applyArgs,
  blockScope,
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

    it('讀得懂 skipped-virtual（跨語言契約：C# 的 PrinterFormPolicy.ToResult）', () => {
      // 這串沒列進 HELPER_RESULTS 的話會被退成 helper-error，診斷紀錄就看不出
      // 「預設印表機是 Print to PDF」這個現場最常見的誤設。
      const r = parseHelperOutput('{"result":"skipped-virtual","form":"薦牌","virtual":true}');

      expect(r.result).toBe('skipped-virtual');
      expect(r.virtual).toBe(true);
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
      const r = parseHelperOutput(
        '{"result":"exact","kind":"257","prev":{"kind":9},"virtual":"no"}',
      );

      expect(r.result).toBe('exact');
      expect(r.kind).toBeUndefined();
      expect(r.prev).toBeUndefined();
      expect(r.virtual).toBeUndefined();
    });
  });

  describe('viewerTitle', () => {
    it('尺寸不符要講「沒幫你選」而不只是「請重建」', () => {
      // 2026-08-06 起尺寸不符不再自動選用（PrinterFormPolicy）。標題只說「請重建」會讓
      // 使用者以為這次還是選好了，然後直接按列印印在錯的紙上。
      const t = viewerTitle({ result: 'mismatch', form: '資料卡' });

      expect(t).toContain('⚠');
      expect(t).toContain('資料卡');
      expect(t).toContain('未自動選用');
      expect(t).toContain('手動選紙');
    });

    it('驅動裡沒有這張紙也要警告（那正是客訴的狀態）', () => {
      expect(viewerTitle({ result: 'not-found', form: '文牒' })).toContain('⚠');
    });

    it.each<FormApplyResult['result']>([
      'skipped-printticket-reject',
      'skipped-printticket-unavailable',
    ])('%s 要告訴使用者手動選哪一張紙', (result) => {
      // 2026-08-08：寫入前的 PrintTicket 預檢沒過 ⇒ 我們沒幫他選。使用者能做的事跟 not-found
      // 一樣，所以標題講「手動選『資料卡』」，不講 PrintTicket / 驅動這些他處理不了的詞。
      const t = viewerTitle({ result, form: '資料卡' });

      expect(t).toContain('⚠');
      expect(t).toContain('資料卡');
    });

    it('另一個列印視窗開著時要說明為什麼沒自動選紙', () => {
      const t = viewerTitle({ result: 'skipped-viewer-open' });

      expect(t).toContain('⚠');
      expect(t).toContain('手動選紙');
    });

    it('這台印表機已被停用時要講「已停用自動選紙」而不是沉默', () => {
      // 2026-08-10（決策 9d）：碰過會出事的驅動我們永久不再接觸，所以連表單名都沒有——
      // 使用者要知道的是「這次沒幫你選，請自己選」，不是我們內部怎麼記帳的。
      const t = viewerTitle({ result: 'skipped-printer-blocked' });

      expect(t).toContain('⚠');
      expect(t).toContain('手動選紙');
    });

    it('使用者自己關掉自動選紙時不掛 ⚠（那不是異常）', () => {
      const t = viewerTitle({ result: 'skipped-disabled' });

      expect(t).not.toContain('⚠');
      expect(t).toContain('自動選紙已關閉');
    });

    it('helper 還在跑時沿用「另一個列印視窗開著」的說法', () => {
      expect(viewerTitle({ result: 'skipped-helper-busy' })).toContain('⚠');
    });

    it('逾時沒寫入也要告訴使用者手動選哪一張紙', () => {
      const t = viewerTitle({ result: 'skipped-over-budget', form: '文牒' });

      expect(t).toContain('⚠');
      expect(t).toContain('文牒');
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
      // 預設印表機是 Print to PDF 之類的機器：套印本來就無從談起，不值得用標題騷擾使用者。
      'skipped-virtual',
    ])('%s 不警告——不是使用者能處理的事，而且列印照常可用', (result) => {
      expect(viewerTitle({ result })).toBe('列印預覽 — 請按工具列的列印鈕');
    });
  });

  describe('blockScope', () => {
    it.each<FormApplyResult['result']>([
      'skipped-printticket-reject',
      'skipped-printticket-unavailable',
      'driver-rejected',
      'error',
    ])('%s ⇒ 這台印表機不再碰', (result) => {
      expect(blockScope({ result, printerHash: 'a1b2c3d4' })).toBe('printer');
    });

    it.each<FormApplyResult['result']>(['helper-timeout', 'helper-error'])(
      '%s ⇒ 整台機器停用（我們根本不知道是哪台印表機，而卡住正是最該停手的訊號）',
      (result) => {
        expect(blockScope({ result })).toBe('all');
      },
    );

    it('驅動好好回答的失敗不記帳——那是健康的結果', () => {
      // 這幾格記帳的話，「現場還沒建表單」會被誤鎖成永久黑名單，之後建好了也不會生效。
      expect(blockScope({ result: 'not-found', form: '文牒' })).toBe('none');
      expect(blockScope({ result: 'mismatch', printerHash: 'a1b2c3d4' })).toBe('none');
      expect(blockScope({ result: 'skipped-virtual', printerHash: 'a1b2c3d4' })).toBe('none');
      expect(blockScope({ result: 'exact', printerHash: 'a1b2c3d4' })).toBe('none');
      expect(blockScope({ result: 'unchanged' })).toBe('none');
    });

    it('我們自己決定跳過的暫時狀態不得變成永久黑名單', () => {
      expect(blockScope({ result: 'skipped-viewer-open' })).toBe('none');
      expect(blockScope({ result: 'skipped-helper-busy' })).toBe('none');
      expect(blockScope({ result: 'skipped-disabled' })).toBe('none');
      expect(blockScope({ result: 'skipped-printer-blocked' })).toBe('none');
      expect(blockScope({ result: 'skipped-over-budget', printerHash: 'a1b2c3d4' })).toBe('none');
      expect(blockScope({ result: 'skipped-not-windows' })).toBe('none');
      expect(blockScope({ result: 'helper-missing' })).toBe('none');
    });

    it('驅動出事但沒有 printerHash ⇒ 退成整台停用，不是不記', () => {
      // 記不到那台頭上又不停用，等於這道閘門對這條路徑完全失效。
      expect(blockScope({ result: 'error' })).toBe('all');
    });
  });

  describe('applyArgs', () => {
    it('黑名單為空時不送 --blocked（helper 端就不必分辨空字串）', () => {
      expect(applyArgs('datacard', 3000, [])).toEqual(['apply', 'datacard', '--budget-ms', '3000']);
    });

    it('黑名單以逗號串接（C# 的 PrinterContactPolicy.ParseBlocked 是對應的解析端）', () => {
      expect(applyArgs('tablet', 3000, ['a1b2c3d4', 'ff00ee11'])).toEqual([
        'apply',
        'tablet',
        '--budget-ms',
        '3000',
        '--blocked',
        'a1b2c3d4,ff00ee11',
      ]);
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
      expect(needsRestore({ result: 'unchanged', prev })).toBe(false);
      expect(needsRestore({ result: 'not-found' })).toBe(false);
      expect(needsRestore({ result: 'exact' })).toBe(false);
    });

    it('預檢沒過的兩種結果都不寫入，因此不得留還原 journal', () => {
      // 留了 journal 的後果是關窗時拿一份「沒發生過的原始值」去覆蓋使用者現在的設定——
      // 預檢本來就是為了不要動他的驅動設定，還原反而動了它就本末倒置。
      const prev = { kind: 9, fields: 2, w: 0, h: 0 };

      expect(needsRestore({ result: 'skipped-printticket-reject', prev })).toBe(false);
      expect(needsRestore({ result: 'skipped-printticket-unavailable', prev })).toBe(false);
    });

    it('mismatch 不再需要還原——2026-08-06 起它根本不寫入', () => {
      // 就算 helper 因為某種理由還是帶了 prev 回來（舊版 exe 混搭新版前端），也不能當成
      // 「動過」——記了 journal 就會在關窗時拿一份沒發生過的快照去覆蓋使用者的設定。
      expect(needsRestore({ result: 'mismatch', prev: { kind: 9, fields: 2, w: 0, h: 0 } })).toBe(
        false,
      );
      expect(
        needsRestore({ result: 'skipped-virtual', prev: { kind: 9, fields: 2, w: 0, h: 0 } }),
      ).toBe(false);
    });
  });

  describe('restoreArgs', () => {
    it('印表機名稱以獨立參數傳遞（名稱含空白也不會被拆開）', () => {
      expect(
        restoreArgs({ kind: 9, fields: 2, w: 0, h: 0, printer: 'EPSON LQ-310 ESC/P' }),
      ).toEqual(['restore', '9', '2', '0', '0', 'EPSON LQ-310 ESC/P']);
    });

    it('沒有印表機名稱時交給 helper 用預設印表機', () => {
      expect(restoreArgs({ kind: 9, fields: 2, w: 0, h: 0 })).toEqual([
        'restore',
        '9',
        '2',
        '0',
        '0',
      ]);
    });
  });
});
