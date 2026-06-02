import {
  ChangeDetectionStrategy,
  Component,
  computed,
  ElementRef,
  inject,
  signal,
  ViewChild,
} from '@angular/core';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { CdkVirtualScrollViewport, ScrollingModule } from '@angular/cdk/scrolling';
import { BelieverApi } from '../../core/api/believers/believer.api';
import type {
  BelieverListItem,
  BelieverSearchQuery,
} from '../../core/api/believers/believer.models';
import { ApiError } from '../../core/http/api-error';
import { FormOverlayComponent } from '../../shared/form-overlay/form-overlay.component';
import { ConfirmDialogService } from '../../shared/confirm-dialog/confirm-dialog.service';
import { ContextMenuService } from '../../shared/context-menu/context-menu.service';
import type { ContextMenuItem } from '../../shared/context-menu/context-menu.types';
import { IconComponent } from '../../shared/icon/icon.component';
import { BelieverEditFormComponent } from './believer-edit-form.component';
import { BELIEVER_COLUMNS, type BelieverColumnDef } from './believer-columns';

const ROW_HEIGHT = 26;

@Component({
  selector: 'app-believers-page',
  imports: [
    ReactiveFormsModule,
    ScrollingModule,
    FormOverlayComponent,
    BelieverEditFormComponent,
    IconComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './believers-page.html',
  styleUrl: './believers-page.scss',
})
export class BelieversPage {
  private readonly api = inject(BelieverApi);
  private readonly fb = inject(FormBuilder);
  private readonly confirmDialog = inject(ConfirmDialogService);
  private readonly menu = inject(ContextMenuService);

  @ViewChild(BelieverEditFormComponent) protected editFormRef?: BelieverEditFormComponent;
  @ViewChild('vp') protected vp?: CdkVirtualScrollViewport;
  @ViewChild('headerInner') protected headerInner?: ElementRef<HTMLElement>;

  protected readonly results = signal<BelieverListItem[]>([]);
  protected readonly total = signal(0);
  protected readonly searching = signal(false);
  protected readonly hasSearched = signal(false);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly editTarget = signal<BelieverListItem | null>(null);
  protected readonly overlayOpen = signal(false);
  protected readonly editDirty = signal(false);

  protected readonly rowHeight = ROW_HEIGHT;
  protected readonly columns = BELIEVER_COLUMNS;

  protected readonly gridTemplateColumns = computed<string>(() =>
    this.columns.map((c) => `${c.width}px`).join(' '),
  );
  protected readonly totalGridWidth = computed<number>(() =>
    this.columns.reduce((sum, c) => sum + c.width, 0),
  );

  protected readonly editTitle = computed(() =>
    this.editTarget() ? '編輯信眾' : '新增信眾',
  );

  protected readonly searchForm = this.fb.nonNullable.group({
    name: [''],
    phone: [''],
    hallName: [''],
    livingName: [''],
    deadName: [''],
  });

  protected async search(): Promise<void> {
    const raw = this.searchForm.getRawValue();
    const query: BelieverSearchQuery = {
      name: raw.name || null,
      phone: raw.phone || null,
      hallName: raw.hallName || null,
      livingName: raw.livingName || null,
      deadName: raw.deadName || null,
    };
    if (!query.name && !query.phone && !query.hallName && !query.livingName && !query.deadName) {
      this.errorMessage.set('請輸入搜尋條件');
      return;
    }
    this.searching.set(true);
    this.errorMessage.set(null);
    try {
      const resp = await this.api.search(query);
      this.results.set(resp.items);
      this.total.set(resp.total);
      this.hasSearched.set(true);
    } catch (err) {
      this.errorMessage.set(toMessage(err));
    } finally {
      this.searching.set(false);
    }
  }

  protected resetSearch(): void {
    this.searchForm.reset({
      name: '', phone: '', hallName: '', livingName: '', deadName: '',
    });
    this.results.set([]);
    this.total.set(0);
    this.hasSearched.set(false);
    this.errorMessage.set(null);
  }

  protected onViewportScroll(): void {
    if (!this.vp || !this.headerInner) return;
    const el = this.vp.getElementRef().nativeElement;
    this.headerInner.nativeElement.style.transform = `translateX(-${el.scrollLeft}px)`;
  }

  protected trackRow = (_: number, item: BelieverListItem): string => item.id;

  protected startCreate(): void {
    this.editTarget.set(null);
    this.editDirty.set(false);
    this.overlayOpen.set(true);
  }

  protected startEdit(item: BelieverListItem): void {
    this.editTarget.set(item);
    this.editDirty.set(false);
    this.overlayOpen.set(true);
  }

  protected onOverlayClose(): void {
    this.overlayOpen.set(false);
    this.editTarget.set(null);
    this.editDirty.set(false);
  }

  protected onOverlaySubmit(): void {
    void this.editFormRef?.submit();
  }

  protected async onOverlaySaved(): Promise<void> {
    this.onOverlayClose();
    await this.search();
  }

  protected onEditDirtyChange(dirty: boolean): void {
    this.editDirty.set(dirty);
  }

  protected openRowMenu(event: MouseEvent, item: BelieverListItem): void {
    event.preventDefault();
    event.stopPropagation();
    this.menu.open<BelieverListItem>({
      origin: { x: event.clientX, y: event.clientY },
      items: this.buildMenuItems(),
      context: item,
    });
  }

  protected openRowMenuFromButton(button: HTMLElement, item: BelieverListItem): void {
    this.menu.open<BelieverListItem>({
      origin: button,
      items: this.buildMenuItems(),
      context: item,
    });
  }

  private buildMenuItems(): ContextMenuItem<BelieverListItem>[] {
    return [
      {
        id: 'edit',
        label: '編輯',
        icon: 'pencil',
        onClick: (item) => this.startEdit(item),
      },
      {
        id: 'delete',
        label: '刪除',
        icon: 'trash',
        danger: true,
        onClick: (item) => this.actionDelete(item),
      },
    ];
  }

  private async actionDelete(item: BelieverListItem): Promise<void> {
    const ok = await this.confirmDialog.ask({
      title: '刪除信眾',
      message: `將刪除信眾「${item.name}」，不可復原，確定？`,
      confirmLabel: '確認刪除',
      danger: true,
    });
    if (!ok) return;
    this.errorMessage.set(null);
    try {
      await this.api.remove(item.id);
      await this.search();
    } catch (err) {
      this.errorMessage.set(toMessage(err));
    }
  }
}

function toMessage(err: unknown): string {
  return err instanceof ApiError ? err.message : '操作失敗，請稍後再試';
}
