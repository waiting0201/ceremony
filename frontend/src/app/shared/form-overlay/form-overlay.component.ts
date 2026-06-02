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
 * - 若 `dirty=true` 顯示「未儲存的變更」確認；否則直接 emit `close`
 * - panel 寬高 content-adaptive（max 92vw × 92vh）
 */
@Component({
  selector: 'app-form-overlay',
  imports: [IconComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  encapsulation: ViewEncapsulation.None,
  template: `
    <div class="overlay-backdrop" (click)="tryClose()">
      <div
        class="overlay-panel"
        role="dialog"
        aria-modal="true"
        [attr.aria-label]="title()"
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
        <div class="overlay-actions">
          <ng-content select="[overlay-actions]" />
        </div>
      </div>
    </div>
  `,
})
export class FormOverlayComponent {
  private readonly confirmDialog = inject(ConfirmDialogService);

  readonly title = input.required<string>();
  readonly dirty = input<boolean>(false);
  readonly close = output<void>();

  @HostListener('document:keydown.escape')
  protected onEscape(): void {
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
