import { ChangeDetectionStrategy, Component } from '@angular/core';
import { TestBed, type ComponentFixture } from '@angular/core/testing';
import { CellTooltipDirective } from './cell-tooltip.directive';

/**
 * 行為鎖：對齊舊系統 WinForms DataGridView 的 ShowCellToolTips——
 * 只有「文字被欄寬截斷」的儲存格 hover 才冒出完整內容，沒截斷的不冒。
 */
describe('CellTooltipDirective（格線截斷才顯示完整內容）', () => {
  @Component({
    selector: 'app-cell-tooltip-host',
    imports: [CellTooltipDirective],
    changeDetection: ChangeDetectionStrategy.OnPush,
    template: `
      <div class="vgrid-zone" appCellTooltip>
        <div class="vgrid-th"><span class="vgrid-th-label">寄件地址</span></div>
        <div class="vgrid-td"><span class="vgrid-td-text" id="cut">台中市北屯區松竹路二段 100 號</span></div>
        <div class="vgrid-td"><span class="vgrid-td-text" id="fit">王大明</span></div>
        <div class="vgrid-td"><span class="vgrid-td-text" id="blank"></span></div>
      </div>
    `,
  })
  class HostComponent {}

  /** jsdom 不做排版，scrollWidth / clientWidth 恆為 0，得自己餵尺寸才能測「有沒有被截斷」 */
  const setWidths = (id: string, scrollWidth: number, clientWidth: number): HTMLElement => {
    const el = fixture.nativeElement.querySelector(`#${id}`) as HTMLElement;
    Object.defineProperty(el, 'scrollWidth', { value: scrollWidth, configurable: true });
    Object.defineProperty(el, 'clientWidth', { value: clientWidth, configurable: true });
    return el;
  };

  const hover = (el: HTMLElement): void => {
    el.dispatchEvent(new MouseEvent('mouseover', { bubbles: true }));
  };

  const leave = (el: HTMLElement, to: Node | null = null): void => {
    el.dispatchEvent(new MouseEvent('mouseout', { bubbles: true, relatedTarget: to }));
  };

  const tooltipText = (): string | null =>
    document.querySelector('.cell-tooltip')?.textContent?.trim() ?? null;

  let fixture: ComponentFixture<HostComponent>;

  beforeEach(() => {
    vi.useFakeTimers();
    TestBed.configureTestingModule({});
    fixture = TestBed.createComponent(HostComponent);
    fixture.detectChanges();
  });

  afterEach(() => {
    fixture.destroy();
    vi.useRealTimers();
  });

  it('文字被截斷 → 停留後顯示完整內容', () => {
    const cell = setWidths('cut', 260, 120);

    hover(cell);
    expect(tooltipText()).toBeNull(); // 尚未過 initial delay

    vi.advanceTimersByTime(500);
    fixture.detectChanges();
    expect(tooltipText()).toBe('台中市北屯區松竹路二段 100 號');
  });

  it('文字沒被截斷 → 不顯示', () => {
    hover(setWidths('fit', 60, 120));
    vi.advanceTimersByTime(500);
    fixture.detectChanges();

    expect(tooltipText()).toBeNull();
  });

  it('空儲存格即使量到截斷也不顯示', () => {
    hover(setWidths('blank', 260, 120));
    vi.advanceTimersByTime(500);
    fixture.detectChanges();

    expect(tooltipText()).toBeNull();
  });

  it('表頭欄名被截斷也會顯示', () => {
    const label = fixture.nativeElement.querySelector('.vgrid-th-label') as HTMLElement;
    Object.defineProperty(label, 'scrollWidth', { value: 200, configurable: true });
    Object.defineProperty(label, 'clientWidth', { value: 40, configurable: true });

    hover(label);
    vi.advanceTimersByTime(500);
    fixture.detectChanges();

    expect(tooltipText()).toBe('寄件地址');
  });

  it('滑鼠離開 → 收起', () => {
    const cell = setWidths('cut', 260, 120);
    hover(cell);
    vi.advanceTimersByTime(500);
    fixture.detectChanges();
    expect(tooltipText()).not.toBeNull();

    leave(cell);
    fixture.detectChanges();
    expect(tooltipText()).toBeNull();
  });

  it('捲動 → 收起（虛擬捲動會把該格拿去畫別列）', () => {
    const cell = setWidths('cut', 260, 120);
    hover(cell);
    vi.advanceTimersByTime(500);
    fixture.detectChanges();
    expect(tooltipText()).not.toBeNull();

    cell.dispatchEvent(new Event('scroll'));
    fixture.detectChanges();
    expect(tooltipText()).toBeNull();
  });

  it('點擊 → 收起，不擋住選列', () => {
    const cell = setWidths('cut', 260, 120);
    hover(cell);
    vi.advanceTimersByTime(500);
    fixture.detectChanges();

    cell.dispatchEvent(new MouseEvent('mousedown', { bubbles: true }));
    fixture.detectChanges();
    expect(tooltipText()).toBeNull();
  });

  it('移到另一格 → 換成該格內容', () => {
    const first = setWidths('cut', 260, 120);
    hover(first);
    vi.advanceTimersByTime(500);
    fixture.detectChanges();
    expect(tooltipText()).toBe('台中市北屯區松竹路二段 100 號');

    const second = setWidths('fit', 300, 40);
    leave(first, second);
    hover(second);
    vi.advanceTimersByTime(500);
    fixture.detectChanges();
    expect(tooltipText()).toBe('王大明');
  });
});
