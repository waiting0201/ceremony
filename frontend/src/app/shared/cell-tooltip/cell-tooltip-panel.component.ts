import { ChangeDetectionStrategy, Component, input, ViewEncapsulation } from '@angular/core';

@Component({
  selector: 'app-cell-tooltip-panel',
  changeDetection: ChangeDetectionStrategy.OnPush,
  encapsulation: ViewEncapsulation.None,
  template: `<div class="cell-tooltip" role="tooltip">{{ text() }}</div>`,
  styles: `
    /* pane 是 overlay 容器（本元件的父層），故樣式必須全域（encapsulation: None）。
       pointer-events: none：tooltip 蓋在格線上時不可吃掉點選／右鍵，否則會擋住選列與右鍵選單。 */
    .cell-tooltip-pane { pointer-events: none; }
    .cell-tooltip {
      max-width: 420px;
      padding: 3px 6px;
      background: var(--c-surface);
      border: 1px solid var(--c-border);
      border-radius: 3px;
      box-shadow: 0 4px 14px rgba(44, 42, 38, 0.16);
      font-family: var(--font-ui);
      font-size: var(--font-size-sm-plus);
      line-height: 1.45;
      color: var(--c-text-primary);
      /* 儲存格是單行截斷，但完整內容（備註／地址）可能很長，這裡改為可折行全文顯示 */
      white-space: pre-wrap;
      overflow-wrap: anywhere;
    }
  `,
})
export class CellTooltipPanelComponent {
  readonly text = input.required<string>();
}
