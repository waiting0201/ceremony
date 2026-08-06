import { ChangeDetectionStrategy, Component } from '@angular/core';
import { TestBed, type ComponentFixture } from '@angular/core/testing';
import { CellTooltipDirective } from './cell-tooltip.directive';

/**
 * 行為鎖：hover 任一儲存格（不論文字有沒有被欄寬截斷）停留 500ms 都顯示該格內容，
 * 只有空白格不顯示。2026-08-06 使用者指定由「只有截斷才冒」改為「每一格都冒」。
 */
describe('CellTooltipDirective（格線 hover 顯示儲存格內容）', () => {
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

  const cell = (id: string): HTMLElement => fixture.nativeElement.querySelector(`#${id}`);

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
    const target = cell('cut');

    hover(target);
    expect(tooltipText()).toBeNull(); // 尚未過 initial delay

    vi.advanceTimersByTime(500);
    fixture.detectChanges();
    expect(tooltipText()).toBe('台中市北屯區松竹路二段 100 號');
  });

  it('文字沒被截斷也顯示（每一格都有）', () => {
    hover(cell('fit'));
    vi.advanceTimersByTime(500);
    fixture.detectChanges();

    expect(tooltipText()).toBe('王大明');
  });

  it('空儲存格不顯示（沒有內容可冒）', () => {
    hover(cell('blank'));
    vi.advanceTimersByTime(500);
    fixture.detectChanges();

    expect(tooltipText()).toBeNull();
  });

  it('表頭欄名也會顯示', () => {
    const label = fixture.nativeElement.querySelector('.vgrid-th-label') as HTMLElement;

    hover(label);
    vi.advanceTimersByTime(500);
    fixture.detectChanges();

    expect(tooltipText()).toBe('寄件地址');
  });

  it('滑鼠離開 → 收起', () => {
    const target = cell('cut');
    hover(target);
    vi.advanceTimersByTime(500);
    fixture.detectChanges();
    expect(tooltipText()).not.toBeNull();

    leave(target);
    fixture.detectChanges();
    expect(tooltipText()).toBeNull();
  });

  it('捲動 → 收起（虛擬捲動會把該格拿去畫別列）', () => {
    const target = cell('cut');
    hover(target);
    vi.advanceTimersByTime(500);
    fixture.detectChanges();
    expect(tooltipText()).not.toBeNull();

    target.dispatchEvent(new Event('scroll'));
    fixture.detectChanges();
    expect(tooltipText()).toBeNull();
  });

  it('點擊 → 收起，不擋住選列', () => {
    const target = cell('cut');
    hover(target);
    vi.advanceTimersByTime(500);
    fixture.detectChanges();

    target.dispatchEvent(new MouseEvent('mousedown', { bubbles: true }));
    fixture.detectChanges();
    expect(tooltipText()).toBeNull();
  });

  it('移到另一格 → 換成該格內容', () => {
    const first = cell('cut');
    hover(first);
    vi.advanceTimersByTime(500);
    fixture.detectChanges();
    expect(tooltipText()).toBe('台中市北屯區松竹路二段 100 號');

    const second = cell('fit');
    leave(first, second);
    hover(second);
    vi.advanceTimersByTime(500);
    fixture.detectChanges();
    expect(tooltipText()).toBe('王大明');
  });
});
