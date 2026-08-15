import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import type { WritableSignal } from '@angular/core';
import { provideRouter } from '@angular/router';
import type { SignupListItem } from '../../core/api/signups/signup.models';
import { resolveItems, type ContextMenuItem } from '../../shared/context-menu/context-menu.types';
import { SignupListPage } from './signup-list-page';

/**
 * 報名維護清單「列選取」的行為鎖：一般點擊 toggle、shift 選取錨點~本列整段，
 * 且從列首 checkbox 點也要吃得到 shift（2026-07-27 需求）。
 */
describe('SignupListPage（列選取 / shift 範圍選取）', () => {
  type Probe = {
    results: WritableSignal<SignupListItem[]>;
    selectedIds: () => ReadonlySet<string>;
    toggleRow(item: SignupListItem, event: MouseEvent | null, index: number): void;
    onCheckboxClick(item: SignupListItem, event: MouseEvent, index: number): void;
    toggleAll(): void;
    clearSelection(): void;
  };

  const probe = (f: ComponentFixture<SignupListPage>): Probe =>
    f.componentInstance as unknown as Probe;

  const row = (id: string): SignupListItem => ({
    id, year: 113, ceremonyCategoryId: 'c1', ceremonyTitle: null, signupType: 1,
    numberTitle: null, number: null, fee: null, employee: null, employeeType: 1,
    believerId: `b-${id}`, name: id, hallName: null, phone: null, isFixedNumber: false,
    livingNames: [], deadNames: [],
    mailCity: null, mailZone: null, mailZipcode: null, mailAddress: null,
    textCity: null, textZone: null, textZipcode: null, textAddress: null,
    prepayYear: null, prepayCeremonyCategoryId: null, prepayCeremonyTitle: null,
    remark: null, adminName: null, createDate: null,
  });

  /** 六列 r0..r5 的清單；不 detectChanges 以免觸發 ngOnInit 的分類載入與虛擬捲動渲染。 */
  const rows = ['r0', 'r1', 'r2', 'r3', 'r4', 'r5'].map(row);

  const click = (shift = false): MouseEvent =>
    new MouseEvent('click', { shiftKey: shift, cancelable: true });

  const selected = (p: Probe): string[] => [...p.selectedIds()].sort();

  let p: Probe;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    });
    const fixture = TestBed.createComponent(SignupListPage);
    p = probe(fixture);
    p.results.set(rows);
  });

  it('一般點擊 = toggle 該列', () => {
    p.toggleRow(rows[1], click(), 1);
    expect(selected(p)).toEqual(['r1']);

    p.toggleRow(rows[1], click(), 1);
    expect(selected(p)).toEqual([]);
  });

  it('shift 點擊選取「錨點 ~ 本列」整段（往下）', () => {
    p.toggleRow(rows[1], click(), 1);
    p.toggleRow(rows[4], click(true), 4);
    expect(selected(p)).toEqual(['r1', 'r2', 'r3', 'r4']);
  });

  it('shift 往回點也成立（錨點在下、目標在上）', () => {
    p.toggleRow(rows[4], click(), 4);
    p.toggleRow(rows[1], click(true), 1);
    expect(selected(p)).toEqual(['r1', 'r2', 'r3', 'r4']);
  });

  it('連續 shift 以同一錨點重算 → 範圍可以縮小', () => {
    p.toggleRow(rows[1], click(), 1);
    p.toggleRow(rows[5], click(true), 5);
    expect(selected(p)).toEqual(['r1', 'r2', 'r3', 'r4', 'r5']);

    // 錨點仍在 r1（沒有跟著移到 r5），所以再 shift 點 r3 是 r1~r3 而非往外擴
    p.toggleRow(rows[3], click(true), 3);
    expect(selected(p)).toEqual(['r1', 'r2', 'r3']);
  });

  it('shift 範圍不會吃掉錨點之前既有的選取', () => {
    p.toggleRow(rows[0], click(), 0); // 先單獨選 r0
    p.toggleRow(rows[2], click(), 2); // 錨點移到 r2（r0 仍在選取中）
    p.toggleRow(rows[4], click(true), 4);
    expect(selected(p)).toEqual(['r0', 'r2', 'r3', 'r4']);
  });

  it('從列首 checkbox 點也吃得到 shift（回歸鎖：原本走 change 事件拿不到 shiftKey）', () => {
    const first = click();
    p.onCheckboxClick(rows[1], first, 1);
    expect(selected(p)).toEqual(['r1']);
    expect(first.defaultPrevented).toBe(true); // 勾選狀態由 selectedIds 決定，不讓 DOM 自行翻轉

    p.onCheckboxClick(rows[3], click(true), 3);
    expect(selected(p)).toEqual(['r1', 'r2', 'r3']);
  });

  it('全選 / 取消選取後錨點失效，下一次 shift 退化為單純 toggle', () => {
    p.toggleRow(rows[1], click(), 1);
    p.clearSelection();
    p.toggleRow(rows[4], click(true), 4);
    expect(selected(p)).toEqual(['r4']);

    p.toggleAll();
    expect(selected(p)).toEqual(['r0', 'r1', 'r2', 'r3', 'r4', 'r5']);
    p.clearSelection();
    p.toggleRow(rows[2], click(true), 2);
    expect(selected(p)).toEqual(['r2']);
  });
});

/**
 * 搜尋面板預設值的行為鎖：範圍 5 項預設全勾、關鍵字欄開頁即可輸入（2026-07-28 使用者指定，
 * 刻意偏離舊 SignupForm 的全不勾）。resetForm() 也要回到同一組預設。
 */
describe('SignupListPage（搜尋範圍預設）', () => {
  type FormProbe = {
    form: {
      getRawValue(): Record<string, unknown>;
      controls: { searchKey: { disabled: boolean } };
      patchValue(v: Record<string, unknown>): void;
    };
    keyEnabled: () => boolean;
    resetForm(): void;
  };

  const scopes = ['scopeName', 'scopeLivingName', 'scopeDeadName', 'scopePhone', 'scopeRemark'];

  let p: FormProbe;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    });
    p = TestBed.createComponent(SignupListPage).componentInstance as unknown as FormProbe;
  });

  it('姓名/陽上/往生/電話/備註 預設全勾，關鍵字欄可輸入', () => {
    const v = p.form.getRawValue();
    for (const name of scopes) expect(v[name]).toBe(true);
    expect(p.form.controls.searchKey.disabled).toBe(false);
    expect(p.keyEnabled()).toBe(true);
  });

  it('清除條件後仍回到「全勾 + 關鍵字可輸入」', () => {
    p.form.patchValue(Object.fromEntries(scopes.map((n) => [n, false])));
    p.resetForm();

    const v = p.form.getRawValue();
    for (const name of scopes) expect(v[name]).toBe(true);
    expect(p.form.controls.searchKey.disabled).toBe(false);
    expect(p.keyEnabled()).toBe(true);
  });
});

/**
 * 編號起迄的行為鎖（2026-07-31 使用者指定）：搜尋與批次列印的編號都是區間，
 * 只填一端＝只查／只印那一筆；起 > 迄要擋下並提示「編號錯誤」。
 */
describe('SignupListPage（編號起迄區間）', () => {
  type Probe = {
    form: { patchValue(v: Record<string, unknown>): void };
    batchForm: { patchValue(v: Record<string, unknown>): void; invalid: boolean };
    buildQuery(): { numberStart?: number | null; numberEnd?: number | null };
    search(): Promise<void>;
    errorMessage: () => string | null;
  };

  let p: Probe;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    });
    p = TestBed.createComponent(SignupListPage).componentInstance as unknown as Probe;
  });

  it('搜尋只填「起」→ 迄補成同值（只查那一筆）', () => {
    p.form.patchValue({ numberStart: 42, numberEnd: null });
    const q = p.buildQuery();
    expect(q.numberStart).toBe(42);
    expect(q.numberEnd).toBe(42);
  });

  it('搜尋只填「迄」→ 起補成同值（只查那一筆）', () => {
    p.form.patchValue({ numberStart: null, numberEnd: 42 });
    const q = p.buildQuery();
    expect(q.numberStart).toBe(42);
    expect(q.numberEnd).toBe(42);
  });

  it('搜尋兩端皆空 → 不帶編號條件', () => {
    const q = p.buildQuery();
    expect(q.numberStart).toBeNull();
    expect(q.numberEnd).toBeNull();
  });

  it('搜尋起 > 迄 → 擋下並提示「編號錯誤」', async () => {
    p.form.patchValue({ numberStart: 50, numberEnd: 10 });
    await p.search();
    expect(p.errorMessage()).toBe('編號錯誤');
  });

  it('批次列印只填一端仍可送出，兩端皆空才擋', () => {
    expect(p.batchForm.invalid).toBe(true); // 初值兩端皆空

    p.batchForm.patchValue({ numberStart: 7 });
    expect(p.batchForm.invalid).toBe(false);

    p.batchForm.patchValue({ numberStart: null, numberEnd: 7 });
    expect(p.batchForm.invalid).toBe(false);
  });
});

/**
 * 跨路由保留的行為鎖（2026-07-31 使用者回報）：切到其他功能再回報名維護，
 * 條件（含「範圍」）與「顯示完整表格」都要保持原樣，不可被重設。
 * 兩次 createComponent 共用同一個 TestBed injector ⇒ 等同同一個 SignupSearchState singleton。
 */
describe('SignupListPage（跨路由保留搜尋條件 / 檢視設定）', () => {
  type Probe = {
    form: { getRawValue(): Record<string, unknown>; patchValue(v: Record<string, unknown>): void };
    showAll: () => boolean;
    toggleShowAll(): void;
    ngOnInit(): void;
  };

  const create = (): { fixture: ComponentFixture<SignupListPage>; p: Probe } => {
    const fixture = TestBed.createComponent(SignupListPage);
    return { fixture, p: fixture.componentInstance as unknown as Probe };
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    });
  });

  it('離開前勾的「範圍」與「顯示完整表格」，回來時仍是勾的', () => {
    const first = create();
    first.p.ngOnInit();
    first.p.form.patchValue({ isScope: true, year: 113, searchKey: '王' });
    first.p.toggleShowAll();
    expect(first.p.showAll()).toBe(true);
    first.fixture.destroy(); // 切到其他功能 → 元件銷毀

    const back = create();
    back.p.ngOnInit();
    const v = back.p.form.getRawValue();
    expect(v['isScope']).toBe(true);
    expect(v['year']).toBe(113);
    expect(v['searchKey']).toBe('王');
    expect(back.p.showAll()).toBe(true);
  });
});

/**
 * 工具列「新增報名」的代入規則（2026-08-05）：對齊舊 SignupForm.btnNew_Click:76-90——
 * 有選取列就代入該列。⚠️ 刻意偏離舊 `selectedcount > 0 → SelectedRows[0]`：新系統多選是常態
 * （批次列印動輒數百列），故收斂為「恰好 1 筆才代入」，與右鍵「代入新增」同規則。
 */
describe('SignupListPage（工具列「新增報名」的代入規則）', () => {
  type OverlayState = { signupId: string | null; fromSignupId: string | null } | null;
  type Probe = {
    results: WritableSignal<SignupListItem[]>;
    toggleRow(item: SignupListItem, event: MouseEvent | null, index: number): void;
    openCreateOverlay(): void;
    editOverlay: () => OverlayState;
  };

  const row = (id: string): SignupListItem => ({
    id, year: 113, ceremonyCategoryId: 'c1', ceremonyTitle: null, signupType: 1,
    numberTitle: null, number: null, fee: null, employee: null, employeeType: 1,
    believerId: `b-${id}`, name: id, hallName: null, phone: null, isFixedNumber: false,
    livingNames: [], deadNames: [],
    mailCity: null, mailZone: null, mailZipcode: null, mailAddress: null,
    textCity: null, textZone: null, textZipcode: null, textAddress: null,
    prepayYear: null, prepayCeremonyCategoryId: null, prepayCeremonyTitle: null,
    remark: null, adminName: null, createDate: null,
  });

  const rows = ['r0', 'r1', 'r2'].map(row);
  const click = (): MouseEvent => new MouseEvent('click', { cancelable: true });

  let p: Probe;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    });
    p = TestBed.createComponent(SignupListPage).componentInstance as unknown as Probe;
    p.results.set(rows);
  });

  it('未選任何列 → 空白新增', () => {
    p.openCreateOverlay();
    expect(p.editOverlay()).toEqual({ signupId: null, fromSignupId: null });
  });

  it('恰好選 1 筆 → 代入該列（等同右鍵「代入新增」）', () => {
    p.toggleRow(rows[1], click(), 1);
    p.openCreateOverlay();
    expect(p.editOverlay()?.fromSignupId).toBe('r1');
    expect(p.editOverlay()?.signupId).toBeNull();
  });

  it('選 2 筆以上 → 不代入（回歸鎖：刻意偏離舊 SelectedRows[0]）', () => {
    p.toggleRow(rows[0], click(), 0);
    p.toggleRow(rows[1], click(), 1);
    p.openCreateOverlay();
    expect(p.editOverlay()?.fromSignupId).toBeNull();
  });

  it('選取的列已不在目前結果內 → 不代入（selectedRows() 以 results 過濾）', () => {
    p.toggleRow(rows[1], click(), 1);
    p.results.set([row('x0'), row('x1')]); // 重新搜尋換掉結果
    p.openCreateOverlay();
    expect(p.editOverlay()?.fromSignupId).toBeNull();
  });
});

/**
 * 右鍵選單「兩張資料卡互斥停用」的行為鎖（2026-08-15 使用者定案，
 * 見 docs/business-rules-implicit.md §16.2）：普桌選取時只能印普桌資料卡、
 * 非普桌只能印一般資料卡；**混選兩者皆可**，其餘報表（含「列印普桌」牌位）永不受限。
 */
describe('SignupListPage（右鍵：資料卡依報名類型互斥停用）', () => {
  type MenuCtx = { selectedRows: SignupListItem[]; triggerRow: SignupListItem };
  type Probe = {
    results: WritableSignal<SignupListItem[]>;
    toggleRow(item: SignupListItem, event: MouseEvent | null, index: number): void;
    buildMenuItems(): ContextMenuItem<MenuCtx>[];
  };

  const row = (id: string, signupType: number): SignupListItem => ({
    id, year: 113, ceremonyCategoryId: 'c1', ceremonyTitle: null, signupType,
    numberTitle: null, number: null, fee: null, employee: null, employeeType: 1,
    believerId: `b-${id}`, name: id, hallName: null, phone: null, isFixedNumber: false,
    livingNames: [], deadNames: [],
    mailCity: null, mailZone: null, mailZipcode: null, mailAddress: null,
    textCity: null, textZone: null, textZipcode: null, textAddress: null,
    prepayYear: null, prepayCeremonyCategoryId: null, prepayCeremonyTitle: null,
    remark: null, adminName: null, createDate: null,
  });

  const general = row('g1', 1); // 一般報名
  const temple = row('t1', 2); // 寺方
  const worship = row('w1', 4); // 普桌
  const worship2 = row('w2', 4);

  /** 直接餵 context 給 resolveItems（純函式、免 TestBed），取指定 id 的解析結果。 */
  const entry = (selectedRows: SignupListItem[], id: string) => {
    const ctx: MenuCtx = { selectedRows, triggerRow: selectedRows[0] ?? general };
    const found = resolveItems(p.buildMenuItems(), ctx).find((e) => e.item.id === id);
    if (!found) throw new Error(`menu item ${id} 不存在`);
    return found;
  };

  let p: Probe;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    });
    p = TestBed.createComponent(SignupListPage).componentInstance as unknown as Probe;
  });

  it('全部都是普桌 → 停用「列印資料卡」、啟用「列印普桌資料卡」', () => {
    const rows = [worship, worship2];
    expect(entry(rows, 'print-datacard').enabled).toBe(false);
    expect(entry(rows, 'print-datacard').disabledReason).toBe('普桌報名請改印「普桌資料卡」');
    expect(entry(rows, 'print-worshipcard').enabled).toBe(true);
  });

  it('全部都不是普桌 → 停用「列印普桌資料卡」、啟用「列印資料卡」', () => {
    const rows = [general, temple];
    expect(entry(rows, 'print-worshipcard').enabled).toBe(false);
    expect(entry(rows, 'print-worshipcard').disabledReason).toBe('非普桌報名請改印「資料卡」');
    expect(entry(rows, 'print-datacard').enabled).toBe(true);
  });

  it('混選普桌＋非普桌 → 兩張資料卡都可印（不擋使用者的明示選擇）', () => {
    const rows = [worship, general];
    expect(entry(rows, 'print-datacard').enabled).toBe(true);
    expect(entry(rows, 'print-worshipcard').enabled).toBe(true);
  });

  it('未選取任何列 → 兩張都停用，理由仍是「請先選擇報名資料」', () => {
    for (const id of ['print-datacard', 'print-worshipcard']) {
      expect(entry([], id).enabled).toBe(false);
      expect(entry([], id).disabledReason).toBe('請先選擇報名資料');
    }
  });

  it('回歸鎖：普桌牌位／收據／薦牌／文牒不受型別影響（§16 選什麼印什麼）', () => {
    for (const rows of [[worship], [general], [worship, general]]) {
      for (const id of ['print-worship', 'print-receipt', 'print-tablet', 'print-text']) {
        expect(entry(rows, id).enabled).toBe(true);
      }
    }
  });
});
