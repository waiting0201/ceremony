import { TestBed, type ComponentFixture } from '@angular/core/testing';
import { ConfirmDialogComponent } from './confirm-dialog.component';
import type { ConfirmDialogConfig } from './confirm-dialog.types';

/**
 * 對話框的數字輸入格（2026-08-21，給報名列表右鍵「移動插入至…」用）：
 * 預填初值、值非正整數時確認鈕停用、Enter 等同按確認。
 * 沒帶 numberInput 的一般確認框完全不受影響（回歸鎖）。
 */
describe('ConfirmDialogComponent（數字輸入格）', () => {
  let fixture: ComponentFixture<ConfirmDialogComponent>;

  const create = (config: ConfirmDialogConfig): void => {
    fixture = TestBed.createComponent(ConfirmDialogComponent);
    fixture.componentRef.setInput('config', config);
    fixture.detectChanges();
  };

  const withInput = (initial: number | null = 5): ConfirmDialogConfig => ({
    title: '移動插入至…',
    message: '將 No-5 移到指定編號',
    numberInput: { label: '目標編號', initial, min: 1 },
  });

  const box = (): HTMLInputElement =>
    fixture.nativeElement.querySelector('input[type="number"]') as HTMLInputElement;
  const confirmBtn = (): HTMLButtonElement =>
    fixture.nativeElement.querySelectorAll('button')[1] as HTMLButtonElement;

  const type = (text: string): void => {
    box().value = text;
    box().dispatchEvent(new Event('input'));
    fixture.detectChanges();
  };

  it('沒帶 numberInput → 不渲染輸入格，確認鈕可按（一般確認框不受影響）', () => {
    create({ title: '刪除', message: '確定？' });
    expect(box()).toBeNull();
    expect(confirmBtn().disabled).toBe(false);
  });

  it('帶 numberInput → 預填初值，確認送出該值', () => {
    create(withInput(5));
    expect(box().value).toBe('5');

    const seen: number[] = [];
    fixture.componentInstance.confirm.subscribe(() => seen.push(fixture.componentInstance.value()!));
    type('2');
    confirmBtn().click();

    expect(seen).toEqual([2]);
  });

  it.each([
    ['', '空白'],
    ['0', '零'],
    ['-3', '負數'],
    ['2.5', '小數'],
  ])('值為 %s（%s）→ 確認鈕停用', (raw) => {
    create(withInput(5));
    type(raw);
    expect(confirmBtn().disabled).toBe(true);
  });

  it('Enter = 按確認；值不合法時 Enter 不送出', () => {
    create(withInput(5));
    const seen: number[] = [];
    fixture.componentInstance.confirm.subscribe(() => seen.push(fixture.componentInstance.value()!));

    type('');
    box().dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter' }));
    expect(seen).toHaveLength(0);

    type('3');
    box().dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter' }));
    expect(seen).toEqual([3]);
  });

  it('初值為 null → 一開始就停用（沒填不能送）', () => {
    create(withInput(null));
    expect(box().value).toBe('');
    expect(confirmBtn().disabled).toBe(true);
  });
});
