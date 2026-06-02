import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  HostListener,
  inject,
  input,
  output,
  signal,
  ViewEncapsulation,
} from '@angular/core';
import { IconComponent } from '../icon/icon.component';
import type { ResolvedItem } from './context-menu.types';

@Component({
  selector: 'app-context-menu-panel',
  imports: [IconComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  encapsulation: ViewEncapsulation.None,
  template: `
    <div class="ctx-menu" role="menu" tabindex="-1">
      @for (entry of items(); track entry.item.id) {
        @if (entry.item.divider) {
          <div class="ctx-divider" role="separator"></div>
        } @else {
          <button
            type="button"
            class="ctx-item"
            role="menuitem"
            [class.is-danger]="entry.item.danger"
            [class.is-active]="activeIndex() === $index"
            [disabled]="!entry.enabled"
            [attr.title]="entry.disabledReason"
            (click)="trigger(entry)"
            (mouseenter)="activeIndex.set($index)"
          >
            @if (entry.item.icon) {
              <app-icon [name]="entry.item.icon" [size]="16" />
            } @else {
              <span class="ctx-icon-spacer"></span>
            }
            <span class="ctx-label">{{ entry.item.label }}</span>
          </button>
        }
      }
    </div>
  `,
  styles: `
    .ctx-menu {
      min-width: 180px;
      padding: 4px 0;
      background: var(--c-surface);
      border: 1px solid var(--c-border);
      border-radius: 4px;
      box-shadow: 0 6px 24px rgba(44, 42, 38, 0.16);
      font-family: var(--font-ui);
      font-size: var(--font-size-base);
      color: var(--c-text-primary);
      outline: none;
      user-select: none;
    }
    .ctx-item {
      display: flex;
      align-items: center;
      gap: var(--space-sm);
      width: 100%;
      height: 32px;
      padding: 0 var(--space-md);
      border: 0;
      background: transparent;
      color: inherit;
      font: inherit;
      text-align: left;
      cursor: pointer;
    }
    .ctx-item.is-active:not(:disabled) { background: var(--c-bg-darker); }
    .ctx-item:disabled {
      opacity: 0.45;
      cursor: not-allowed;
    }
    .ctx-item.is-danger { color: var(--c-danger); }
    .ctx-item.is-danger.is-active:not(:disabled) { background: var(--c-primary-soft); }
    .ctx-icon-spacer { width: 16px; height: 16px; display: inline-block; }
    .ctx-divider {
      height: 1px;
      background: var(--c-border-soft);
      margin: 4px 0;
    }
    .ctx-label { white-space: nowrap; }
  `,
})
export class ContextMenuPanelComponent {
  readonly items = input.required<ResolvedItem<unknown>[]>();
  readonly select = output<ResolvedItem<unknown>>();
  readonly dismiss = output<void>();

  protected readonly activeIndex = signal(0);
  private readonly host: ElementRef<HTMLElement> = inject(ElementRef);

  trigger(entry: ResolvedItem<unknown>): void {
    if (!entry.enabled || entry.item.divider) return;
    this.select.emit(entry);
  }

  focus(): void {
    const root: HTMLElement = this.host.nativeElement;
    const node = root.querySelector('.ctx-menu');
    if (node instanceof HTMLElement) node.focus();
  }

  @HostListener('document:keydown', ['$event'])
  protected onKeydown(event: KeyboardEvent): void {
    const enabled = this.items().filter((e) => e.enabled && !e.item.divider);
    if (enabled.length === 0) {
      if (event.key === 'Escape') this.dismiss.emit();
      return;
    }
    if (event.key === 'Escape') {
      event.preventDefault();
      this.dismiss.emit();
      return;
    }
    const idx = this.activeIndex();
    if (event.key === 'ArrowDown') {
      event.preventDefault();
      const next = this.findEnabled(idx + 1, 1);
      if (next >= 0) this.activeIndex.set(next);
      return;
    }
    if (event.key === 'ArrowUp') {
      event.preventDefault();
      const prev = this.findEnabled(idx - 1, -1);
      if (prev >= 0) this.activeIndex.set(prev);
      return;
    }
    if (event.key === 'Enter' || event.key === ' ') {
      event.preventDefault();
      const entry = this.items()[idx];
      if (entry && entry.enabled && !entry.item.divider) {
        this.select.emit(entry);
      }
    }
  }

  private findEnabled(start: number, dir: 1 | -1): number {
    const list = this.items();
    let i = start;
    while (i >= 0 && i < list.length) {
      const e = list[i];
      if (e.enabled && !e.item.divider) return i;
      i += dir;
    }
    return -1;
  }
}
