import { Component } from '@angular/core';
import { TestBed, type ComponentFixture } from '@angular/core/testing';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { NarrowInputDirective } from './narrow-input.directive';

@Component({
  imports: [ReactiveFormsModule, NarrowInputDirective],
  template: `<input type="text" appNarrowInput [formControl]="phone" />`,
})
class HostComponent {
  readonly phone = new FormControl('', { nonNullable: true });
}

/** 電話欄不管用全形或半形輸入法打，control 值與畫面一律半形（2026-08-06 使用者指定）。 */
describe('NarrowInputDirective（電話全形轉半形）', () => {
  let fixture: ComponentFixture<HostComponent>;
  let input: HTMLInputElement;

  beforeEach(() => {
    fixture = TestBed.createComponent(HostComponent);
    fixture.detectChanges();
    input = fixture.nativeElement.querySelector('input') as HTMLInputElement;
  });

  /** 模擬非 IME 直接輸入 */
  function type(text: string): void {
    input.value = text;
    input.dispatchEvent(new Event('input'));
  }

  /** 模擬中文輸入法組字：組字期間不清洗，compositionend 才轉 */
  function compose(text: string): void {
    input.dispatchEvent(new Event('compositionstart'));
    input.value = text;
    input.dispatchEvent(new Event('input'));
    input.dispatchEvent(new Event('compositionend'));
  }

  it('全形數字轉半形', () => {
    type('０９１２３４５６７８');
    expect(input.value).toBe('0912345678');
    expect(fixture.componentInstance.phone.value).toBe('0912345678');
  });

  it('全形符號（分隔號 / 括號 / 井號）一併轉半形', () => {
    type('（０４）２２３４－５６７８＃１２３');
    expect(fixture.componentInstance.phone.value).toBe('(04)2234-5678#123');
  });

  it('全形空白轉半形空白', () => {
    type('０４　２２３４５６７８');
    expect(fixture.componentInstance.phone.value).toBe('04 22345678');
  });

  it('半形輸入原樣保留', () => {
    type('04-22345678');
    expect(fixture.componentInstance.phone.value).toBe('04-22345678');
  });

  it('IME 組字結束後才轉半形', () => {
    compose('０９１２');
    expect(input.value).toBe('0912');
    expect(fixture.componentInstance.phone.value).toBe('0912');
  });

  it('清空欄位得到空字串', () => {
    type('０９１２');
    type('');
    expect(fixture.componentInstance.phone.value).toBe('');
  });

  it('轉換後游標維持原位置（全形/半形 1:1 對應）', () => {
    input.value = '０９１２３';
    input.setSelectionRange(3, 3);
    input.dispatchEvent(new Event('input'));
    expect(input.value).toBe('09123');
    expect(input.selectionStart).toBe(3);
  });

  it('setValue 寫入的值原樣顯示，不動 control 值', () => {
    fixture.componentInstance.phone.setValue('０９１２');
    fixture.detectChanges();
    expect(input.value).toBe('０９１２');
    expect(fixture.componentInstance.phone.dirty).toBe(false);
  });
});
