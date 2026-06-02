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
    .confirm-body {
      padding: var(--space-lg);
      font-size: var(--font-size-base);
      line-height: 1.55;
      p { margin: 0; white-space: pre-wrap; }
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
