import {
  ChangeDetectionStrategy,
  Component,
  HostListener,
  input,
  output,
  ViewEncapsulation,
} from '@angular/core';
import type { ConfirmDialogConfig } from './confirm-dialog.types';

@Component({
  selector: 'app-confirm-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  encapsulation: ViewEncapsulation.None,
  template: `
    <div class="confirm-backdrop" (click)="cancel.emit()">
      <div
        class="confirm-dialog"
        [class.is-emphasis]="config().emphasis"
        role="dialog"
        aria-modal="true"
        [attr.aria-labelledby]="'confirm-title'"
        (click)="$event.stopPropagation()"
      >
        <div class="confirm-header">
          <h3 id="confirm-title">{{ config().title }}</h3>
        </div>
        <div class="confirm-body">
          <p>{{ config().message }}</p>
        </div>
        <div class="confirm-actions">
          @if (!config().hideCancel) {
            <button type="button" class="btn" (click)="cancel.emit()">
              {{ config().cancelLabel ?? '取消' }}
            </button>
          }
          <button
            type="button"
            class="btn"
            [class.btn-danger]="config().danger"
            [class.btn-primary]="!config().danger"
            [class.btn-wide]="config().emphasis"
            (click)="confirm.emit()"
          >
            {{ config().confirmLabel ?? '確認' }}
          </button>
        </div>
      </div>
    </div>
  `,
  styles: `
    .confirm-backdrop {
      position: fixed;
      inset: 0;
      background: rgba(44, 42, 38, 0.42);
      z-index: 1000;
      display: flex;
      align-items: center;
      justify-content: center;
      animation: fadeIn 120ms ease-out;
    }
    .confirm-dialog {
      background: var(--c-surface);
      border-radius: 6px;
      border: 1px solid var(--c-border);
      box-shadow: 0 12px 40px rgba(44, 42, 38, 0.22);
      width: min(480px, 92vw);
      font-family: var(--font-ui);
      color: var(--c-text-primary);
      animation: pop 140ms ease-out;
    }
    .confirm-header {
      padding: var(--space-md) var(--space-lg);
      border-bottom: 1px solid var(--c-border-soft);
      h3 { margin: 0; font-size: var(--font-size-lg); font-weight: 600; }
    }
    /* 訊息字級＝側欄選單 .nav-label 的 --font-size-md（2026-07-28 使用者指定「提示字再大一點，
       跟左邊選單文字一樣大」）。這是所有 confirm/alert dialog 共用的 body，全站一起放大——
       dialog 是要人停下來讀的文字，沒有理由比選單還小。 */
    .confirm-body {
      padding: var(--space-lg);
      font-size: var(--font-size-md);
      line-height: 1.55;
      p { margin: 0; white-space: pre-wrap; }
    }
    /* 強調樣式（config.emphasis）：訊息 20px（2026-07-29 使用者指定「報名成功的訊息字體加大至
       20px」），確認鈕的加寬由 .btn-wide 負責。只有帶 emphasis 的提示會變大，一般確認框不受影響。 */
    .confirm-dialog.is-emphasis .confirm-body {
      font-size: 20px;
    }
    .confirm-actions {
      display: flex;
      justify-content: flex-end;
      gap: var(--space-sm);
      padding: var(--space-md) var(--space-lg);
      background: var(--c-bg);
      border-top: 1px solid var(--c-border-soft);
      border-radius: 0 0 6px 6px;
    }
    @keyframes fadeIn {
      from { opacity: 0; }
      to { opacity: 1; }
    }
    @keyframes pop {
      from { transform: translateY(8px) scale(0.98); opacity: 0; }
      to { transform: translateY(0) scale(1); opacity: 1; }
    }
  `,
})
export class ConfirmDialogComponent {
  readonly config = input.required<ConfirmDialogConfig>();
  readonly confirm = output<void>();
  readonly cancel = output<void>();

  @HostListener('document:keydown.escape')
  protected onEscape(): void {
    this.cancel.emit();
  }
}
