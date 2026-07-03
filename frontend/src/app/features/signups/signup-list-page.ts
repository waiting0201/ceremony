import {
  ChangeDetectionStrategy,
  Component,
  computed,
  DestroyRef,
  effect,
  ElementRef,
  inject,
  OnInit,
  signal,
  ViewChild,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { CdkVirtualScrollViewport, ScrollingModule } from '@angular/cdk/scrolling';
import { SignupApi } from '../../core/api/signups/signup.api';
import type {
  SignupListItem,
  SignupSearchQuery,
} from '../../core/api/signups/signup.models';
import { CategoryApi } from '../../core/api/categories/category.api';
import type { CategoryNode } from '../../core/api/categories/category.models';
import { ReportApi } from '../../core/api/reports/report.api';
import type { SingleReportType } from '../../core/api/reports/report.models';
import { ApiError } from '../../core/http/api-error';
import { IconComponent } from '../../shared/icon/icon.component';
import { ContextMenuService } from '../../shared/context-menu/context-menu.service';
import type { ContextMenuItem } from '../../shared/context-menu/context-menu.types';
import { ConfirmDialogService } from '../../shared/confirm-dialog/confirm-dialog.service';
import { SIGNUP_TYPES, signupTypeLabel } from '../../shared/util/signup-type';
import { flattenCategories, type FlatCategory } from '../../shared/util/categories';
import { FormOverlayComponent } from '../../shared/form-overlay/form-overlay.component';
import { SignupEditFormComponent } from './signup-edit-form.component';
import { SignupSearchState, type SignupSearchFormSnapshot } from './signup-search-state';
import {
  SIGNUP_COLUMNS,
  SIGNUP_COL_MAX_WIDTH,
  SIGNUP_COL_MIN_WIDTH,
  type SignupColumnDef,
  type SignupColumnId,
} from './signup-columns';

interface MenuContext {
  selectedRows: SignupListItem[];
  triggerRow: SignupListItem;
}

const REPORT_TYPES: { value: SingleReportType; label: string }[] = [
  { value: 'datacard', label: '資料卡' },
  { value: 'receipt', label: '收據' },
  { value: 'tablet', label: '薦牌' },
  { value: 'text', label: '文牒' },
  { value: 'worship', label: '普桌' },
];

const ROW_HEIGHT = 26;
const LS_SHOW_ALL = 'ceremony.signupList.showAll';
const LS_COL_WIDTHS = 'ceremony.signupList.colWidths';

interface EditOverlayState {
  signupId: string | null;
  fromSignupId: string | null;
}

@Component({
  selector: 'app-signup-list-page',
  imports: [
    ReactiveFormsModule,
    ScrollingModule,
    IconComponent,
    FormOverlayComponent,
    SignupEditFormComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './signup-list-page.html',
  styleUrl: './signup-list-page.scss',
})
export class SignupListPage implements OnInit {
  private readonly destroyRef = inject(DestroyRef);
  @ViewChild('vp') protected vp?: CdkVirtualScrollViewport;
  @ViewChild('headerInner') protected headerInner?: ElementRef<HTMLElement>;
  @ViewChild(SignupEditFormComponent) protected editFormRef?: SignupEditFormComponent;

  protected readonly editOverlay = signal<EditOverlayState | null>(null);
  protected readonly editFormDirty = signal(false);

  private readonly api = inject(SignupApi);
  private readonly categoryApi = inject(CategoryApi);
  private readonly reportApi = inject(ReportApi);
  private readonly fb = inject(FormBuilder);
  private readonly router = inject(Router);
  private readonly menu = inject(ContextMenuService);
  private readonly confirmDialog = inject(ConfirmDialogService);
  private readonly state = inject(SignupSearchState);

  protected readonly signupTypes = SIGNUP_TYPES;
  protected readonly reportTypes = REPORT_TYPES;
  protected readonly rowHeight = ROW_HEIGHT;
  protected readonly categories = signal<CategoryNode[]>([]);
  protected readonly flatCategories = computed<FlatCategory[]>(() =>
    flattenCategories(this.categories()),
  );

  protected readonly results = signal<SignupListItem[]>([]);
  protected readonly total = signal(0);
  protected readonly loading = signal(false);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly successMessage = signal<string | null>(null);
  protected readonly exporting = signal(false);
  protected readonly printing = signal(false);
  protected readonly hasSearched = signal(false);
  protected readonly keyEnabled = signal(false);

  protected readonly showAll = signal(loadShowAll());
  protected readonly columnWidths = signal<Record<string, number>>(loadColumnWidths());

  protected readonly selectedIds = signal<ReadonlySet<string>>(new Set());
  private lastClickIndex: number | null = null;

  protected readonly selectedCount = computed(() => this.selectedIds().size);
  protected readonly allSelected = computed(() => {
    const ids = this.selectedIds();
    const items = this.results();
    return items.length > 0 && items.every((r) => ids.has(r.id));
  });
  protected readonly anySelected = computed(() => this.selectedIds().size > 0);
  protected readonly partialSelected = computed(
    () => this.anySelected() && !this.allSelected(),
  );

  protected readonly visibleColumns = computed<SignupColumnDef[]>(() => {
    const all = this.showAll();
    return SIGNUP_COLUMNS.filter((c) => all || !c.toggleOnly);
  });

  protected readonly gridTemplateColumns = computed<string>(() => {
    const widths = this.columnWidths();
    return this.visibleColumns()
      .map((c) => `${widths[c.id] ?? c.width}px`)
      .join(' ');
  });

  protected readonly totalGridWidth = computed<number>(() => {
    const widths = this.columnWidths();
    return this.visibleColumns().reduce((sum, c) => sum + (widths[c.id] ?? c.width), 0);
  });

  protected readonly form = this.fb.nonNullable.group({
    year: [null as number | null, [Validators.min(1), Validators.max(999)]],
    isScope: [false],
    ceremonyCategoryId: [''],
    signupType: [-1],
    number: [null as number | null],
    isFixedNumber: [false],
    searchKey: [{ value: '', disabled: true }],
    scopeName: [false],
    scopeLivingName: [false],
    scopeDeadName: [false],
    scopePhone: [false],
    scopeRemark: [false],
  });

  protected readonly batchForm = this.fb.nonNullable.group({
    numberStart: [null as number | null, [Validators.required, Validators.min(1)]],
    numberEnd: [null as number | null, [Validators.required, Validators.min(1)]],
    reportType: ['datacard' as SingleReportType, [Validators.required]],
  });

  protected readonly signupTypeLabel = signupTypeLabel;

  constructor() {
    effect(() => {
      try {
        localStorage.setItem(LS_SHOW_ALL, this.showAll() ? '1' : '0');
      } catch {
        /* noop */
      }
    });
    effect(() => {
      try {
        localStorage.setItem(LS_COL_WIDTHS, JSON.stringify(this.columnWidths()));
      } catch {
        /* noop */
      }
    });
    // 選取狀態跨路由保留：每次變動同步到 SignupSearchState
    effect(() => {
      const ids = this.selectedIds();
      if (this.hasSearched()) {
        this.state.selectedIds.set(ids);
      }
    });
  }

  ngOnInit(): void {
    void this.loadCategories();
    this.bindScopeKeyToggle();
    this.restoreFromState();
  }

  /**
   * 進入修改/歷程頁再返回時，由 SignupSearchState 還原上次的搜尋條件 + 結果。
   * 若 state 帶 stale flag（edit/create/delete 成功設定），重新查詢一次。
   */
  private restoreFromState(): void {
    const cached = this.state.form();
    if (!cached) return; // 第一次進入，無快取

    this.form.patchValue(cached, { emitEvent: false });
    // scope* 連動 key 啟用狀態
    const anyScope =
      cached.scopeName ||
      cached.scopeLivingName ||
      cached.scopeDeadName ||
      cached.scopePhone ||
      cached.scopeRemark;
    this.keyEnabled.set(anyScope);
    const keyCtrl = this.form.controls.searchKey;
    if (anyScope) keyCtrl.enable({ emitEvent: false });
    else keyCtrl.disable({ emitEvent: false });

    if (this.state.stale()) {
      this.state.clearStale();
      void this.search();
      return;
    }

    this.results.set(this.state.results().slice());
    this.total.set(this.state.total());
    this.hasSearched.set(this.state.hasSearched());
    this.selectedIds.set(new Set(this.state.selectedIds()));
  }

  private saveToState(): void {
    this.state.form.set(this.form.getRawValue() as SignupSearchFormSnapshot);
    this.state.results.set(this.results());
    this.state.total.set(this.total());
    this.state.hasSearched.set(this.hasSearched());
    this.state.selectedIds.set(this.selectedIds());
  }

  private bindScopeKeyToggle(): void {
    const update = () => {
      const v = this.form.getRawValue();
      const any =
        v.scopeName || v.scopeLivingName || v.scopeDeadName || v.scopePhone || v.scopeRemark;
      this.keyEnabled.set(any);
      const ctrl = this.form.controls.searchKey;
      if (any) {
        if (ctrl.disabled) ctrl.enable({ emitEvent: false });
      } else {
        if (!ctrl.disabled) ctrl.disable({ emitEvent: false });
        if (ctrl.value) ctrl.setValue('', { emitEvent: false });
      }
    };
    for (const name of [
      'scopeName',
      'scopeLivingName',
      'scopeDeadName',
      'scopePhone',
      'scopeRemark',
    ] as const) {
      this.form.controls[name].valueChanges
        .pipe(takeUntilDestroyed(this.destroyRef))
        .subscribe(update);
    }
  }

  protected onViewportScroll(): void {
    if (!this.vp || !this.headerInner) return;
    const el = this.vp.getElementRef().nativeElement;
    this.headerInner.nativeElement.style.transform = `translateX(-${el.scrollLeft}px)`;
  }

  private async loadCategories(): Promise<void> {
    try {
      const resp = await this.categoryApi.list();
      this.categories.set(resp.items);
    } catch (err) {
      this.errorMessage.set(toMessage(err));
    }
  }

  private buildQuery(): SignupSearchQuery {
    const v = this.form.getRawValue();
    return {
      year: v.year ?? null,
      isScope: v.isScope,
      ceremonyCategoryId: v.ceremonyCategoryId || null,
      signupType: v.signupType,
      number: v.number,
      searchKey: v.searchKey?.trim() || null,
      scopeName: v.scopeName,
      scopeLivingName: v.scopeLivingName,
      scopeDeadName: v.scopeDeadName,
      scopePhone: v.scopePhone,
      scopeRemark: v.scopeRemark,
      isFixedNumber: v.isFixedNumber,
    };
  }

  protected async search(): Promise<void> {
    this.loading.set(true);
    this.errorMessage.set(null);
    this.successMessage.set(null);
    this.hasSearched.set(true);
    try {
      const resp = await this.api.search(this.buildQuery());
      this.results.set(resp.items);
      this.total.set(resp.total);
      this.selectedIds.set(new Set());
      this.lastClickIndex = null;
      this.saveToState();
    } catch (err) {
      this.errorMessage.set(toMessage(err));
    } finally {
      this.loading.set(false);
    }
  }

  protected resetForm(): void {
    this.form.reset({
      year: null,
      isScope: false,
      ceremonyCategoryId: '',
      signupType: -1,
      number: null,
      isFixedNumber: false,
      searchKey: '',
      scopeName: false,
      scopeLivingName: false,
      scopeDeadName: false,
      scopePhone: false,
      scopeRemark: false,
    });
    this.form.controls.searchKey.disable({ emitEvent: false });
    this.keyEnabled.set(false);
  }

  protected async exportExcel(): Promise<void> {
    if (this.exporting()) return;
    this.exporting.set(true);
    this.errorMessage.set(null);
    try {
      const { blob, fileName } = await this.api.exportExcel(this.buildQuery());
      downloadBlob(blob, fileName);
      this.successMessage.set(`已匯出 ${fileName}`);
    } catch (err) {
      this.errorMessage.set(toMessage(err));
    } finally {
      this.exporting.set(false);
    }
  }

  // ──────────── Column width persistence + resize ────────────

  protected widthOf(col: SignupColumnDef): number {
    return this.columnWidths()[col.id] ?? col.width;
  }

  protected startColumnResize(event: PointerEvent, col: SignupColumnDef): void {
    if (!col.resizable) return;
    event.preventDefault();
    event.stopPropagation();
    const startX = event.clientX;
    const startWidth = this.widthOf(col);
    const colId = col.id;

    const onMove = (ev: PointerEvent) => {
      const dx = ev.clientX - startX;
      const next = clamp(startWidth + dx, SIGNUP_COL_MIN_WIDTH, SIGNUP_COL_MAX_WIDTH);
      this.columnWidths.update((w) => ({ ...w, [colId]: next }));
    };
    const onUp = () => {
      document.removeEventListener('pointermove', onMove);
      document.removeEventListener('pointerup', onUp);
      document.body.style.cursor = '';
      document.body.style.userSelect = '';
    };
    document.addEventListener('pointermove', onMove);
    document.addEventListener('pointerup', onUp);
    document.body.style.cursor = 'col-resize';
    document.body.style.userSelect = 'none';
  }

  protected resetColumnWidths(): void {
    this.columnWidths.set({});
  }

  // ──────────── Selection ────────────

  protected toggleRow(item: SignupListItem, event: MouseEvent | null, index: number): void {
    const current = new Set(this.selectedIds());
    if (event?.shiftKey && this.lastClickIndex != null) {
      const lo = Math.min(this.lastClickIndex, index);
      const hi = Math.max(this.lastClickIndex, index);
      const items = this.results();
      for (let i = lo; i <= hi; i++) current.add(items[i].id);
    } else {
      current.has(item.id) ? current.delete(item.id) : current.add(item.id);
    }
    this.selectedIds.set(current);
    this.lastClickIndex = index;
  }

  protected toggleAll(): void {
    if (this.allSelected()) {
      this.selectedIds.set(new Set());
    } else {
      this.selectedIds.set(new Set(this.results().map((r) => r.id)));
    }
  }

  protected clearSelection(): void {
    this.selectedIds.set(new Set());
    this.lastClickIndex = null;
  }

  protected isSelected(id: string): boolean {
    return this.selectedIds().has(id);
  }

  protected toggleShowAll(): void {
    this.showAll.update((v) => !v);
  }

  protected onHeaderCheckboxChange(input: HTMLInputElement): void {
    input.indeterminate = this.partialSelected();
  }

  // ──────────── Context menu ────────────

  protected openRowMenu(event: MouseEvent, item: SignupListItem, index: number): void {
    event.preventDefault();
    event.stopPropagation();
    if (!this.selectedIds().has(item.id)) {
      this.selectedIds.set(new Set([item.id]));
      this.lastClickIndex = index;
    }
    this.menu.open<MenuContext>({
      origin: { x: event.clientX, y: event.clientY },
      items: this.buildMenuItems(),
      context: this.menuContext(item),
    });
  }

  protected openRowMenuFromButton(button: HTMLElement, item: SignupListItem, index: number): void {
    if (!this.selectedIds().has(item.id)) {
      this.selectedIds.set(new Set([item.id]));
      this.lastClickIndex = index;
    }
    this.menu.open<MenuContext>({
      origin: button,
      items: this.buildMenuItems(),
      context: this.menuContext(item),
    });
  }

  protected openBulkMenu(button: HTMLElement): void {
    const rows = this.selectedRows();
    if (rows.length === 0) return;
    this.menu.open<MenuContext>({
      origin: button,
      items: this.buildMenuItems(),
      context: {
        selectedRows: rows,
        triggerRow: rows[0],
      },
    });
  }

  private menuContext(triggerRow: SignupListItem): MenuContext {
    const rows = this.selectedRows();
    return {
      selectedRows: rows.length > 0 ? rows : [triggerRow],
      triggerRow,
    };
  }

  private selectedRows(): SignupListItem[] {
    const ids = this.selectedIds();
    return this.results().filter((r) => ids.has(r.id));
  }

  private buildMenuItems(): ContextMenuItem<MenuContext>[] {
    return [
      {
        id: 'add-from',
        label: '代入新增',
        icon: 'file-plus',
        enabledWhen: (ctx) =>
          ctx.selectedRows.length === 1 || { enabled: false, reason: '請先選擇 1 筆' },
        onClick: (ctx) => this.actionAddFrom(ctx.selectedRows[0]),
      },
      {
        id: 'edit',
        label: '修改資料',
        icon: 'pencil',
        enabledWhen: (ctx) =>
          ctx.selectedRows.length === 1 || { enabled: false, reason: '請先選擇 1 筆' },
        onClick: (ctx) => this.actionEdit(ctx.selectedRows[0]),
      },
      { id: 'sep-print', label: '', divider: true, onClick: () => {} },
      ...REPORT_TYPES.map((r) => buildPrintItem(r, (ctx) => this.actionPrint(r.value, ctx))),
      { id: 'sep-danger', label: '', divider: true, onClick: () => {} },
      {
        id: 'delete',
        label: '刪除資料',
        icon: 'trash',
        danger: true,
        enabledWhen: (ctx) =>
          ctx.selectedRows.length >= 1 || { enabled: false, reason: '請先選擇報名資料' },
        onClick: (ctx) => this.actionDelete(ctx.selectedRows),
      },
      {
        id: 'logs',
        label: '瀏覽歷程',
        icon: 'history',
        enabledWhen: (ctx) =>
          ctx.selectedRows.length === 1 || { enabled: false, reason: '請先選擇 1 筆' },
        onClick: (ctx) => this.actionLogs(ctx.selectedRows[0]),
      },
    ];
  }

  // ──────────── Actions ────────────

  protected openCreateOverlay(): void {
    this.editOverlay.set({ signupId: null, fromSignupId: null });
  }

  private actionAddFrom(item: SignupListItem): void {
    this.editOverlay.set({ signupId: null, fromSignupId: item.id });
  }

  private actionEdit(item: SignupListItem): void {
    this.editOverlay.set({ signupId: item.id, fromSignupId: null });
  }

  protected goEditSelected(): void {
    const rows = this.selectedRows();
    if (rows.length !== 1) return;
    this.actionEdit(rows[0]);
  }

  protected onOverlaySubmit(): void {
    void this.editFormRef?.submit();
  }

  protected onOverlaySaved(): void {
    this.editOverlay.set(null);
    this.editFormDirty.set(false);
    void this.search();
  }

  protected onOverlayClose(): void {
    this.editOverlay.set(null);
    this.editFormDirty.set(false);
  }

  protected onEditFormDirtyChange(dirty: boolean): void {
    this.editFormDirty.set(dirty);
  }

  private actionLogs(item: SignupListItem): void {
    void this.router.navigateByUrl(`/signups/${item.id}/logs`);
  }

  private async actionDelete(items: SignupListItem[]): Promise<void> {
    const ok = await this.confirmDialog.ask({
      title: '刪除報名資料',
      message:
        items.length === 1
          ? `將刪除 ${items[0].year} ${items[0].ceremonyTitle ?? ''} ${items[0].numberTitle ?? ''}-${items[0].number} ${items[0].name ?? ''}，不可復原，確定？`
          : `將刪除 ${items.length} 筆報名資料，不可復原，確定？`,
      confirmLabel: '確認刪除',
      danger: true,
    });
    if (!ok) return;
    this.errorMessage.set(null);
    try {
      for (const item of items) await this.api.remove(item.id);
      this.successMessage.set(`已刪除 ${items.length} 筆報名資料`);
      await this.search();
    } catch (err) {
      this.errorMessage.set(toMessage(err));
    }
  }

  private async actionPrint(type: SingleReportType, ctx: MenuContext): Promise<void> {
    const items = ctx.selectedRows;
    if (items.length === 0) return;
    if (items.length === 1) {
      await this.printSingle(type, items[0]);
      return;
    }
    await this.printSelected(type, items);
  }

  private async printSingle(type: SingleReportType, item: SignupListItem): Promise<void> {
    if (this.printing()) return;
    this.printing.set(true);
    this.errorMessage.set(null);
    try {
      const { blob, fileName } = await this.reportApi.single(type, item.id);
      openPdfInNewTab(blob, fileName);
    } catch (err) {
      this.errorMessage.set(toMessage(err));
    } finally {
      this.printing.set(false);
    }
  }

  private async printSelected(type: SingleReportType, items: SignupListItem[]): Promise<void> {
    if (this.printing()) return;
    this.printing.set(true);
    this.errorMessage.set(null);
    try {
      const resp = await this.reportApi.batch({
        reportType: type,
        signupIds: items.map((i) => i.id),
      });
      openPdfInNewTab(resp.blob, resp.fileName);
      const count = resp.signupCount ?? items.length;
      this.successMessage.set(`已列印 ${count} 筆 (${resp.fileName})`);
    } catch (err) {
      this.errorMessage.set(toMessage(err));
    } finally {
      this.printing.set(false);
    }
  }

  private async printBatch(
    type: SingleReportType,
    numberStart: number,
    numberEnd: number,
  ): Promise<void> {
    if (this.printing()) return;
    this.printing.set(true);
    this.errorMessage.set(null);
    const q = this.form.getRawValue();
    try {
      const resp = await this.reportApi.batch({
        reportType: type,
        numberStart,
        numberEnd,
        year: q.year ?? null,
        ceremonyCategoryId: q.ceremonyCategoryId || null,
        signupType: q.signupType >= 0 ? q.signupType : null,
      });
      openPdfInNewTab(resp.blob, resp.fileName);
      const count = resp.signupCount ?? '';
      this.successMessage.set(`已列印批次 ${count} 筆 (${resp.fileName})`);
    } catch (err) {
      this.errorMessage.set(toMessage(err));
    } finally {
      this.printing.set(false);
    }
  }

  protected async submitBatchPrint(): Promise<void> {
    if (this.batchForm.invalid || this.printing()) return;
    const v = this.batchForm.getRawValue();
    if (v.numberStart == null || v.numberEnd == null) return;
    if (v.numberEnd < v.numberStart) {
      this.errorMessage.set('編號錯誤');
      return;
    }
    await this.printBatch(v.reportType, v.numberStart, v.numberEnd);
  }

  protected trackRow = (_: number, item: SignupListItem): string => item.id;
  protected trackCol = (_: number, col: SignupColumnDef): SignupColumnId => col.id;
}

function buildPrintItem(
  spec: { value: SingleReportType; label: string },
  onClick: (ctx: MenuContext) => Promise<void>,
): ContextMenuItem<MenuContext> {
  return {
    id: `print-${spec.value}`,
    label: `列印${spec.label}`,
    icon: 'printer',
    enabledWhen: (ctx) => {
      if (ctx.selectedRows.length === 0) {
        return { enabled: false, reason: '請先選擇報名資料' };
      }
      // 普桌只能套用在 SignupType=4 的資料上（後端對單筆 by-id 驗證、批次強制 type=4）。
      // 不再看搜尋篩選，改驗證實際選取的每一列都是普桌；夾雜非普桌就擋下，避免印出錯位 PDF。
      if (spec.value === 'worship') {
        const nonWorship = ctx.selectedRows.filter((r) => r.signupType !== 4);
        if (nonWorship.length > 0) {
          return {
            enabled: false,
            reason: `選取含 ${nonWorship.length} 筆非普桌資料，僅普桌(類型 4)可列印`,
          };
        }
      }
      return true;
    },
    onClick,
  };
}

function openPdfInNewTab(blob: Blob, _fileName: string): void {
  const url = URL.createObjectURL(blob);
  window.open(url, '_blank', 'noopener');
  setTimeout(() => URL.revokeObjectURL(url), 60_000);
}

function downloadBlob(blob: Blob, fileName: string): void {
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = fileName;
  document.body.appendChild(a);
  a.click();
  document.body.removeChild(a);
  setTimeout(() => URL.revokeObjectURL(url), 0);
}

function toMessage(err: unknown): string {
  return err instanceof ApiError ? err.message : '操作失敗，請稍後再試';
}

function clamp(v: number, lo: number, hi: number): number {
  return Math.max(lo, Math.min(hi, v));
}

function loadShowAll(): boolean {
  try {
    return localStorage.getItem(LS_SHOW_ALL) === '1';
  } catch {
    return false;
  }
}

function loadColumnWidths(): Record<string, number> {
  try {
    const raw = localStorage.getItem(LS_COL_WIDTHS);
    if (!raw) return {};
    const parsed = JSON.parse(raw);
    if (typeof parsed !== 'object' || parsed === null) return {};
    const out: Record<string, number> = {};
    for (const [k, v] of Object.entries(parsed as Record<string, unknown>)) {
      if (typeof v === 'number' && v >= SIGNUP_COL_MIN_WIDTH && v <= SIGNUP_COL_MAX_WIDTH) {
        out[k] = v;
      }
    }
    return out;
  } catch {
    return {};
  }
}
