import { describe, expect, it } from 'vitest';
import {
  PRINT_RESULTS,
  parsePrintLine,
  printDialogArgs,
  printDialogLogFields,
  printDialogFinalMessage,
  printDialogMessage,
  type PrintDialogOutcome,
} from '../../../../electron/print-dialog-core';

/**
 * 決策 11 送印路徑的純函式。
 *
 * 這一組鎖三件事：跨語言契約（PRINT_RESULTS ↔ C# 的 PrintDialogResults.All）、
 * parser 永不丟例外、以及診斷紀錄的白名單不得洩漏印表機原始名稱。
 */
describe('print-dialog-core', () => {
  describe('PRINT_RESULTS（跨語言契約）', () => {
    it('恰好是這七個值，與 C# 的 PrintDialogResults.All 相同', () => {
      // ⚠️ 改這裡就要同步改 Ceremony.Domain/Reports/PrintDialogResults.cs，反之亦然。
      expect([...PRINT_RESULTS].sort()).toEqual(
        [
          'printed',
          'cancelled',
          'no-default-printer',
          'dialog-failed',
          'render-failed',
          'driver-rejected',
          'error',
        ].sort(),
      );
    });

    it('沒有重複值', () => {
      expect(new Set(PRINT_RESULTS).size).toBe(PRINT_RESULTS.length);
    });
  });

  describe('printDialogArgs', () => {
    it('只給 pdf 時是最小參數', () => {
      expect(printDialogArgs('C:\\a.pdf')).toEqual(['print', 'C:\\a.pdf']);
    });

    it('完整參數', () => {
      expect(
        printDialogArgs('C:\\a.pdf', {
          owner: 12345n,
          reportType: 'datacard',
          noForm: true,
          devmodeSource: 'user',
          scale: 'stretch',
          jobName: '資料卡 1-5',
        }),
      ).toEqual([
        'print', 'C:\\a.pdf',
        '--owner', '12345',
        '--report', 'datacard',
        '--no-form',
        '--devmode-source', 'user',
        '--scale', 'stretch',
        '--job-name', '資料卡 1-5',
      ]);
    });

    it('owner 為 0 或 null 時不帶該參數（0 是無效的 HWND）', () => {
      expect(printDialogArgs('a.pdf', { owner: 0 })).toEqual(['print', 'a.pdf']);
      expect(printDialogArgs('a.pdf', { owner: null })).toEqual(['print', 'a.pdf']);
    });
  });

  describe('parsePrintLine —— 永不丟例外', () => {
    it.each([
      ['', '空行'],
      ['   ', '空白'],
      ['not json', '純文字'],
      ['{壞掉的', '壞 JSON'],
      ['null', 'JSON null'],
      ['[1,2]', '不是物件'],
      ['驅動亂吐的中文訊息', '雜訊'],
    ])('%s（%s）回 null 而不是丟例外', (line) => {
      expect(() => parsePrintLine(line)).not.toThrow();
      expect(parsePrintLine(line)).toBeNull();
    });

    it('第一行 dialog-shown', () => {
      expect(parsePrintLine('{"event":"dialog-shown","devmodeSource":"printer"}')).toEqual({
        shown: true,
        devmodeSource: 'printer',
      });
    });

    it('最後一行帶 result 與診斷欄位', () => {
      expect(
        parsePrintLine('{"result":"printed","pages":3,"dpi":"600x600","destRect":"41,30,4760x3296"}'),
      ).toEqual({ result: 'printed', pages: 3, dpi: '600x600', destRect: '41,30,4760x3296' });
    });

    it('未知的 result 退成 error，不原樣放行', () => {
      // 否則 UI 會拿到一個沒有人處理的字串
      expect(parsePrintLine('{"result":"who-knows"}')).toEqual({ result: 'error' });
    });

    it('型別不對的欄位直接忽略', () => {
      expect(parsePrintLine('{"result":"printed","pages":"三","dpi":600}')).toEqual({
        result: 'printed',
      });
    });
  });

  describe('printDialogLogFields —— 白名單', () => {
    it('帶 path:dialog 讓兩條路徑在診斷紀錄裡分得開', () => {
      expect(printDialogLogFields({ result: 'printed' })['path']).toBe('dialog');
    });

    it('絕不寫出印表機原始名稱', () => {
      const o = {
        result: 'printed',
        printer: '\\\\PC-王小明\\Kyocera PA2000 GX',
        chosenPrinter: '\\\\PC-王小明\\Kyocera PA2000 GX',
      } as unknown as PrintDialogOutcome;

      const f = printDialogLogFields(o);
      expect(JSON.stringify(f)).not.toContain('Kyocera');
      expect(JSON.stringify(f)).not.toContain('王小明');
    });

    it('undefined 的欄位不寫進去', () => {
      expect(Object.keys(printDialogLogFields({ result: 'cancelled' }))).toEqual([
        'path',
        'printResult',
      ]);
    });
  });

  describe('printDialogMessage', () => {
    it('取消不是錯誤，不得顯示訊息', () => {
      expect(printDialogMessage('cancelled')).toBeNull();
    });

    it('成功也不顯示訊息', () => {
      expect(printDialogMessage('printed')).toBeNull();
    });

    it('其餘結果都要有中文訊息', () => {
      for (const r of PRINT_RESULTS) {
        if (r === 'printed' || r === 'cancelled') continue;
        expect(printDialogMessage(r)).toBeTruthy();
      }
    });

    it('驅動類的失敗要指路到 Adobe Reader（現場已證實那條可行）', () => {
      expect(printDialogMessage('driver-rejected')).toContain('Adobe');
      expect(printDialogMessage('dialog-failed')).toContain('Adobe');
    });
  });

  describe('printDialogFinalMessage（送印結束後的事後告知）', () => {
    it('成功講的是「送出到佇列」而不是「已列印」——我們只保證到 spooler', () => {
      const r = printDialogFinalMessage({ result: 'printed', pages: 3 });
      expect(r?.ok).toBe(true);
      expect(r?.text).toContain('3 頁');
      expect(r?.text).toContain('佇列');
      expect(r?.text).not.toContain('已列印');
    });

    it('有 job id 就寫進成功訊息——現場能直接對 Windows 佇列裡的那一筆', () => {
      const r = printDialogFinalMessage({ result: 'printed', pages: 1, jobId: 42 });
      expect(r?.text).toContain('42');
      expect(r?.text).toContain('列印佇列');
    });

    it('沒有頁數也講得出來', () => {
      expect(printDialogFinalMessage({ result: 'printed' })?.text).toContain('已送出到印表機佇列');
    });

    it('使用者取消 ⇒ 什麼都不顯示', () => {
      expect(printDialogFinalMessage({ result: 'cancelled' })).toBeNull();
    });

    it('失敗一定附代碼，現場截圖就能定位', () => {
      const r = printDialogFinalMessage({ result: 'driver-rejected', win32: 1784 });
      expect(r?.ok).toBe(false);
      expect(r?.text).toContain('driver-rejected');
      expect(r?.text).toContain('win32=1784');
    });

    it('沒有 win32 就不硬湊一個代碼進去', () => {
      expect(printDialogFinalMessage({ result: 'render-failed' })?.text).not.toContain('win32');
    });

    it('四種失敗結局各有各的文字（＝現場不會再只有「沒有反應」）', () => {
      const texts = (['driver-rejected', 'render-failed', 'dialog-failed', 'error'] as const).map(
        (result) => printDialogFinalMessage({ result })!.text,
      );
      expect(new Set(texts).size).toBe(4);
    });
  });
});
