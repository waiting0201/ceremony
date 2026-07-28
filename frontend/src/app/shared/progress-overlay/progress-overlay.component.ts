import {
  ChangeDetectionStrategy,
  Component,
  computed,
  HostListener,
  input,
  output,
  ViewEncapsulation,
} from '@angular/core';
import type { ProgressOverlayConfig } from './progress-overlay.types';

/**
 * 阻擋畫面的置中進度 overlay（批次列印用）。
 *
 * 刻意不做 backdrop 點擊關閉：這裡擋的是長時間工作，誤觸中斷的代價比關不掉高。
 * 只能按「取消」或 Esc。設計規格見 docs/design/visual-design.md「進度 Overlay」。
 */
@Component({
  selector: 'app-progress-overlay',
  changeDetection: ChangeDetectionStrategy.OnPush,
  encapsulation: ViewEncapsulation.None,
  template: `
    <div class="progress-backdrop">
      <div
        class="progress-dialog"
        role="dialog"
        aria-modal="true"
        aria-labelledby="progress-title"
      >
        <h3 id="progress-title" class="progress-title">{{ config().title }}</h3>
        @if (config().detail) {
          <p class="progress-detail">{{ config().detail }}</p>
        }

        <div class="progress-percent">{{ percent() }}%</div>

        <div
          class="progress-track"
          role="progressbar"
          [attr.aria-valuenow]="percent()"
          aria-valuemin="0"
          aria-valuemax="100"
        >
          <div class="progress-fill" [style.width.%]="percent()"></div>
        </div>

        <p class="progress-count" aria-live="polite">
          {{ config().completed }} / {{ config().total }} 筆
        </p>

        @if (config().note) {
          <p class="progress-note">{{ config().note }}</p>
        }

        @if (config().cancelable !== false) {
          <div class="progress-actions">
            <button type="button" class="btn" (click)="cancel.emit()">
              {{ config().cancelLabel ?? '取消' }}
            </button>
          </div>
        }
      </div>
    </div>
  `,
  styles: `
    /* z-index 1100：高於 confirm-dialog(1000) 與 form-overlay(900)，
       進行中的工作不該被任何東西蓋住。層級表見 docs/design/visual-design.md。 */
    .progress-backdrop {
      position: fixed;
      inset: 0;
      background: rgba(44, 42, 38, 0.42);
      z-index: 1100;
      display: flex;
      align-items: center;
      justify-content: center;
      animation: fadeIn 120ms ease-out;
    }
    .progress-dialog {
      background: var(--c-surface);
      border-radius: 6px;
      border: 1px solid var(--c-border);
      box-shadow: 0 12px 40px rgba(44, 42, 38, 0.22);
      width: min(420px, 92vw);
      padding: var(--space-xl) var(--space-lg);
      font-family: var(--font-ui);
      color: var(--c-text-primary);
      text-align: center;
      animation: pop 140ms ease-out;
    }
    .progress-title {
      margin: 0;
      font-size: var(--font-size-lg);
      font-weight: 600;
    }
    .progress-detail {
      margin: var(--space-xs) 0 0;
      font-size: var(--font-size-sm);
      color: var(--c-text-secondary);
    }
    .progress-percent {
      margin-top: var(--space-lg);
      font-size: var(--font-size-xl);
      font-weight: 600;
      color: var(--c-primary-strong);
      font-variant-numeric: tabular-nums;
    }
    .progress-track {
      margin-top: var(--space-sm);
      height: 8px;
      border-radius: 4px;
      background: var(--c-border-soft);
      overflow: hidden;
    }
    .progress-fill {
      height: 100%;
      background: var(--c-primary-strong);
      border-radius: 4px;
      /* 輪詢每 250ms 一次，補上這段過場才不會看到跳格 */
      transition: width 200ms ease-out;
    }
    .progress-count {
      margin: var(--space-sm) 0 0;
      font-size: var(--font-size-sm);
      color: var(--c-text-secondary);
      font-variant-numeric: tabular-nums;
    }
    .progress-note {
      margin: var(--space-xs) 0 0;
      font-size: var(--font-size-sm);
      color: var(--c-text-secondary);
    }
    .progress-actions {
      margin-top: var(--space-lg);
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
export class ProgressOverlayComponent {
  readonly config = input.required<ProgressOverlayConfig>();
  readonly cancel = output<void>();

  protected readonly percent = computed(() => {
    const { total, completed } = this.config();
    if (total <= 0) return 0;
    return Math.min(100, Math.round((completed / total) * 100));
  });

  @HostListener('document:keydown.escape')
  protected onEscape(): void {
    if (this.config().cancelable !== false) this.cancel.emit();
  }
}
