import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import type { CeremonyBridge, PrintFormState } from '../../core/platform/electron';
import { ReportsPreviewPage } from './reports-preview-page';

/**
 * 回歸鎖（2026-08-11 使用者回報「工具列沒看到」）：列印排障列的可見性**不得**依賴預覽。
 *
 * v2.4.6 把三顆復位鍵（診斷紀錄／印表機設定／自動選紙）放在 `.preview-toolbar` 裡，
 * 而那條工具列整段在 `@if (previewUrl())` 之內——要先成功產出一份 PDF 才會出現。
 * 但它們要救的故障情境正是「按下列印鈕整個卡死／印不出來」，那時根本產不出預覽，
 * 等於這幾顆按鈕在唯一需要它們的情況下必定不可見。
 *
 * 判準：**復位鍵的可見性不得依賴任何會被故障本身破壞的前提。**
 * 見 docs/blueprints/print-channel-electron.md 決策 9e。
 */
describe('ReportsPreviewPage（列印排障列）', () => {
  const formState: PrintFormState = { enabled: true, blockedAll: false, blockedPrinters: 0 };

  /**
   * 只放本頁會用到的幾支；其餘 bridge 方法本頁不碰。
   *
   * ⚠️ 這個 stub 刻意**不**補齊整個 bridge——它同時是「呼叫端對缺方法夠不夠韌」的哨兵。
   * 2026-08-17 加 `getPrintPath` 時就是它先叫的：`ceremony()?.getPrintPath()` 在方法不存在時
   * 是**同步** TypeError，`.catch()` 掛不上去，於是整個 ngOnInit 炸掉、排障列整條不見。
   * 呼叫端已改為 `?.getPrintPath?.()`。
   */
  const stubBridge = (): void => {
    window.ceremony = {
      getPrintFormState: () => Promise.resolve(formState),
      setPrintFormEnabled: () => Promise.resolve(formState),
      getPrintPath: () => Promise.resolve({ viaDialog: false }),
      setPrintPath: (viaDialog: boolean) => Promise.resolve({ viaDialog }),
    } as unknown as CeremonyBridge;
  };

  const create = (): ComponentFixture<ReportsPreviewPage> => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    });
    const fixture = TestBed.createComponent(ReportsPreviewPage);
    fixture.detectChanges();
    return fixture;
  };

  /** ngOnInit 的 `printFormState()` 是 async；多刷一輪 microtask 才看得到「自動選紙」那格。 */
  const settle = async (fixture: ComponentFixture<ReportsPreviewPage>): Promise<void> => {
    await fixture.whenStable();
    await new Promise((resolve) => setTimeout(resolve, 0));
    fixture.detectChanges();
  };

  const labels = (fixture: ComponentFixture<ReportsPreviewPage>, selector: string): string[] =>
    [...fixture.nativeElement.querySelectorAll(`${selector} button`)].map((b) =>
      (b as HTMLElement).textContent!.replace(/\s+/g, ''),
    );

  afterEach(() => {
    delete window.ceremony;
    TestBed.resetTestingModule();
  });

  it('桌面版：還沒有任何預覽時，排障列就已經在畫面上', async () => {
    stubBridge();
    const fixture = create();
    await settle(fixture);

    // 前提：確實沒有預覽（`.preview-toolbar` 不存在）
    expect(fixture.nativeElement.querySelector('.preview-toolbar')).toBeNull();

    const bar = fixture.nativeElement.querySelector('.trouble-bar');
    expect(bar).not.toBeNull();
    // 決策 11 起多一顆「列印方式」：它與其他三顆一樣是**故障當下才要按的鍵**，
    // 所以同樣不得依賴「先產生一份預覽」這個會被故障本身破壞的前提。
    expect(labels(fixture, '.trouble-bar')).toEqual([
      '印表機設定',
      '自動選紙：開',
      '列印方式：檢視器',
      '診斷紀錄',
    ]);
  });

  it('走「對話框」路徑時多一顆止血鍵；按下去會叫 bridge', async () => {
    // 2026-08-18 客訴：對話框卡住關不掉、預覽視窗被 modal disable，現場只能關掉整個程式。
    // 這顆鍵是那條出路的替代品，所以它**只**在對話框路徑出現（檢視器路徑沒有我們的行程可砍）。
    let aborted = 0;
    window.ceremony = {
      getPrintFormState: () => Promise.resolve(formState),
      setPrintFormEnabled: () => Promise.resolve(formState),
      getPrintPath: () => Promise.resolve({ viaDialog: true }),
      setPrintPath: (viaDialog: boolean) => Promise.resolve({ viaDialog }),
      abortPrintDialog: () => {
        aborted += 1;
        return Promise.resolve({ aborted: true });
      },
    } as unknown as CeremonyBridge;

    const fixture = create();
    await settle(fixture);

    expect(labels(fixture, '.trouble-bar')).toEqual([
      '印表機設定',
      '自動選紙：開',
      '列印方式：對話框',
      '中止列印視窗',
      '診斷紀錄',
    ]);

    const btn = [...fixture.nativeElement.querySelectorAll('.trouble-bar button')].find(
      (b) => (b as HTMLElement).textContent!.replace(/\s+/g, '') === '中止列印視窗',
    ) as HTMLButtonElement;
    btn.click();
    await settle(fixture);
    expect(aborted).toBe(1);
  });

  it('bridge 沒有 abortPrintDialog（舊版桌面殼）也不會炸掉整頁', async () => {
    // 與 getPrintPath 同型的哨兵：缺方法是**同步** TypeError，.catch() 掛不上去。
    stubBridge();
    window.ceremony = {
      ...window.ceremony!,
      getPrintPath: () => Promise.resolve({ viaDialog: true }),
    } as unknown as CeremonyBridge;

    const fixture = create();
    await settle(fixture);

    const btn = [...fixture.nativeElement.querySelectorAll('.trouble-bar button')].find(
      (b) => (b as HTMLElement).textContent!.replace(/\s+/g, '') === '中止列印視窗',
    ) as HTMLButtonElement;
    expect(() => btn.click()).not.toThrow();
    await settle(fixture);
    expect(fixture.nativeElement.querySelector('.trouble-bar')).not.toBeNull();
  });

  it('桌面版：排障鈕不重複出現在預覽工具列裡', async () => {
    stubBridge();
    const fixture = create();
    await settle(fixture);

    const all = labels(fixture, '.page');
    for (const label of ['印表機設定', '診斷紀錄']) {
      expect(all.filter((t) => t === label).length).toBe(1);
    }
  });

  it('非桌面版（瀏覽器 / ng serve）：整條排障列不顯示', async () => {
    const fixture = create();
    await settle(fixture);

    expect(fixture.nativeElement.querySelector('.trouble-bar')).toBeNull();
  });
});

/**
 * 回歸鎖（2026-08-11 使用者回報「編號起訖會重疊、含後續年份的 checkbox 也被壓到」）：
 * 語意相依的欄位要綁成一組，換行時整組一起走。
 *
 * 版面重疊的真因是 CSS（`.field` 是 grid，子項 min-width 預設 auto ＝ input 的固有寬約 190px，
 * 不會被壓縮 ⇒ 溢出 100px 的欄位盒、蓋到右邊欄位），那一半在 jsdom 量不到，
 * 已用 Playwright 實測各寬度確認無重疊、無水平溢出。這裡鎖的是結構的那一半：
 * 只要有人把 `.field-group` 拆掉，窄視窗下「起」與「迄」就會被分到不同列。
 */
describe('ReportsPreviewPage（列印表單版面）', () => {
  const create = (): ComponentFixture<ReportsPreviewPage> => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    });
    const fixture = TestBed.createComponent(ReportsPreviewPage);
    fixture.detectChanges();
    return fixture;
  };

  const formRow = (fixture: ComponentFixture<ReportsPreviewPage>): HTMLElement =>
    fixture.nativeElement.querySelector('.form-row') as HTMLElement;

  /** 以欄位標題找出那個 `.field` / `.field-check`。 */
  const fieldByLabel = (row: HTMLElement, text: string): HTMLElement =>
    [...row.querySelectorAll('label')].find((l) =>
      l.querySelector('span')?.textContent?.includes(text),
    ) as HTMLElement;

  afterEach(() => {
    TestBed.resetTestingModule();
  });

  /**
   * 回歸鎖（2026-08-11 使用者指定）：本頁**不得**再要求使用者輸入 Signup ID。
   *
   * 原本的「單筆列印」分頁要貼上 GUID，而現場沒有任何畫面看得到那串值——等於一個
   * 只有開發者用得動的入口佔著主要位置。單筆／多筆的正式入口是報名維護的右鍵選單。
   * 判準：**要使用者輸入的識別碼，必須是他在畫面上看得到的那一個。**
   */
  it('頁面上沒有任何要求輸入 Signup ID 的欄位，並指出單筆列印該去哪裡', () => {
    const fixture = create();
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';

    expect(text).not.toContain('Signup ID');
    expect(fixture.nativeElement.querySelector('.mode-tab')).toBeNull();
    expect(text).toContain('報名維護');
  });

  it('編號起與編號迄在同一個 .field-group（不會被拆到不同列）', () => {
    const row = formRow(create());
    const start = fieldByLabel(row, '編號起');
    const end = fieldByLabel(row, '編號迄');

    expect(start.parentElement!.classList.contains('field-group')).toBe(true);
    expect(start.parentElement).toBe(end.parentElement);
  });

  it('民國年與「含後續年份」在同一個 .field-group', () => {
    const row = formRow(create());
    const year = fieldByLabel(row, '民國年');
    const gte = fieldByLabel(row, '含後續年份');

    expect(year.parentElement!.classList.contains('field-group')).toBe(true);
    expect(year.parentElement).toBe(gte.parentElement);
  });

  it('.field-group 只是換行用的包裝，不吃掉任何欄位', () => {
    const row = formRow(create());
    const labels = [...row.querySelectorAll('label')];

    // 七個欄位（報表類型／民國年／含後續年份／法會分類／報名類型／編號起／編號迄）都還在
    expect(labels.length).toBe(7);
    expect(row.querySelectorAll('input, select').length).toBe(7);
  });
});
