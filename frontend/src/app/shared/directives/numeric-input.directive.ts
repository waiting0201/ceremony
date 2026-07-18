import { Directive, ElementRef, forwardRef, HostListener, inject } from '@angular/core';
import { ControlValueAccessor, NG_VALUE_ACCESSOR } from '@angular/forms';

// 數字欄一律用 <input type="text" inputmode="numeric" appNumericInput>，不用 type="number"：
// Chromium 的 number input 不支援 IME 組字，中文輸入法（注音/全形）打數字會被整段丟棄
// 且畫面毫無回饋（客訴「批次列印起迄沒辦法手動輸入」根因）。text input 讓組字過程可見，
// 本 directive 於組字結束後把全形數字轉半形、濾掉非數字，control 值維持 number | null。
@Directive({
  selector: 'input[appNumericInput]',
  standalone: true,
  providers: [
    {
      provide: NG_VALUE_ACCESSOR,
      useExisting: forwardRef(() => NumericInputDirective),
      multi: true,
    },
  ],
})
export class NumericInputDirective implements ControlValueAccessor {
  private readonly el = inject<ElementRef<HTMLInputElement>>(ElementRef).nativeElement;
  private onChange: (value: number | null) => void = () => {};
  private onTouched: () => void = () => {};
  private composing = false;

  @HostListener('compositionstart')
  protected onCompositionStart(): void {
    this.composing = true;
  }

  @HostListener('compositionend')
  protected onCompositionEnd(): void {
    this.composing = false;
    this.sanitize();
  }

  @HostListener('input')
  protected onInput(): void {
    // 組字中不清洗，否則會打斷 IME 組字；等 compositionend 一次處理
    if (!this.composing) this.sanitize();
  }

  @HostListener('blur')
  protected onBlur(): void {
    this.onTouched();
  }

  private sanitize(): void {
    const raw = this.el.value;
    const clean = raw
      .replace(/[０-９]/g, (d) => String.fromCharCode(d.charCodeAt(0) - 0xfee0))
      .replace(/\D/g, '');
    if (clean !== raw) {
      this.el.value = clean;
    }
    this.onChange(clean === '' ? null : Number(clean));
  }

  writeValue(value: number | null | undefined): void {
    this.el.value = value == null ? '' : String(value);
  }

  registerOnChange(fn: (value: number | null) => void): void {
    this.onChange = fn;
  }

  registerOnTouched(fn: () => void): void {
    this.onTouched = fn;
  }

  setDisabledState(isDisabled: boolean): void {
    this.el.disabled = isDisabled;
  }
}
