import {
  ChangeDetectionStrategy,
  Component,
  HostListener,
  computed,
  input,
  output,
  signal,
  ViewEncapsulation,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import { inject } from '@angular/core';
import type { ScaleMode } from '../../core/platform/electron';
import type { PrintDialogConfig, PrintDialogResult } from './print-dialog.types';

/**
 * 列印對話框（自建，不是系統對話框）：左邊 PDF 預覽、右邊列印設定。
 *
 * 為什麼不用 Windows 原生列印對話框：Electron 的 print({silent:false}) 走原生 PrintDlgEx，
 * 我們傳的 pageSize / deviceName 進不去對話框初值 → 使用者又要手動調紙張，等於沒修。
 * 自己畫才能保證「紙張是對的、按下去就印對」。決策見 docs/blueprints/print-channel-electron.md。
 * 代價是 silent:true 送出去前使用者什麼都看不到，所以預覽必須內建在這裡（舊系統的
 * PrintPreviewDialog 等價物）。
 *
 * 紙張尺寸刻意做成唯讀：那是報表規格（座標系的基準），不是使用者選項。
 */
@Component({
  selector: 'app-print-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  encapsulation: ViewEncapsulation.None,
  imports: [FormsModule],
  template: `
    <div class="print-backdrop" (click)="cancel.emit()">
      <div
        class="print-dialog"
        [class.no-preview]="!hasPreview()"
        role="dialog"
        aria-modal="true"
        aria-labelledby="print-title"
        (click)="$event.stopPropagation()"
      >
        <div class="print-header">
          <h3 id="print-title">列印{{ config().reportLabel }}</h3>
          @if (config().detail) {
            <span class="print-detail">{{ config().detail }}</span>
          }
        </div>

        <div class="print-body">
          @if (previewSrc(); as src) {
            <div class="print-preview">
              <iframe class="pdf-frame" title="列印預覽" [src]="src"></iframe>
            </div>
          } @else if (config().previewNotice) {
            <p class="print-notice">{{ config().previewNotice }}</p>
          }

          <div class="print-settings">
            @if (!previewOnly()) {
              <label class="print-row">
                <span class="print-label">印表機</span>
                @if (config().printers.length) {
                  <select class="print-control" [(ngModel)]="device">
                    @for (p of config().printers; track p.name) {
                      <option [value]="p.name">
                        {{ p.displayName }}{{ p.isDefault ? '（預設）' : '' }}
                      </option>
                    }
                  </select>
                } @else {
                  <span class="print-static print-warn">找不到可用的印表機</span>
                }
              </label>

              <label class="print-row">
                <span class="print-label">份數</span>
                <input
                  class="print-control print-copies"
                  type="number"
                  min="1"
                  max="99"
                  [(ngModel)]="copies"
                />
              </label>
            }

            <div class="print-row">
              <span class="print-label">紙張</span>
              <span class="print-static">{{ config().paperLabel }}</span>
            </div>

            @if (!previewOnly()) {
              <label class="print-row">
                <span class="print-label">縮放</span>
                <select class="print-control" [(ngModel)]="scale">
                  <option value="actual">實際大小（100%）</option>
                  <option value="fit">符合紙張</option>
                </select>
              </label>

              <label class="print-remember">
                <input type="checkbox" [(ngModel)]="remember" />
                <span>記住這台印表機與設定，下次列印{{ config().reportLabel }}直接沿用</span>
              </label>
            }
          </div>
        </div>

        <div class="print-actions">
          <button type="button" class="btn" (click)="cancel.emit()">取消</button>
          <button type="button" class="btn btn-primary" [disabled]="!canSubmit()" (click)="submit()">
            {{ confirmLabel() }}
          </button>
        </div>
      </div>
    </div>
  `,
  styles: `
    .print-backdrop {
      position: fixed;
      inset: 0;
      background: rgba(44, 42, 38, 0.42);
      z-index: 1000;
      display: flex;
      align-items: center;
      justify-content: center;
      animation: fadeIn 120ms ease-out;
    }
    .print-dialog {
      background: var(--c-surface);
      border-radius: 6px;
      border: 1px solid var(--c-border);
      box-shadow: 0 12px 40px rgba(44, 42, 38, 0.22);
      width: min(1080px, 94vw);
      height: min(760px, 88vh);
      display: flex;
      flex-direction: column;
      font-family: var(--font-ui);
      color: var(--c-text-primary);
      animation: pop 140ms ease-out;
    }
    /* 無預覽（大檔 / 取檔失敗）退回原本的窄版，不要留一片空白 */
    .print-dialog.no-preview {
      width: min(460px, 92vw);
      height: auto;
    }
    .print-header {
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
    .print-body {
      flex: 1;
      min-height: 0;
      display: grid;
      /* 一定要 minmax(0, 1fr)：純 1fr 的 min-width 是 auto，iframe 會把整格撐爆 */
      grid-template-columns: minmax(0, 1fr) 300px;
      font-size: var(--font-size-md);
    }
    .print-dialog.no-preview .print-body {
      grid-template-columns: 1fr;
    }
    .print-preview {
      min-width: 0;
      min-height: 0;
      border-right: 1px solid var(--c-border-soft);
    }
    .pdf-frame {
      width: 100%;
      height: 100%;
      min-height: 0;
      border: 0;
      display: block;
      background: #525659;
    }
    .print-notice {
      margin: 0;
      padding: var(--space-md) var(--space-lg) 0;
      color: var(--c-text-secondary);
      font-size: var(--font-size-sm);
    }
    .print-settings {
      padding: var(--space-lg);
      display: flex;
      flex-direction: column;
      gap: var(--space-md);
      overflow-y: auto;
    }
    .print-row {
      display: grid;
      grid-template-columns: 4.5rem minmax(0, 1fr);
      align-items: center;
      gap: var(--space-md);
    }
    .print-label {
      color: var(--c-text-secondary);
    }
    .print-control {
      font-family: inherit;
      font-size: inherit;
      padding: 0.35rem 0.5rem;
      border: 1px solid var(--c-border);
      border-radius: 4px;
      background: var(--c-surface);
      color: inherit;
      min-width: 0;
    }
    .print-copies {
      width: 5rem;
    }
    .print-static {
      color: var(--c-text-primary);
    }
    .print-warn {
      color: var(--c-danger, #b3261e);
    }
    .print-detail {
      color: var(--c-text-secondary);
      font-size: var(--font-size-sm);
      white-space: nowrap;
    }
    .print-remember {
      display: flex;
      align-items: flex-start;
      gap: var(--space-sm);
      font-size: var(--font-size-sm);
      color: var(--c-text-secondary);
      line-height: 1.5;
    }
    .print-actions {
      display: flex;
      justify-content: flex-end;
      gap: var(--space-sm);
      padding: var(--space-md) var(--space-lg);
      background: var(--c-bg);
      border-top: 1px solid var(--c-border-soft);
      border-radius: 0 0 6px 6px;
    }
    @keyframes fadeIn {
      from {
        opacity: 0;
      }
      to {
        opacity: 1;
      }
    }
    @keyframes pop {
      from {
        transform: translateY(8px) scale(0.98);
        opacity: 0;
      }
      to {
        transform: translateY(0) scale(1);
        opacity: 1;
      }
    }
  `,
})
export class PrintDialogComponent {
  private readonly sanitizer = inject(DomSanitizer);

  readonly config = input.required<PrintDialogConfig>();
  readonly print = output<PrintDialogResult>();
  readonly cancel = output<void>();

  protected readonly hasPreview = computed(() => !!this.config().previewUrl);
  protected readonly previewOnly = computed(() => this.config().mode === 'preview-only');
  /** 瀏覽器沒有印表機能力，主鈕做的事是「開新分頁自己列印」，文案不能騙人 */
  protected readonly confirmLabel = computed(() => (this.previewOnly() ? '在新分頁開啟' : '列印'));
  protected readonly canSubmit = computed(
    () => this.previewOnly() || this.config().printers.length > 0,
  );

  /**
   * `#toolbar=0`：Chromium 內建 PDF viewer 的工具列自帶列印鈕，按下去會繞過整條列印通道
   * （紙張 / 縮放全部失效）。預覽只該是預覽，送印一律走底下那顆按鈕。
   */
  protected readonly previewSrc = computed<SafeResourceUrl | null>(() => {
    const url = this.config().previewUrl;
    return url ? this.sanitizer.bypassSecurityTrustResourceUrl(`${url}#toolbar=0`) : null;
  });

  /** 預設選擇：上次記住的 → 系統預設印表機 → 第一台。 */
  private readonly initialDevice = computed(() => {
    const c = this.config();
    if (c.deviceName && c.printers.some((p) => p.name === c.deviceName)) return c.deviceName;
    return (c.printers.find((p) => p.isDefault) ?? c.printers[0])?.name ?? '';
  });

  protected readonly device = signal('');
  protected readonly copies = signal(1);
  protected readonly scale = signal<ScaleMode>('actual');
  protected readonly remember = signal(true);

  constructor() {
    // config 是 required input，在第一次變更偵測後才有值 → 用 effect 之外的最簡方式：
    // queueMicrotask 讓 input 先就緒再套預設值（對話框只建立一次，不需要持續同步）。
    queueMicrotask(() => {
      this.device.set(this.initialDevice());
      this.copies.set(this.config().copies);
      this.scale.set(this.config().scaleMode);
    });
  }

  protected submit(): void {
    this.print.emit({
      deviceName: this.device() || undefined,
      copies: Math.min(99, Math.max(1, Math.round(Number(this.copies()) || 1))),
      scaleMode: this.scale(),
      // preview-only 沒有印表機設定可記，記了下次進 Electron 反而會套到空值
      remember: this.previewOnly() ? false : this.remember(),
    });
  }

  @HostListener('document:keydown.escape')
  protected onEscape(): void {
    this.cancel.emit();
  }
}
