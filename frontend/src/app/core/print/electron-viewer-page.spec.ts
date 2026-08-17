import { describe, expect, it } from 'vitest';
import { toFileUrl, viewerPageHtml } from '../../../../electron/viewer-page';

/**
 * 決策 11 預覽視窗的 wrapper 頁（純字串產生器）。
 *
 * 這一組最重要的是**中文路徑**：temp 檔會落在 `C:\Users\王小明\AppData\Local\Temp\…`，
 * 而這類 bug 開發機永遠看不到——與 gotchas〈createWriteStream 不會補父目錄〉同型。
 */
describe('viewer-page', () => {
  describe('toFileUrl', () => {
    it('Windows 反斜線換成正斜線', () => {
      expect(toFileUrl('C:\\temp\\a.pdf')).toBe('file:///C:/temp/a.pdf');
    });

    it('中文使用者名稱要 encode（現場常態）', () => {
      const url = toFileUrl('C:\\Users\\王小明\\AppData\\Local\\Temp\\ceremony-print\\a.pdf');
      expect(url).toContain('%E7%8E%8B%E5%B0%8F%E6%98%8E');
      expect(url).not.toContain('王');
    });

    it('空白也要 encode，否則 iframe src 會被截斷', () => {
      expect(toFileUrl('C:\\my docs\\a.pdf')).toBe('file:///C:/my%20docs/a.pdf');
    });

    it('# 與 ? 必須 encode —— 否則會被當成 fragment／query，PDF 直接載不到', () => {
      const url = toFileUrl('C:\\a#b?c\\d.pdf');
      expect(url).not.toMatch(/#b/);
      expect(url).not.toMatch(/\?c/);
    });

    it('POSIX 路徑（開發機）也能處理', () => {
      expect(toFileUrl('/tmp/ceremony-print/a.pdf')).toBe('file:///tmp/ceremony-print/a.pdf');
    });
  });

  describe('viewerPageHtml', () => {
    const html = viewerPageHtml('C:\\temp\\datacard.pdf', '列印預覽');

    it('有我們自己的「列印」鈕（＝舊系統 PrintPreviewDialog 那顆）', () => {
      expect(html).toContain('id="print"');
      expect(html).toContain('>列印<');
    });

    it('PDF 的 iframe 帶 #toolbar=0 —— 藏掉通往壞路徑的那顆 🖨', () => {
      // 新路徑上 Chromium 那顆列印鈕會走到 Windows 新版對話框（已知會壞）。
      // 同一個動作有兩個語意不同的入口，比少一顆按鈕糟得多。
      expect(html).toContain('#toolbar=0');
    });

    it('標題會逃脫，避免報表名帶標籤字元時破版', () => {
      expect(viewerPageHtml('C:\\a.pdf', '<script>x</script>')).not.toContain('<script>x');
    });

    it('路徑裡的引號不會逃出 src 屬性', () => {
      expect(viewerPageHtml('C:\\a"onerror="alert(1).pdf', 't')).not.toContain('"onerror="');
    });

    it('按下列印之後按鈕會解鎖 —— 取消或卡紙都要能再按一次', () => {
      // 這條是「再列印一次」落在 Phase 1 的依據：同一份 temp PDF 可以重複送印，不重跑渲染。
      expect(html).toContain('btn.disabled = false');
    });
  });
});
