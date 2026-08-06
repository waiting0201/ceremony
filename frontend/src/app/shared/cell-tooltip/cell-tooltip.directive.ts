import { Overlay, OverlayRef, type ConnectedPosition } from '@angular/cdk/overlay';
import { ComponentPortal } from '@angular/cdk/portal';
import { DestroyRef, Directive, ElementRef, HostListener, inject } from '@angular/core';
import { CellTooltipPanelComponent } from './cell-tooltip-panel.component';

// 滑鼠停留在儲存格（或表頭欄名）就冒出該格內容。舊 WinForms DataGridView 的 ShowCellToolTips
// 只在文字被欄寬截斷時才冒，2026-08-06 使用者指定改為**每一格都冒**（不看有沒有截斷）；
// 只有空白格例外——沒有文字可顯示。
// 掛在 .vgrid-zone 上做事件委派（不是每格掛一個），因為虛擬捲動下 row 會不斷回收重建，
// 逐格綁定會隨捲動反覆建立／銷毀監聽器。
const SHOW_DELAY_MS = 500; // 對齊 WinForms ToolTip.InitialDelay 預設值
const TARGET_SELECTOR = '.vgrid-td-text, .vgrid-th-label';

@Directive({
  selector: '[appCellTooltip]',
  standalone: true,
})
export class CellTooltipDirective {
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef).nativeElement;
  private readonly overlay = inject(Overlay);

  private overlayRef: OverlayRef | null = null;
  private anchor: HTMLElement | null = null;
  private timer: ReturnType<typeof setTimeout> | null = null;

  constructor() {
    // scroll 不冒泡，@HostListener('scroll') 收不到子層 viewport 的捲動，只能用 capture。
    // 捲動時務必關掉：虛擬捲動會把 tooltip 錨定的那格拿去畫別一列，留著會指到錯的資料。
    this.host.addEventListener('scroll', this.hide, true);
    inject(DestroyRef).onDestroy(() => {
      this.host.removeEventListener('scroll', this.hide, true);
      this.hide();
    });
  }

  @HostListener('mouseover', ['$event'])
  protected onMouseOver(event: MouseEvent): void {
    const target = event.target;
    if (!(target instanceof HTMLElement)) return;
    const cell = target.closest<HTMLElement>(TARGET_SELECTOR);
    if (cell === this.anchor) return;
    this.hide();
    if (!cell) return;
    const text = cell.textContent?.trim() ?? '';
    if (!text) return;
    this.anchor = cell;
    this.timer = setTimeout(() => this.show(cell, text), SHOW_DELAY_MS);
  }

  @HostListener('mouseout', ['$event'])
  protected onMouseOut(event: MouseEvent): void {
    const next = event.relatedTarget;
    if (next instanceof Node && this.anchor?.contains(next)) return;
    this.hide();
  }

  // 使用者一開始操作（點選、右鍵、滾輪）就收掉，別讓 tooltip 卡在畫面上擋事
  @HostListener('mousedown')
  @HostListener('wheel')
  protected onInteract(): void {
    this.hide();
  }

  private show(cell: HTMLElement, text: string): void {
    this.timer = null;
    if (!cell.isConnected) {
      this.anchor = null;
      return;
    }
    const positions: ConnectedPosition[] = [
      { originX: 'start', originY: 'bottom', overlayX: 'start', overlayY: 'top', offsetY: 2 },
      { originX: 'start', originY: 'top', overlayX: 'start', overlayY: 'bottom', offsetY: -2 },
    ];
    const overlayRef = this.overlay.create({
      positionStrategy: this.overlay
        .position()
        .flexibleConnectedTo(cell)
        .withPositions(positions)
        .withPush(true),
      scrollStrategy: this.overlay.scrollStrategies.close(),
      disposeOnNavigation: true,
      panelClass: 'cell-tooltip-pane',
    });
    const componentRef = overlayRef.attach(new ComponentPortal(CellTooltipPanelComponent));
    componentRef.setInput('text', text);
    this.overlayRef = overlayRef;
  }

  private readonly hide = (): void => {
    if (this.timer !== null) {
      clearTimeout(this.timer);
      this.timer = null;
    }
    this.anchor = null;
    if (this.overlayRef) {
      this.overlayRef.dispose();
      this.overlayRef = null;
    }
  };
}
