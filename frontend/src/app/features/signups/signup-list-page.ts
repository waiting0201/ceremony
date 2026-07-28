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
  viewChild,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
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
import { NumericInputDirective } from '../../shared/directives/numeric-input.directive';
import { openPdfInNewTab } from '../../shared/util/pdf';
import {
  SignupEditFormComponent,
  type InsertAtContext,
  type SignupSavedEvent,
} from './signup-edit-form.component';
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

// 垂直捲軸右鍵子選單（對齊舊系統 Windows 原生捲軸選單）：offsetY＝點擊位置相對 viewport 頂端的 px。
interface ScrollMenuContext {
  offsetY: number;
}

const REPORT_TYPES: { value: SingleReportType; label: string }[] = [
  { value: 'datacard', label: '資料卡' },
  { value: 'receipt', label: '收據' },
  { value: 'tablet', label: '薦牌' },
  { value: 'text', label: '文牒' },
  { value: 'worship', label: '普桌' },
  { value: 'worshipcard', label: '普桌資料卡' },
];

const ROW_HEIGHT = 26;
const LS_SHOW_ALL = 'ceremony.signupList.showAll';
const LS_COL_WIDTHS = 'ceremony.signupList.colWidths';

/**
 * 「全部」模式下要被停用的搜尋條件控制項（值保留、只是不生效）。
 * 不含 isAll 本身；showAll（顯示完整表格）是欄位顯隱、與條件無關故不停用。
 */
const CONDITION_CONTROLS = [
  'year',
  'isScope',
  'ceremonyCategoryId',
  'signupType',
  'number',
  'isFixedNumber',
  'searchKey',
  'scopeName',
  'scopeLivingName',
  'scopeDeadName',
  'scopePhone',
  'scopeRemark',
] as const;

/** 「全部」模式送出的查詢：所有條件留空 → 後端 WHERE 1=1，回傳全部報名。 */
const ALL_QUERY: SignupSearchQuery = {};

interface EditOverlayState {
  signupId: string | null;
  fromSignupId: string | null;
  // 插入模式（右鍵「在此前插入」）：帶目標群組 + 插入位置編號，走 InsertShift（後續編號 +1 順移）。
  insertAt?: InsertAtContext | null;
}

@Component({
  selector: 'app-signup-list-page',
  imports: [
    ReactiveFormsModule,
    ScrollingModule,
    IconComponent,
    FormOverlayComponent,
    SignupEditFormComponent,
    NumericInputDirective,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './signup-list-page.html',
  styleUrl: './signup-list-page.scss',
})
export class SignupListPage implements OnInit {
  private readonly destroyRef = inject(DestroyRef);
  @ViewChild('vp') protected vp?: CdkVirtualScrollViewport;
  @ViewChild('headerInner') protected headerInner?: ElementRef<HTMLElement>;
  // signal query（非 @ViewChild）：overlay actions 列要即時反映表單狀態（列印資料卡的 disabled），
  // 傳統 @ViewChild 不是 reactive，OnPush + zoneless 下綁到模板不會即時更新。
  protected readonly editForm = viewChild(SignupEditFormComponent);

  protected readonly editOverlay = signal<EditOverlayState | null>(null);
  protected readonly editFormDirty = signal(false);

  /**
   * 關閉 overlay 時要不要跳「未儲存的變更」確認（2026-07-27）。
   * 純新增模式的未完成內容會存成草稿、下次開新增報名自動帶回（見 signup-draft-state.ts），
   * 資料不會不見 → 不需要攔人。編輯 / 代入新增 / 插入模式不做草稿，維持原本的確認。
   */
  protected readonly overlayGuardsDirty = computed<boolean>(() => {
    const o = this.editOverlay();
    return !!o && (!!o.signupId || !!o.fromSignupId || !!o.insertAt);
  });

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

  // 自繪垂直捲軸（隱藏原生垂直捲軸）：原生捲軸的右鍵事件不會派送給網頁 JS，
  // 且 macOS 懸浮式捲軸寬度為 0，故無法攔截；改自繪才能跨平台支援「捲軸右鍵子選單」。
  private readonly vpQuery = viewChild<CdkVirtualScrollViewport>('vp');
  private readonly scrollTop = signal(0);
  private readonly viewportH = signal(0);
  protected readonly thumbHeight = computed(() => {
    const vh = this.viewportH();
    const contentH = this.results().length * this.rowHeight;
    if (contentH <= vh || vh === 0) return 0; // 內容未超出 → 不顯示捲軸
    return Math.max(24, (vh * vh) / contentH);
  });
  protected readonly thumbTop = computed(() => {
    const vh = this.viewportH();
    const contentH = this.results().length * this.rowHeight;
    const maxScroll = Math.max(0, contentH - vh);
    if (maxScroll <= 0) return 0;
    return (this.scrollTop() / maxScroll) * (vh - this.thumbHeight());
  });
  protected readonly showScrollbar = computed(() => this.thumbHeight() > 0);
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
  /** 關鍵字欄可否輸入＝範圍 5 項是否至少勾一個；預設全勾故初值 true。 */
  protected readonly keyEnabled = signal(true);

  /** 「全部」模式是否啟用（鏡射 form.isAll，供模板做樣式綁定；OnPush + zoneless 下 control.value 不是 reactive）。 */
  protected readonly allMode = signal(false);
  /** 進入「全部」模式前是否已搜尋過 → 決定取消勾選時要重查條件、還是回到未搜尋的空狀態。 */
  private searchedBeforeAll = false;

  protected readonly showAll = signal(loadShowAll());
  protected readonly columnWidths = signal<Record<string, number>>(loadColumnWidths());

  protected readonly selectedIds = signal<ReadonlySet<string>>(new Set());
  // shift 範圍選取的錨點：一般點擊 / 右鍵選列時更新，shift 點擊時不動（見 toggleRow）。
  private anchorIndex: number | null = null;
  /** 錨點成立當下的選取集合——shift 範圍以此為基準重算，讓範圍可縮小且不吃掉更早的選取。 */
  private anchorSelection: ReadonlySet<string> = new Set();

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
    // 「全部」：忽略下方所有搜尋條件、grid 直接顯示全部報名（條件值保留，取消勾選即還原）
    isAll: [false],
    year: [null as number | null, [Validators.min(1), Validators.max(999)]],
    isScope: [false],
    ceremonyCategoryId: [''],
    signupType: [-1],
    number: [null as number | null],
    isFixedNumber: [false],
    // 範圍 5 項預設全勾（2026-07-28 使用者指定，刻意不同於舊系統的全不勾）：
    // 打關鍵字就能搜，不必先勾欄位；關鍵字欄因此一開始就是啟用狀態（見 keyEnabled 初值）
    searchKey: [''],
    scopeName: [true],
    scopeLivingName: [true],
    scopeDeadName: [true],
    scopePhone: [true],
    scopeRemark: [true],
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
    // 自繪捲軸尺寸量測：viewport 出現或結果變動時重量，並以 ResizeObserver 追蹤版面/視窗縮放
    effect((onCleanup) => {
      const vp = this.vpQuery();
      this.results(); // 依賴：結果變動後重新量測 thumb 大小
      if (!vp) return;
      const el = vp.getElementRef().nativeElement;
      const measure = () => {
        this.viewportH.set(el.clientHeight);
        this.scrollTop.set(el.scrollTop);
      };
      requestAnimationFrame(measure);
      const ro = new ResizeObserver(measure);
      ro.observe(el);
      onCleanup(() => ro.disconnect());
    });
  }

  ngOnInit(): void {
    void this.loadCategories();
    this.bindScopeKeyToggle();
    this.bindAllModeToggle();
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

    // 「全部」模式一併跨路由還原（含條件控制項的停用狀態）
    this.allMode.set(cached.isAll);
    this.searchedBeforeAll = this.state.searchedBeforeAll();
    this.applyAllMode(cached.isAll);

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
    this.state.searchedBeforeAll.set(this.searchedBeforeAll);
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

  /**
   * 「全部」勾選 → 立即以空條件重查（grid 顯示全部報名）；取消勾選 → 立即用原條件重查還原。
   * 條件值全程保留在表單，只是在全部模式下被停用（getRawValue 仍讀得到）。
   */
  private bindAllModeToggle(): void {
    this.form.controls.isAll.valueChanges
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((on) => void this.onAllModeChanged(on));
  }

  private async onAllModeChanged(on: boolean): Promise<void> {
    this.allMode.set(on);
    this.applyAllMode(on);

    if (on) {
      this.searchedBeforeAll = this.hasSearched();
      await this.search();
      return;
    }

    if (this.searchedBeforeAll) {
      await this.search(); // 還原：用保留下來的條件重查
      return;
    }
    // 進全部模式前根本沒搜尋過 → 回到「請設定搜尋條件後點搜尋」的空狀態，不憑空跑一次無條件查詢
    this.results.set([]);
    this.total.set(0);
    this.hasSearched.set(false);
    this.clearSelection();
    this.saveToState();
  }

  /** 全部模式：停用（而非清空）所有條件控制項，讓「條件仍在、但此刻不生效」一目了然。 */
  private applyAllMode(on: boolean): void {
    for (const name of CONDITION_CONTROLS) {
      const ctrl = this.form.controls[name];
      if (on) ctrl.disable({ emitEvent: false });
      else ctrl.enable({ emitEvent: false });
    }
    // 離開全部模式時，關鍵字仍須依 scope* 勾選狀態決定可否輸入（見 bindScopeKeyToggle）
    if (!on && !this.keyEnabled()) {
      this.form.controls.searchKey.disable({ emitEvent: false });
    }
  }

  protected onViewportScroll(): void {
    if (!this.vp) return;
    const el = this.vp.getElementRef().nativeElement;
    if (this.headerInner) {
      this.headerInner.nativeElement.style.transform = `translateX(-${el.scrollLeft}px)`;
    }
    // 同步自繪垂直捲軸 thumb 位置
    this.scrollTop.set(el.scrollTop);
    this.viewportH.set(el.clientHeight);
  }

  /** 編號欄 ▲▼：對目標 control 做 ±1（下限 1），對齊舊系統 NumericUpDown。 */
  protected stepNumber(control: FormControl<number | null>, delta: number): void {
    if (control.disabled) return;
    const cur = control.value;
    const base = typeof cur === 'number' && Number.isFinite(cur) ? cur : 0;
    control.setValue(Math.max(1, base + delta));
    control.markAsDirty();
  }

  // ──────────── 自繪垂直捲軸互動 ────────────

  /** thumb 左鍵拖曳＝捲動。 */
  protected onThumbPointerDown(event: PointerEvent): void {
    if (event.button !== 0) return; // 只處理左鍵；右鍵交給 contextmenu
    event.preventDefault();
    event.stopPropagation(); // 避免觸發 track 的翻頁
    const startY = event.clientY;
    const startScroll = this.scrollTop();
    const range = this.viewportH() - this.thumbHeight();
    const max = this.maxScrollOffset();
    const onMove = (e: PointerEvent) => {
      if (range <= 0 || !this.vp) return;
      const next = clamp(startScroll + ((e.clientY - startY) / range) * max, 0, max);
      this.vp.scrollToOffset(next);
    };
    const onUp = () => {
      document.removeEventListener('pointermove', onMove);
      document.removeEventListener('pointerup', onUp);
    };
    document.addEventListener('pointermove', onMove);
    document.addEventListener('pointerup', onUp);
  }

  /** 點軌道空白處（非 thumb）＝往點擊方向翻一頁。 */
  protected onScrollbarPointerDown(event: PointerEvent, track: HTMLElement): void {
    if (event.button !== 0) return;
    const clickY = event.clientY - track.getBoundingClientRect().top;
    const dir = clickY < this.thumbTop() ? -1 : 1;
    this.scrollByOffset(dir * this.pageAmount());
  }

  /** 滑鼠滾輪移到捲軸上時仍能捲動內容。 */
  protected onScrollbarWheel(event: WheelEvent): void {
    event.preventDefault();
    this.scrollByOffset(event.deltaY);
  }

  /**
   * 右鍵點在自繪垂直捲軸上：開自訂捲動子選單（對齊舊 WinForms 原生捲軸選單）。
   * offsetY＝點擊位置相對軌道頂端（＝viewport 頂端）的 px，供「捲動到這裡」定位。
   */
  protected onScrollbarContextMenu(event: MouseEvent, track: HTMLElement): void {
    event.preventDefault();
    event.stopPropagation();
    this.menu.open<ScrollMenuContext>({
      origin: { x: event.clientX, y: event.clientY },
      items: this.buildScrollMenuItems(),
      context: { offsetY: event.clientY - track.getBoundingClientRect().top },
    });
  }

  private buildScrollMenuItems(): ContextMenuItem<ScrollMenuContext>[] {
    return [
      { id: 'scroll-here', label: '捲動到這裡', onClick: (c) => this.scrollHere(c.offsetY) },
      { id: 'sep-edge', label: '', divider: true, onClick: () => {} },
      { id: 'scroll-top', label: '頂端', onClick: () => this.scrollToEdge('top') },
      { id: 'scroll-bottom', label: '底部', onClick: () => this.scrollToEdge('bottom') },
      { id: 'sep-page', label: '', divider: true, onClick: () => {} },
      { id: 'page-up', label: '上一頁', onClick: () => this.scrollByOffset(-this.pageAmount()) },
      { id: 'page-down', label: '下一頁', onClick: () => this.scrollByOffset(this.pageAmount()) },
      { id: 'sep-line', label: '', divider: true, onClick: () => {} },
      { id: 'line-up', label: '向上捲動', onClick: () => this.scrollByOffset(-this.rowHeight) },
      { id: 'line-down', label: '向下捲動', onClick: () => this.scrollByOffset(this.rowHeight) },
    ];
  }

  private maxScrollOffset(): number {
    if (!this.vp) return 0;
    const total = this.results().length * this.rowHeight;
    return Math.max(0, total - this.vp.getViewportSize());
  }

  private pageAmount(): number {
    return this.vp ? this.vp.getViewportSize() : 0;
  }

  private scrollHere(offsetY: number): void {
    if (!this.vp) return;
    const size = this.vp.getViewportSize();
    const frac = size > 0 ? offsetY / size : 0;
    this.vp.scrollToOffset(clamp(frac * this.maxScrollOffset(), 0, this.maxScrollOffset()));
  }

  private scrollToEdge(edge: 'top' | 'bottom'): void {
    if (!this.vp) return;
    this.vp.scrollToOffset(edge === 'top' ? 0 : this.maxScrollOffset());
  }

  private scrollByOffset(delta: number): void {
    if (!this.vp) return;
    const cur = this.vp.measureScrollOffset('top');
    this.vp.scrollToOffset(clamp(cur + delta, 0, this.maxScrollOffset()));
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
    // 「全部」模式：忽略所有條件（搜尋與匯出 Excel 皆然），條件值仍留在表單供取消勾選後還原
    if (v.isAll) return ALL_QUERY;
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
      this.clearAnchor(); // 結果換掉了，舊 index 對不上新資料
      this.saveToState();
    } catch (err) {
      this.errorMessage.set(toMessage(err));
    } finally {
      this.loading.set(false);
    }
  }

  protected resetForm(): void {
    this.form.reset({
      isAll: false,
      year: null,
      isScope: false,
      ceremonyCategoryId: '',
      signupType: -1,
      number: null,
      isFixedNumber: false,
      searchKey: '',
      scopeName: true,
      scopeLivingName: true,
      scopeDeadName: true,
      scopePhone: true,
      scopeRemark: true,
    });
    this.form.controls.searchKey.enable({ emitEvent: false });
    this.keyEnabled.set(true);
    this.allMode.set(false);
    this.applyAllMode(false);
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

  /**
   * 列選取。一般點擊＝toggle 該列並把它設為**錨點**；shift + 點擊＝選取「錨點 ~ 本列」整段。
   *
   * shift 以「錨點當下的選取狀態」為基準重算（`anchorSelection`），而非疊加到現有選取，
   * 所以可以反覆 shift 點擊調整範圍（含**縮小**）；錨點前既有的選取則完整保留。
   * 錨點在 shift 期間刻意不移動——移動的話第二次 shift 會從上一個終點再往外長，範圍只能變大。
   */
  protected toggleRow(item: SignupListItem, event: MouseEvent | null, index: number): void {
    const items = this.results();
    if (event?.shiftKey && this.anchorIndex != null && this.anchorIndex < items.length) {
      const lo = Math.min(this.anchorIndex, index);
      const hi = Math.max(this.anchorIndex, index);
      const next = new Set(this.anchorSelection);
      for (let i = lo; i <= hi; i++) next.add(items[i].id);
      this.selectedIds.set(next);
      return;
    }

    const next = new Set(this.selectedIds());
    next.has(item.id) ? next.delete(item.id) : next.add(item.id);
    this.selectedIds.set(next);
    this.setAnchor(index, next);
  }

  /**
   * 列首 checkbox 的點擊：與點列同一套邏輯（含 shift 範圍選取）。
   * 走 click 而非 change——change 的 event 沒有 shiftKey，checkbox 就永遠吃不到 shift。
   * preventDefault 讓勾選狀態一律由 `selectedIds` 經 `[checked]` 決定，避免 DOM 自行翻轉造成不同步。
   */
  protected onCheckboxClick(item: SignupListItem, event: MouseEvent, index: number): void {
    event.stopPropagation(); // 別讓列的 (click) 再處理一次
    event.preventDefault();
    this.toggleRow(item, event, index);
  }

  /** shift + 點列會觸發瀏覽器的文字範圍選取（整片反白）；在 mousedown 擋掉，click 階段照常選列。 */
  protected onRowMouseDown(event: MouseEvent): void {
    if (event.shiftKey) event.preventDefault();
  }

  protected toggleAll(): void {
    const next = this.allSelected() ? new Set<string>() : new Set(this.results().map((r) => r.id));
    this.selectedIds.set(next);
    this.clearAnchor(); // 全選/全不選後舊錨點已無意義，下一次 shift 需重新指定起點
  }

  protected clearSelection(): void {
    this.selectedIds.set(new Set());
    this.clearAnchor();
  }

  private setAnchor(index: number, selection: ReadonlySet<string>): void {
    this.anchorIndex = index;
    this.anchorSelection = selection;
  }

  private clearAnchor(): void {
    this.anchorIndex = null;
    this.anchorSelection = new Set();
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
      const next = new Set([item.id]);
      this.selectedIds.set(next);
      this.setAnchor(index, next); // 右鍵選中的那列同時成為 shift 範圍的起點
    }
    this.menu.open<MenuContext>({
      origin: { x: event.clientX, y: event.clientY },
      items: this.buildMenuItems(),
      context: this.menuContext(item),
    });
  }

  protected openRowMenuFromButton(button: HTMLElement, item: SignupListItem, index: number): void {
    if (!this.selectedIds().has(item.id)) {
      const next = new Set([item.id]);
      this.selectedIds.set(next);
      this.setAnchor(index, next);
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
        id: 'insert-before',
        label: '在此前插入',
        icon: 'insert-above',
        enabledWhen: (ctx) =>
          (ctx.triggerRow.number != null) || { enabled: false, reason: '此列無編號可插入' },
        onClick: (ctx) => this.actionInsertBefore(ctx.triggerRow),
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

  /** 在此列前插入一筆新報名（該列與其後編號 +1 順移）。 */
  private actionInsertBefore(item: SignupListItem): void {
    if (item.number == null) return;
    this.editOverlay.set({
      signupId: null,
      fromSignupId: null,
      insertAt: {
        number: item.number,
        year: item.year,
        ceremonyCategoryId: item.ceremonyCategoryId,
        signupType: item.signupType,
      },
    });
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
    void this.editForm()?.submit();
  }

  /**
   * 存檔成功。列表一律重查（對齊舊 `NewSignupForm` 成功後呼叫 `signupForm.LoadSearchSignups()`）；
   * 是否關閉 overlay 由表單決定——新增類 `keepOpen=true`＝表單與已填資料原樣留著（2026-07-27 使用者指定）。
   */
  protected onOverlaySaved(e: SignupSavedEvent): void {
    if (!e.keepOpen) {
      this.editOverlay.set(null);
      this.editFormDirty.set(false);
    }
    void this.search();
  }

  protected onOverlayClose(): void {
    this.editOverlay.set(null);
    this.editFormDirty.set(false);
  }

  /**
   * overlay「取消」鈕（2026-07-21 使用者指定）：
   * 新增模式＝清成新的一筆（保留法會資料、不關閉表單、不跳頁）；
   * 編輯模式＝維持原本關閉 overlay 回列表。
   */
  protected onOverlayCancel(): void {
    if (this.editOverlay()?.signupId) {
      this.onOverlayClose();
    } else {
      this.editForm()?.resetBelow();
      this.editFormDirty.set(false);
    }
  }

  /** 列印剛新增那筆的資料卡（按鈕在 overlay actions 列、取消鈕左邊）。 */
  protected onPrintDataCard(): void {
    void this.editForm()?.printDataCard();
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
      openPdfInNewTab(blob);
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
      openPdfInNewTab(resp.blob);
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
      openPdfInNewTab(resp.blob);
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
      // 普桌／普桌資料卡與其他列印選項一致，不檢查 SignupType——
      // 對齊舊系統 tsmiPrintWorship：選什麼印什麼（2026-07-18 客訴解鎖）。
      return true;
    },
    onClick,
  };
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
