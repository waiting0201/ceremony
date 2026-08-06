import { Directive, ElementRef, forwardRef, HostListener, inject } from '@angular/core';
import { ControlValueAccessor, NG_VALUE_ACCESSOR } from '@angular/forms';

/** 全形 → 半形（U+FF01–U+FF5E 的 ASCII 全形字 + U+3000 全形空白）。 */
export function toNarrow(s: string): string {
  return s
    .replace(/[！-～]/g, (c) => String.fromCharCode(c.charCodeAt(0) - 0xfee0))
    .replace(/　/g, ' ');
}

// 電話欄用 <input type="text" appNarrowInput>：不管使用者用注音/全形輸入法打「０９１２－３４５」
// 還是半形打，欄位值一律轉半形，畫面即時看得到轉換結果。
// 後端 ToNarrow（CreateSignupHandler / UpdateSignupHandler / BelieverWriteValidator）已做同樣轉換，
// 這裡是把同一規則提前到輸入當下，避免「存檔後才變半形」與搜尋條件全形撈不到資料。
// 只用於電話：姓名/地址不可套用，使用者會刻意用全形空格排直書版面（見 docs/gotchas.md「姓名中間空格」）。
@Directive({
  selector: 'input[appNarrowInput]',
  standalone: true,
  providers: [
    {
      provide: NG_VALUE_ACCESSOR,
      useExisting: forwardRef(() => NarrowInputDirective),
      multi: true,
    },
  ],
})
export class NarrowInputDirective implements ControlValueAccessor {
  private readonly el = inject<ElementRef<HTMLInputElement>>(ElementRef).nativeElement;
  private onChange: (value: string) => void = () => {};
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
    const clean = toNarrow(raw);
    if (clean !== raw) {
      // 全形→半形是 1:1 字元對應，長度不變，游標位置可原樣還原
      const caret = this.el.selectionStart;
      this.el.value = clean;
      if (caret !== null) this.el.setSelectionRange(caret, caret);
    }
    this.onChange(clean);
  }

  // 顯示值原樣寫入、不在此轉半形：轉了會讓 DOM 與 control 值不一致，
  // 又不能在 writeValue 內呼叫 onChange（會把剛載入的表單標成 dirty）。
  // 舊資料若殘留全形，使用者一動就轉，沒動也有後端 ToNarrow 兜底。
  writeValue(value: string | null | undefined): void {
    this.el.value = value == null ? '' : String(value);
  }

  registerOnChange(fn: (value: string) => void): void {
    this.onChange = fn;
  }

  registerOnTouched(fn: () => void): void {
    this.onTouched = fn;
  }

  setDisabledState(isDisabled: boolean): void {
    this.el.disabled = isDisabled;
  }
}
