import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import type { WritableSignal } from '@angular/core';
import { provideRouter } from '@angular/router';
import type { SignupListItem } from '../../core/api/signups/signup.models';
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
