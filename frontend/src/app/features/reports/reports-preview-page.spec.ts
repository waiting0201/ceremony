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

  /** 只放本頁會用到的兩支；其餘 bridge 方法本頁不碰。 */
  const stubBridge = (): void => {
    window.ceremony = {
      getPrintFormState: () => Promise.resolve(formState),
      setPrintFormEnabled: () => Promise.resolve(formState),
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
    expect(labels(fixture, '.trouble-bar')).toEqual(['印表機設定', '自動選紙：開', '診斷紀錄']);
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
describe('ReportsPreviewPage（批次列印表單版面）', () => {
  const create = (): ComponentFixture<ReportsPreviewPage> => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    });
    const fixture = TestBed.createComponent(ReportsPreviewPage);
    fixture.detectChanges();
    return fixture;
  };

  /** 切到批次分頁；`.form-row.wrap` 只有這個模式才存在。 */
  const openBatch = (fixture: ComponentFixture<ReportsPreviewPage>): HTMLElement => {
    const tabs = [...fixture.nativeElement.querySelectorAll('.mode-tab')] as HTMLElement[];
    tabs.find((t) => t.textContent!.includes('批次列印'))!.click();
    fixture.detectChanges();
    return fixture.nativeElement.querySelector('.form-row.wrap') as HTMLElement;
  };

  /** 以欄位標題找出那個 `.field` / `.field-check`。 */
  const fieldByLabel = (row: HTMLElement, text: string): HTMLElement =>
    [...row.querySelectorAll('label')].find((l) =>
      l.querySelector('span')?.textContent?.includes(text),
    ) as HTMLElement;

  afterEach(() => {
    TestBed.resetTestingModule();
  });

  it('編號起與編號迄在同一個 .field-group（不會被拆到不同列）', () => {
    const row = openBatch(create());
    const start = fieldByLabel(row, '編號起');
    const end = fieldByLabel(row, '編號迄');

    expect(start.parentElement!.classList.contains('field-group')).toBe(true);
    expect(start.parentElement).toBe(end.parentElement);
  });

  it('民國年與「含後續年份」在同一個 .field-group', () => {
    const row = openBatch(create());
    const year = fieldByLabel(row, '民國年');
    const gte = fieldByLabel(row, '含後續年份');

    expect(year.parentElement!.classList.contains('field-group')).toBe(true);
    expect(year.parentElement).toBe(gte.parentElement);
  });

  it('.field-group 只是換行用的包裝，不吃掉任何欄位', () => {
    const row = openBatch(create());
    const labels = [...row.querySelectorAll('label')];

    // 七個欄位（報表類型／民國年／含後續年份／法會分類／報名類型／編號起／編號迄）都還在
    expect(labels.length).toBe(7);
    expect(row.querySelectorAll('input, select').length).toBe(7);
  });
});
