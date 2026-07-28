import {
  ChangeDetectionStrategy,
  Component,
  HostListener,
  inject,
  input,
  output,
  ViewEncapsulation,
} from '@angular/core';
import { IconComponent } from '../icon/icon.component';
import { ConfirmDialogService } from '../confirm-dialog/confirm-dialog.service';

/**
 * 全系統 create/edit form 統一 shell。
 *
 * 使用方式：
 * ```html
 * <app-form-overlay
 *   title="編輯報名"
 *   [dirty]="form.dirty"
 *   (close)="onClose()"
 * >
 *   <app-signup-edit-form ... />
 *   <ng-container overlay-actions>
 *     <button class="btn" (click)="onClose()">取消</button>
 *     <button class="btn btn-primary" (click)="submit()">確認</button>
 *   </ng-container>
 * </app-form-overlay>
 * ```
 *
 * 行為：
 * - backdrop click / `Esc` / × button 觸發 `tryClose()`
 *   （`[dismissible]="false"` 時 backdrop 與 `Esc` 不再關閉，只剩 × 與表單自己的取消按鈕）
 * - 若 `dirty=true` 顯示「未儲存的變更」確認；否則直接 emit `close`
 * - panel 寬高 content-adaptive（max 92vw × 92vh）
 */
@Component({
  selector: 'app-form-overlay',
  imports: [IconComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  encapsulation: ViewEncapsulation.None,
  template: `
    <div class="overlay-backdrop" (click)="onBackdropClick()">
      <div
        class="overlay-panel"
        role="dialog"
        aria-modal="true"
        [attr.aria-label]="title()"
        [style.width]="width()"
        (click)="$event.stopPropagation()"
      >
        <div class="overlay-header">
          <h3>{{ title() }}</h3>
          <button
            type="button"
            class="overlay-close-btn"
            aria-label="關閉"
            (click)="tryClose()"
          >
            <app-icon name="close" [size]="20" />
          </button>
        </div>
        <div class="overlay-body">
          <ng-content />
        </div>
        @if (showActions()) {
          <div class="overlay-actions">
            <ng-content select="[overlay-actions]" />
          </div>
        }
      </div>
    </div>
  `,
})
export class FormOverlayComponent {
  private readonly confirmDialog = inject(ConfirmDialogService);

  readonly title = input.required<string>();
  readonly dirty = input<boolean>(false);
  /**
   * panel 寬度（CSS 長度字串，如 `'900px'`）。不給＝維持 content-adaptive。
   * 內容含寬表格時務必給值：panel 預設是「有多寬長多寬（上限 92vw）」，
   * 一張 19 欄的表就能把整個視窗撐滿。全域 `max-width: 92vw` 仍在，小視窗會自動縮。
   */
  readonly width = input<string | null>(null);
  /**
   * 是否顯示 panel 底部的 actions footer（預設顯示）。
   * 給 `false` 用在「按鈕由內層表單自己排版」的場合——例如報名表單把
   * 列印資料卡/取消/確認移到備註下方（2026-07-28）；此時 footer 若留著會是一條
   * 只有 padding + 上框線的空灰帶。
   */
  readonly showActions = input<boolean>(true);
  /**
   * 是否允許「點 backdrop / 按 Esc」關閉（預設允許）。
   * 給 `false` 用在「誤關代價高」的表單——例如報名維護的新增／編輯報名
   * （2026-07-28 客訴：打字時手滑點到旁邊整張表就沒了），此時只有右上 × 與
   * 表單自己的「取消」按鈕能關；兩者一樣走 `tryClose()`，dirty 仍會先問。
   */
  readonly dismissible = input<boolean>(true);
  readonly close = output<void>();

  @HostListener('document:keydown.escape')
  protected onEscape(): void {
    if (!this.dismissible()) return;
    void this.tryClose();
  }

  protected onBackdropClick(): void {
    if (!this.dismissible()) return;
    void this.tryClose();
  }

  protected async tryClose(): Promise<void> {
    if (!this.dirty()) {
      this.close.emit();
      return;
    }
    const ok = await this.confirmDialog.ask({
      title: '未儲存的變更',
      message: '表單尚未儲存，確定要離開嗎？',
      confirmLabel: '離開',
      danger: true,
    });
    if (ok) this.close.emit();
  }
}
