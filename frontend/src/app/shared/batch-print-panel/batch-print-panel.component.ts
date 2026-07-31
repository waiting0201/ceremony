import {
  ChangeDetectionStrategy,
  Component,
  computed,
  input,
  output,
  ViewEncapsulation,
} from '@angular/core';
import type { ChunkedPrintRun } from '../../core/reports/chunked-print.service';
import {
  printedCount,
  segmentLabel,
  type PrintSegment,
  type SegmentStatus,
} from '../../core/reports/chunked-print.types';

const STATUS_TEXT: Record<SegmentStatus, string> = {
  pending: '待印',
  rendering: '準備中',
  printing: '送印中',
  // 刻意不叫「完成」：spooler 收下了不代表紙上有字，使用者要能對照實體紙張再決定重印
  printed: '已送印',
  failed: '失敗',
  canceled: '已略過',
};

/**
 * 大量列印的分段進度面板。
 *
 * 為什麼不是用既有的 ProgressOverlay：那支是「單一工作 + 一條進度條」，而大量列印要顯示的是
 * 每一段的獨立狀態與各自的重印鈕——5000 筆印 3 小時，中途卡紙時使用者需要知道「哪一段要重印」，
 * 一條總進度條給不出這個資訊。決策見 docs/blueprints/chunked-batch-printing.md
 */
@Component({
  selector: 'app-batch-print-panel',
  changeDetection: ChangeDetectionStrategy.OnPush,
  encapsulation: ViewEncapsulation.None,
  template: `
    <div class="bpp-backdrop">
      <div class="bpp-panel" role="dialog" aria-modal="true" aria-labelledby="bpp-title">
        <div class="bpp-header">
          <h3 id="bpp-title">列印{{ s().reportLabel }}</h3>
          <span class="bpp-sub">{{ headline() }}</span>
        </div>

        <div class="bpp-progress" role="progressbar" [attr.aria-valuenow]="printed()"
             aria-valuemin="0" [attr.aria-valuemax]="s().total">
          <div class="bpp-bar"><span [style.width.%]="percent()"></span></div>
          <div class="bpp-counts">
            <span>已送印 {{ printed() }} / {{ s().total }} 筆</span>
            <span>{{ percent() }}%</span>
          </div>
        </div>

        @if (s().errorMessage) {
          <p class="bpp-error">{{ s().errorMessage }}</p>
        }

        <ul class="bpp-segments">
          @for (seg of s().segments; track seg.index) {
            <li class="bpp-seg" [attr.data-status]="seg.status">
              <span class="bpp-seg-label">{{ label(seg) }}</span>
              <span class="bpp-seg-status">
                {{ statusText(seg.status) }}
                @if (seg.status === 'rendering' && seg.rendered > 0) {
                  （{{ seg.rendered }}/{{ seg.signupIds.length }}）
                }
              </span>
              @if (seg.errorMessage) {
                <span class="bpp-seg-error">{{ seg.errorMessage }}</span>
              }
              <button
                type="button"
                class="btn bpp-reprint"
                [disabled]="!canReprint()"
                (click)="run().reprint(seg.index)"
              >
                重印
              </button>
            </li>
          }
        </ul>

        <p class="bpp-hint">
          「已送印」代表已交給印表機佇列。若某一段卡紙或印歪，可單獨重印該段，不必整批重來。
        </p>

        <div class="bpp-actions">
          @if (s().phase === 'running') {
            <button type="button" class="btn" (click)="run().pause()">暫停</button>
            <button type="button" class="btn" (click)="run().cancel()">停止</button>
          } @else if (s().phase === 'paused') {
            <button type="button" class="btn" (click)="run().cancel()">停止</button>
            <button type="button" class="btn btn-primary" (click)="run().resume()">繼續</button>
          } @else {
            <button type="button" class="btn btn-primary" (click)="close.emit()">關閉</button>
          }
        </div>
      </div>
    </div>
  `,
  styles: `
    .bpp-backdrop {
      position: fixed;
      inset: 0;
      background: rgba(44, 42, 38, 0.42);
      z-index: 1100;
      display: flex;
      align-items: center;
      justify-content: center;
    }
    .bpp-panel {
      background: var(--c-surface);
      border: 1px solid var(--c-border);
      border-radius: 6px;
      box-shadow: 0 12px 40px rgba(44, 42, 38, 0.22);
      width: min(560px, 94vw);
      max-height: 88vh;
      display: flex;
      flex-direction: column;
      font-family: var(--font-ui);
      color: var(--c-text-primary);
    }
    .bpp-header {
      padding: var(--space-md) var(--space-lg);
      border-bottom: 1px solid var(--c-border-soft);
      display: flex;
      align-items: baseline;
      justify-content: space-between;
      gap: var(--space-md);
      h3 {
        margin: 0;
        font-size: var(--font-size-lg);
        font-weight: 600;
      }
    }
    .bpp-sub {
      font-size: var(--font-size-sm);
      color: var(--c-text-secondary);
    }
    .bpp-progress {
      padding: var(--space-md) var(--space-lg) 0;
    }
    .bpp-bar {
      height: 8px;
      border-radius: 4px;
      background: var(--c-border-soft);
      overflow: hidden;
      span {
        display: block;
        height: 100%;
        background: var(--c-primary-strong);
        transition: width 200ms ease-out;
      }
    }
    .bpp-counts {
      display: flex;
      justify-content: space-between;
      margin-top: var(--space-xs);
      font-size: var(--font-size-sm);
      color: var(--c-text-secondary);
      font-variant-numeric: tabular-nums;
    }
    .bpp-error {
      margin: var(--space-md) var(--space-lg) 0;
      color: var(--c-danger, #b3261e);
      font-size: var(--font-size-sm);
    }
    .bpp-segments {
      flex: 1;
      min-height: 0;
      overflow-y: auto;
      margin: var(--space-md) 0 0;
      padding: 0 var(--space-lg);
      list-style: none;
    }
    .bpp-seg {
      display: grid;
      grid-template-columns: minmax(0, 1fr) auto auto;
      align-items: center;
      gap: var(--space-sm);
      padding: 0.4rem 0;
      border-bottom: 1px solid var(--c-border-soft);
      font-size: var(--font-size-sm);
      font-variant-numeric: tabular-nums;
    }
    .bpp-seg-status {
      color: var(--c-text-secondary);
      white-space: nowrap;
    }
    .bpp-seg[data-status='printed'] .bpp-seg-status {
      color: var(--c-primary-strong);
    }
    .bpp-seg[data-status='failed'] .bpp-seg-status {
      color: var(--c-danger, #b3261e);
    }
    .bpp-seg-error {
      grid-column: 1 / -1;
      color: var(--c-danger, #b3261e);
    }
    .bpp-reprint {
      padding: 0.15rem 0.6rem;
      font-size: var(--font-size-sm);
    }
    .bpp-hint {
      margin: 0;
      padding: var(--space-md) var(--space-lg) 0;
      font-size: var(--font-size-sm);
      color: var(--c-text-secondary);
      line-height: 1.5;
    }
    .bpp-actions {
      display: flex;
      justify-content: flex-end;
      gap: var(--space-sm);
      padding: var(--space-md) var(--space-lg);
      margin-top: var(--space-md);
      background: var(--c-bg);
      border-top: 1px solid var(--c-border-soft);
      border-radius: 0 0 6px 6px;
    }
  `,
})
export class BatchPrintPanelComponent {
  readonly run = input.required<ChunkedPrintRun>();
  readonly close = output<void>();

  protected readonly s = computed(() => this.run().state());
  protected readonly printed = computed(() => printedCount(this.s().segments));
  protected readonly percent = computed(() => {
    const total = this.s().total;
    return total > 0 ? Math.round((this.printed() / total) * 100) : 0;
  });

  /** 主迴圈跑著時不能重印：兩邊會搶同一台印表機，順序也會亂。 */
  protected readonly canReprint = computed(() => this.s().phase !== 'running');

  protected readonly headline = computed(() => {
    const s = this.s();
    const segs = s.segments.length;
    switch (s.phase) {
      case 'running':
        return `共 ${s.total} 筆，分 ${segs} 段`;
      case 'paused':
        return '已暫停（目前這一段仍會印完）';
      case 'canceled':
        return '已停止';
      default: {
        const failed = s.segments.filter((x) => x.status === 'failed').length;
        return failed ? `${failed} 段失敗，可個別重印` : `全部 ${segs} 段已送印`;
      }
    }
  });

  protected label(seg: PrintSegment): string {
    return segmentLabel(seg);
  }

  protected statusText(status: SegmentStatus): string {
    return STATUS_TEXT[status];
  }
}
