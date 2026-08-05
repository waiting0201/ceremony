import {
  ChangeDetectionStrategy,
  Component,
  computed,
  DestroyRef,
  effect,
  inject,
  input,
  OnInit,
  output,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormArray, FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { combineLatest, debounceTime, distinctUntilChanged, map, startWith } from 'rxjs';
import { SignupApi } from '../../core/api/signups/signup.api';
import type {
  CreateSignupRequest,
  SignupDuplicateItem,
  SignupListItem,
} from '../../core/api/signups/signup.models';
import { CategoryApi } from '../../core/api/categories/category.api';
import type { CategoryNode } from '../../core/api/categories/category.models';
import { BelieverApi } from '../../core/api/believers/believer.api';
import type { BelieverListItem } from '../../core/api/believers/believer.models';
import { ZipcodeApi } from '../../core/api/zipcodes/zipcode.api';
import type { ZipcodeAreaItem } from '../../core/api/zipcodes/zipcode.models';
import { PrintService } from '../../core/print/print.service';
import { toMessage } from '../../core/errors/to-message';
import { SIGNUP_TYPES, signupTypeLabel } from '../../shared/util/signup-type';
import { flattenCategories, type FlatCategory } from '../../shared/util/categories';
import { currentTaiwanYear } from '../../shared/util/taiwan-year';
import { currentSeason, resolveSeasonRootId } from '../../shared/util/ceremony-season';
import { NumericInputDirective } from '../../shared/directives/numeric-input.directive';
import { ConfirmDialogService } from '../../shared/confirm-dialog/confirm-dialog.service';
import { SignupDraftState, type SignupDraft } from './signup-draft-state';

/**
 * 報名 create/edit 表單（不含 page layout / overlay shell）。
 *
 * 表單編排對齊舊 NewSignupForm.cs（單頁呈現，非兩步驟；mockup v4 決議單頁）：
 * 信眾搜尋（常駐結果列表）→ 法會資料 → 基本資料 → 地址（寄件上/文牒下）→ 名單（往生上/陽上下）→ 編號/費用 → 備註/預繳。
 * 2026-07-04：視覺上改雙欄密集排版節省高度、避免整頁垂直捲動（見 signup-edit-form.component.html/scss）。
 * 2026-07-17：信眾搜尋由 modal picker 改回舊系統式常駐 in-form 列表（頂部全寬），
 * 地址/名單改上下堆疊對齊舊 Designer 版面，未選信眾送出自動先建新信眾（同舊 btnConfirm）。
 *
 * - signupId 有值 → 編輯模式
 * - fromSignupId 有值 → 代入新增模式（不帶 year/ceremony/type）。2026-08-05 起會自動把來源
 *   姓名填進搜尋框、跑一次信眾搜尋並選中來源列，對齊舊 btnNextStep_Click:97-111「//代入新增」段
 * - 兩者都 null → 純新增模式（唯一會存跨路由草稿的模式，見 signup-draft-state.ts）
 *
 * 由外部容器（route page / overlay）呼叫 `submit()` 觸發儲存；成功 emit `saved`。
 */
/** 插入模式（列表右鍵「在此前插入」）帶入的目標群組與插入位置編號。 */
export interface InsertAtContext {
  number: number;
  year: number;
  ceremonyCategoryId: string;
  signupType: number;
}

/**
 * 儲存成功事件。
 * `keepOpen`＝新增類（非編輯、非插入）存檔後**保留表單與已填資料、不關閉**，對齊舊
 * `NewSignupForm.btnConfirm_Click:355-361`（跳「編號X，新增報名成功」後按鈕重新啟用，
 * 表單原樣留著可接著列印資料卡）。host 收到後一律重查列表。
 */
export interface SignupSavedEvent {
  keepOpen: boolean;
}

@Component({
  selector: 'app-signup-edit-form',
  imports: [ReactiveFormsModule, NumericInputDirective],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './signup-edit-form.component.html',
  styleUrl: './signup-edit-form.component.scss',
})
export class SignupEditFormComponent implements OnInit {
  private readonly api = inject(SignupApi);
  private readonly categoryApi = inject(CategoryApi);
  private readonly believerApi = inject(BelieverApi);
  private readonly zipcodeApi = inject(ZipcodeApi);
  private readonly print = inject(PrintService);
  private readonly fb = inject(FormBuilder);
  private readonly draftState = inject(SignupDraftState);
  private readonly confirmDialog = inject(ConfirmDialogService);

  private readonly destroyRef = inject(DestroyRef);

  readonly signupId = input<string | null>(null);
  readonly fromSignupId = input<string | null>(null);
  // 插入模式（列表右鍵「在此前插入」）：帶入目標群組 + 插入位置編號，走 InsertShift（後續編號 +1 順移）。
  readonly insertAt = input<InsertAtContext | null>(null);
  readonly saved = output<SignupSavedEvent>();
  readonly cancelled = output<void>();
  readonly dirtyChange = output<boolean>();

  protected readonly signupTypes = SIGNUP_TYPES;
  protected readonly categories = signal<CategoryNode[]>([]);
  protected readonly flatCategories = computed<FlatCategory[]>(() =>
    flattenCategories(this.categories()),
  );

  protected readonly loading = signal(false);
  protected readonly saving = signal(false);
  /**
   * 剛新增成功那一筆的 id（＝舊 `NewSignupForm.CurrentSignupID`）。**public**：按鈕在 host 的
   * overlay actions 列（取消鈕左邊），由 host 讀這個 signal 決定 disabled。
   * 「列印資料卡」在存檔前 disabled、存檔後才能按，對齊舊 btnPrintDataCard
   * （進表單時 `Enabled = false`:95 → btnConfirm 成功後 `Enabled = true`:361）。
   */
  readonly lastCreatedSignupId = signal<string | null>(null);
  readonly printingDataCard = signal(false);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly selectedBeliever = signal<BelieverListItem | null>(null);

  // 員工類型 / 固定編號 / 堂號改為 per-signup 可編輯（2026-07-21），改由 form control 持有（見下方 form）。
  // 選信眾時帶入該信眾現值當預設，之後只改「這筆報名」、不回寫 Believer（後端寫 Signups 自有欄，
  // SignupView 以 COALESCE 回退信眾值）。見 docs/blueprints/signup-hallname-isolation.md（方案 A）。

  // 城市 / 區域連動下拉資料
  protected readonly cities = signal<string[]>([]);
  protected readonly mailAreas = signal<ZipcodeAreaItem[]>([]);
  protected readonly textAreas = signal<ZipcodeAreaItem[]>([]);
  protected readonly mailZipcode = signal('');
  protected readonly textZipcode = signal('');

  // 重複報名警示：選定信眾在同年同法會（忽略報名類型）既有的報名；僅警示、不阻擋
  protected readonly duplicates = signal<SignupDuplicateItem[]>([]);
  protected readonly checkingDuplicates = signal(false);

  protected readonly believerSearchTerm = signal('');
  protected readonly believerSearchResults = signal<SignupListItem[]>([]);
  protected readonly believerSearching = signal(false);
  protected readonly believerHasSearched = signal(false);
  /** 常駐結果列表中目前選定的列（高亮用；選定後列表保留，可隨時改選，對齊舊 dgvBelievers）。 */
  protected readonly pickedRowId = signal<string | null>(null);

  protected readonly mode = computed<'create' | 'edit'>(() =>
    this.signupId() ? 'edit' : 'create',
  );
  // 插入模式：非編輯、且帶 insertAt。年/法會/類型鎖定為目標群組。
  protected readonly isInsert = computed<boolean>(() => !this.signupId() && !!this.insertAt());

  /**
   * 純新增模式（非編輯 / 非代入新增 / 非插入）＝ 唯一會存草稿的模式。
   * 其他三種模式各有自己的資料來源（既有報名、來源報名、插入群組），還原草稿只會互相打架。
   * 見 signup-draft-state.ts。
   */
  private readonly isPlainCreate = computed<boolean>(
    () => !this.signupId() && !this.fromSignupId() && !this.insertAt(),
  );

  protected readonly form = this.fb.nonNullable.group({
    // 法會資料（舊 Step1）
    year: [currentTaiwanYear(), [Validators.required, Validators.min(1)]],
    ceremonyCategoryId: ['', [Validators.required]],
    signupType: [1, [Validators.required]],
    // 信眾（非必填：未選信眾時送出會自動建立新信眾，對齊舊 btnConfirm_Click:186-223）
    believerId: [''],
    // 基本資料。員工類型/固定編號/堂號為 per-signup 可編輯欄（2026-07-21）：選信眾帶入現值當預設，
    // 只改這筆報名、不回寫 Believer。employeeType 1=非員工 2=大殿 3=地藏殿。
    name: ['', [Validators.required, Validators.maxLength(50)]],
    phone: [''],
    employeeType: [1, [Validators.required, Validators.min(1), Validators.max(3)]],
    isFixedNumber: [false],
    hallName: ['', [Validators.maxLength(10)]],
    // 地址（城市/區域連動；zipcodeId 以字串持有，submit 轉 number）
    mailCity: [''],
    mailZipcodeId: [''],
    // 地址非必填（2026-07-21 使用者指定）：僅長度限制，不再 required。
    mailAddress: ['', [Validators.maxLength(200)]],
    sameMailAddress: [false],
    textCity: [''],
    textZipcodeId: [''],
    textAddress: ['', [Validators.maxLength(200)]],
    // 名單
    livingNames: this.fb.array(Array.from({ length: 6 }, () => this.fb.control(''))),
    deadNames: this.fb.array(Array.from({ length: 6 }, () => this.fb.control(''))),
    // 編號 / 費用
    keepNumber: [false],
    customNumber: [null as number | null],
    fee: [null as number | null],
    // 備註 / 預繳
    remark: [''],
    prepayYear: [null as number | null],
    prepayCeremonyCategoryId: [''],
  });

  protected get livingArray(): FormArray { return this.form.controls.livingNames; }
  protected get deadArray(): FormArray { return this.form.controls.deadNames; }

  /** 對外暴露：表單是否髒（給 overlay 判斷是否要確認再關閉） */
  get isDirty(): boolean { return this.form.dirty; }

  constructor() {
    void this.loadCategories();
    void this.loadCities();
    effect(() => {
      const id = this.signupId();
      if (id) void this.loadExisting(id);
    });
    effect(() => {
      const fromId = this.fromSignupId();
      if (fromId && !this.signupId()) void this.prefillFromSignup(fromId);
    });
    effect(() => {
      const ins = this.insertAt();
      if (ins && !this.signupId()) this.applyInsertContext(ins);
    });
    this.form.valueChanges
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.dirtyChange.emit(this.form.dirty));

    // 重複報名警示：year / 法會 / 信眾 任一變動就（去抖後）重查。
    // pickBeliever / applyItem 都是透過 patchValue 改 believerId，會觸發此處。
    combineLatest([
      this.form.controls.year.valueChanges.pipe(startWith(this.form.controls.year.value)),
      this.form.controls.ceremonyCategoryId.valueChanges.pipe(
        startWith(this.form.controls.ceremonyCategoryId.value),
      ),
      this.form.controls.believerId.valueChanges.pipe(
        startWith(this.form.controls.believerId.value),
      ),
    ])
      .pipe(
        debounceTime(300),
        map(([year, ceremonyCategoryId, believerId]) => ({ year, ceremonyCategoryId, believerId })),
        distinctUntilChanged(
          (a, b) =>
            a.year === b.year &&
            a.ceremonyCategoryId === b.ceremonyCategoryId &&
            a.believerId === b.believerId,
        ),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe(() => void this.checkDuplicates());

    // 編號欄恆顯示、僅在勾「指定編號」時可編輯（2026-07-27 使用者指定，對齊舊
    // cbKeepNumber_CheckedChanged:139-149 的 `txtNumber.Enabled = cbKeepNumber.Checked`）。
    this.form.controls.keepNumber.valueChanges
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.syncCustomNumberEnabled());

    // 元件銷毀（關 overlay / 切到其他功能頁）時保存未完成的新增內容，見 ngOnInit 的還原。
    this.destroyRef.onDestroy(() => this.saveDraft());
  }

  /**
   * 編號欄的啟用狀態：新增類跟著「指定編號」勾選走（未勾＝disabled，仍看得到欄位）；
   * 編輯模式編號恆可改（舊 EditSignupForm 亦然），一律 enable。
   * disabled control 的值不進 `form.value`，但 submit 走 `getRawValue()` 故不受影響。
   */
  private syncCustomNumberEnabled(): void {
    const ctrl = this.form.controls.customNumber;
    const enabled = this.mode() === 'edit' || this.form.controls.keepNumber.value;
    if (enabled) ctrl.enable({ emitEvent: false });
    else ctrl.disable({ emitEvent: false });
  }

  /**
   * 純新增模式：把上次未完成的內容原樣帶回（靜默，不顯示提示列）。
   * 早於 loadCategories() 的 applySeasonDefault()，且該方法「已有值就不覆蓋」，故不會蓋掉草稿的法會。
   */
  async ngOnInit(): Promise<void> {
    this.syncCustomNumberEnabled(); // inputs 已就緒（mode 才判得準）
    if (!this.isPlainCreate()) return;
    const draft = this.draftState.draft();
    if (draft) await this.applyDraft(draft);
  }

  private async applyDraft(draft: SignupDraft): Promise<void> {
    const v = draft.value;
    this.selectedBeliever.set(draft.selectedBeliever);
    this.pickedRowId.set(draft.pickedRowId);
    this.believerSearchTerm.set(draft.believerSearchTerm);
    this.believerSearchResults.set(draft.believerSearchResults);
    this.believerHasSearched.set(draft.believerHasSearched);
    this.lastCreatedSignupId.set(draft.lastCreatedSignupId);
    this.form.patchValue(v);
    this.livingArray.setValue(pad6(v.livingNames));
    this.deadArray.setValue(pad6(v.deadNames));
    // 地址：區域下拉選項是依城市即時載入的（不入草稿），故重跑一次連動把選項與郵遞區號補回來。
    await this.applyAddress(
      'mail', v.mailCity || null, v.mailZipcodeId ? Number(v.mailZipcodeId) : null,
      null, v.mailAddress,
    );
    await this.applyAddress(
      'text', v.textCity || null, v.textZipcodeId ? Number(v.textZipcodeId) : null,
      null, v.textAddress,
    );
    this.form.controls.sameMailAddress.setValue(v.sameMailAddress);
    // 髒/乾淨照離開當下還原（patchValue 本身不會標髒），讓 host 的 dirty 狀態與畫面一致。
    if (draft.dirty) this.form.markAsDirty();
    else this.form.markAsPristine();
    this.dirtyChange.emit(draft.dirty);
    // 重複報名警示由 valueChanges → checkDuplicates 自動重查，不需入草稿。
  }

  /**
   * 純新增模式離開時**無條件**快照當下畫面（2026-07-28 使用者定案：回來要跟離開前一樣）。
   * 不再看 `form.dirty`——存檔成功後與按「取消」後的表單都是 pristine，但畫面上有東西，
   * 過門檻擋掉就會「看得到卻帶不回來」。空白表單被存起來也無妨（還原＝一樣是空白）。
   */
  private saveDraft(): void {
    if (!this.isPlainCreate()) return;
    this.draftState.save({
      value: this.form.getRawValue(),
      selectedBeliever: this.selectedBeliever(),
      pickedRowId: this.pickedRowId(),
      believerSearchTerm: this.believerSearchTerm(),
      believerSearchResults: this.believerSearchResults(),
      believerHasSearched: this.believerHasSearched(),
      lastCreatedSignupId: this.lastCreatedSignupId(),
      dirty: this.form.dirty,
    });
  }

  /**
   * 查選定信眾在同年同法會（忽略報名類型）是否已有報名 → 警示用。
   * 三鍵未齊則清空；編輯模式排除自己這筆。便利功能，失敗不阻斷流程。
   */
  private async checkDuplicates(): Promise<void> {
    const year = this.form.controls.year.value;
    const ceremonyCategoryId = this.form.controls.ceremonyCategoryId.value;
    const believerId = this.form.controls.believerId.value;
    if (!year || year <= 0 || !ceremonyCategoryId || !believerId) {
      this.duplicates.set([]);
      return;
    }
    this.checkingDuplicates.set(true);
    try {
      const resp = await this.api.checkDuplicates({
        year,
        ceremonyCategoryId,
        believerId,
        excludeSignupId: this.signupId(),
      });
      this.duplicates.set(resp.items);
    } catch {
      this.duplicates.set([]);
    } finally {
      this.checkingDuplicates.set(false);
    }
  }

  /** 警示逐筆用：報名類型代碼 → 顯示名稱（沿用共用 helper）。 */
  protected readonly signupTypeLabel = signupTypeLabel;

  /** 警示標題用：依目前選定的法會 id 取名稱。 */
  protected selectedCeremonyTitle(): string {
    const id = this.form.controls.ceremonyCategoryId.value;
    return this.flatCategories().find((c) => c.id === id)?.title ?? '';
  }

  private async loadCategories(): Promise<void> {
    try {
      const resp = await this.categoryApi.list();
      this.categories.set(resp.items);
      this.applySeasonDefault();
    } catch (err) {
      this.errorMessage.set(toMessage(err));
    }
  }

  /**
   * 新增模式：依當月自動帶出季別 root（春季/中元/秋季）作為可編輯的預設；
   * 子法會仍由使用者挑選。月→季對照見 docs/business-rules-implicit.md。
   */
  private applySeasonDefault(): void {
    if (this.mode() !== 'create') return;                    // 編輯模式不覆蓋既有值
    if (this.form.controls.ceremonyCategoryId.value) return; // 已有值（含使用者已選）不覆蓋
    const rootId = resolveSeasonRootId(this.categories(), currentSeason());
    if (rootId) this.form.controls.ceremonyCategoryId.setValue(rootId);
  }

  private async loadCities(): Promise<void> {
    try {
      const resp = await this.zipcodeApi.cities();
      this.cities.set(resp.items);
    } catch (err) {
      this.errorMessage.set(toMessage(err));
    }
  }

  private async loadExisting(id: string): Promise<void> {
    this.loading.set(true);
    try {
      const item = await this.api.getById(id);
      await this.applyItem(item);
      this.form.markAsPristine();
    } catch (err) {
      this.errorMessage.set(toMessage(err));
    } finally {
      this.loading.set(false);
    }
  }

  /**
   * 代入新增：以來源報名預填表單。對齊舊 `NewSignupForm.btnNextStep_Click:97-111`「//代入新增」段——
   * 姓名填進搜尋框 → 跑一次信眾搜尋（舊 `LoadBelievers()`）→ 在結果中找到來源那一列 →
   * 選中它（舊 `dgvRow.Selected = true` + `BelieverSelected(dgvRow)`＝我們的 `applyBelieverRow`）。
   * 少了這段的話搜尋框空白、結果表不渲染、無高亮列，使用者也無法就地改選同信眾的別筆報名。
   *
   * 走 `applyBelieverRow` 而非自己 patchValue，同時讓兩條「從一筆報名帶資料」的路徑一致：
   * 預繳（`prepayYear`/`prepayCeremonyCategoryId`）、地址整段為空時退回信眾主檔、`selectedBeliever`
   * 取主檔而非 stub。年/法會/類型/編號/費用兩條路徑都不帶（那是新的一筆要自己決定的）。
   *
   * 三層 token 分工：`prefillToken`（input 又變 → 整條放棄）、`believerSearchToken`（使用者搶先按
   * 搜尋 → rows 為 null → 不選列）、`pickToken`（使用者搶先點別列 → 連 fallback 都不跑）。
   */
  private async prefillFromSignup(signupId: string): Promise<void> {
    const token = ++this.prefillToken;
    const pickTokenAtStart = this.pickToken;
    this.loading.set(true);
    try {
      const item = await this.api.getById(signupId);
      if (token !== this.prefillToken) return;

      let picked = false;
      const term = (item.name ?? '').trim();
      if (term) {
        // 舊 `if(ParamName != null && ParamName != string.Empty)`：姓名為空就完全不發搜尋
        this.believerSearchTerm.set(term);
        const rows = await this.searchBelievers(term, signupId);
        if (token !== this.prefillToken) return;
        const source = rows?.find((r) => r.id === signupId) ?? null;
        if (source) picked = await this.applyBelieverRow(source, { markDirty: false });
        if (token !== this.prefillToken) return;
      }
      // fallback：姓名為空 / 搜尋失敗 / 來源列無 believerId。使用者若已搶先自己點過列
      // （pickToken 變了）就尊重使用者的選擇，不覆蓋。
      if (!picked && this.pickToken === pickTokenAtStart) await this.applyPrefillItem(item);
      if (token !== this.prefillToken) return;

      // 代入的內容全部來自系統，使用者一個字都沒打 → 維持 pristine，關 overlay 不跳未儲存確認。
      // `markAsPristine()` 不會走 valueChanges，要主動同步 host 的 editFormDirty。
      this.form.markAsPristine();
      this.dirtyChange.emit(false);
    } catch (err) {
      this.errorMessage.set(toMessage(err));
    } finally {
      if (token === this.prefillToken) this.loading.set(false);
    }
  }

  /** 代入新增在結果列中找不到來源列時的退路：直接以該筆報名的欄位填表（無高亮列可設）。 */
  private async applyPrefillItem(item: SignupListItem): Promise<void> {
    this.form.patchValue({
      believerId: item.believerId ?? '',
      name: item.name ?? '',
      phone: item.phone ?? '',
      // per-signup 覆寫欄：帶回該筆報名自身值（2026-07-21）
      employeeType: item.employeeType ?? 1,
      isFixedNumber: item.isFixedNumber,
      hallName: item.hallName ?? '',
      remark: item.remark ?? '',
      // 預繳與 applyBelieverRow 同規則（帶該筆自身值）；先前這裡漏帶，兩條路徑不一致
      prepayYear: item.prepayYear,
      prepayCeremonyCategoryId: item.prepayCeremonyCategoryId ?? '',
    });
    await this.applyAddress('mail', item.mailCity, null, item.mailZone, item.mailAddress);
    await this.applyAddress('text', item.textCity, null, item.textZone, item.textAddress);
    this.livingArray.setValue(pad6(item.livingNames));
    this.deadArray.setValue(pad6(item.deadNames));
    this.selectedBeliever.set(makeBelieverStubFromSignup(item));
  }

  private async applyItem(item: SignupListItem): Promise<void> {
    this.form.patchValue({
      year: item.year,
      ceremonyCategoryId: item.ceremonyCategoryId,
      signupType: item.signupType,
      believerId: item.believerId ?? '',
      name: item.name ?? '',
      // per-signup 覆寫欄：帶回該筆報名自身值（2026-07-21）
      employeeType: item.employeeType ?? 1,
      isFixedNumber: item.isFixedNumber,
      hallName: item.hallName ?? '',
      keepNumber: false,
      customNumber: item.number,
      fee: item.fee,
      phone: item.phone ?? '',
      remark: item.remark ?? '',
      prepayYear: item.prepayYear,
      prepayCeremonyCategoryId: item.prepayCeremonyCategoryId ?? '',
    });
    await this.applyAddress('mail', item.mailCity, null, item.mailZone, item.mailAddress);
    await this.applyAddress('text', item.textCity, null, item.textZone, item.textAddress);
    this.livingArray.setValue(pad6(item.livingNames));
    this.deadArray.setValue(pad6(item.deadNames));
    this.selectedBeliever.set(makeBelieverStubFromSignup(item));
  }

  // ── 城市 / 區域連動 ───────────────────────────────────────────────

  /** 城市下拉變更（使用者操作）→ 載入該城市區域、清空已選區域。 */
  protected async onCityChange(kind: 'mail' | 'text'): Promise<void> {
    const city = kind === 'mail'
      ? this.form.controls.mailCity.value
      : this.form.controls.textCity.value;
    await this.applyAddress(kind, city, null, null,
      kind === 'mail' ? this.form.controls.mailAddress.value : this.form.controls.textAddress.value);
  }

  /** 區域下拉變更 → 更新顯示的郵遞區號。 */
  protected onAreaChange(kind: 'mail' | 'text'): void {
    this.refreshZipcode(kind);
  }

  /**
   * 設定某地址區塊（城市 → 載入區域 → 選定區域 → 地址）。
   * zipcodeId 優先；無 zipcodeId 時退而以區域名稱比對（編輯既有報名只存 city/area 字串）。
   */
  private async applyAddress(
    kind: 'mail' | 'text',
    city: string | null,
    zipcodeId: number | null,
    areaName: string | null,
    address: string | null,
    /** 載入區域清單期間若條件已被更新的操作取代（如快速改選信眾）→ 放棄寫入，避免蓋掉新的選擇。 */
    isStale?: () => boolean,
  ): Promise<void> {
    const cityCtrl = kind === 'mail' ? this.form.controls.mailCity : this.form.controls.textCity;
    const zipCtrl = kind === 'mail' ? this.form.controls.mailZipcodeId : this.form.controls.textZipcodeId;
    const addrCtrl = kind === 'mail' ? this.form.controls.mailAddress : this.form.controls.textAddress;

    cityCtrl.setValue(city ?? '');
    addrCtrl.setValue(address ?? '');

    let areas: ZipcodeAreaItem[] = [];
    if (city) {
      try {
        const resp = await this.zipcodeApi.areas(city);
        areas = resp.items;
      } catch (err) {
        this.errorMessage.set(toMessage(err));
      }
    }
    if (isStale?.()) return;
    if (kind === 'mail') this.mailAreas.set(areas);
    else this.textAreas.set(areas);

    let selectedId = '';
    if (zipcodeId != null && areas.some((a) => a.zipcodeId === zipcodeId)) {
      selectedId = String(zipcodeId);
    } else if (areaName) {
      const match = areas.find((a) => a.area === areaName);
      if (match) selectedId = String(match.zipcodeId);
    }
    zipCtrl.setValue(selectedId);
    this.refreshZipcode(kind);
  }

  private refreshZipcode(kind: 'mail' | 'text'): void {
    const areas = kind === 'mail' ? this.mailAreas() : this.textAreas();
    const id = kind === 'mail'
      ? this.form.controls.mailZipcodeId.value
      : this.form.controls.textZipcodeId.value;
    const zip = areas.find((a) => String(a.zipcodeId) === id)?.zipcode ?? '';
    if (kind === 'mail') this.mailZipcode.set(zip);
    else this.textZipcode.set(zip);
  }

  /**
   * 同寄件地址 checkbox（對齊舊 cbSameMailAddress_CheckedChanged）。
   *
   * **刻意偏離舊系統**（2026-07-31 使用者指定）：舊 NewSignupForm.cs:477-502 要求
   * `txtMailAddress.Text.Trim() != ""` 才肯同步，否則跳「請先輸入寄件地址」並彈回勾選。
   * 但地址自 2026-07-21 起已非必填，只選了城市/區域、地址欄留空是合法狀態，這時同步城市/區域
   * 一樣有意義。改成三者（城市/區域/地址）全空才擋。
   */
  protected async onSameMailAddressChange(): Promise<void> {
    const checked = this.form.controls.sameMailAddress.value;
    if (checked) {
      const mailCity = this.form.controls.mailCity.value;
      const mailZipId = this.form.controls.mailZipcodeId.value;
      const mailAddr = this.form.controls.mailAddress.value.trim();
      if (!mailCity && !mailZipId && !mailAddr) {
        this.form.controls.sameMailAddress.setValue(false);
        this.errorMessage.set('請先填寫寄件地址（城市／區域或地址）');
        return;
      }
      this.errorMessage.set(null);
      const mailZipNum = mailZipId ? Number(mailZipId) : null;
      await this.applyAddress('text', mailCity, mailZipNum, null, mailAddr);
    } else {
      await this.applyAddress('text', '', null, null, '');
    }
  }

  // ── 信眾搜尋（常駐 in-form 列表，對齊舊 txtQ + dgvBelievers）──────────

  private believerSearchToken = 0;
  /** 改選信眾的 in-flight guard（見 pickBeliever）。 */
  private pickToken = 0;
  /** 代入新增的 in-flight guard（見 prefillFromSignup）。 */
  private prefillToken = 0;

  /** 輸入只更新框內文字，不打 API；對齊舊 NewSignupForm 按「搜尋」鍵才查詢 */
  protected onBelieverSearchInput(term: string): void {
    this.believerSearchTerm.set(term);
    this.believerHasSearched.set(false);
  }

  protected onBelieverSearchKeydown(event: KeyboardEvent): void {
    if (event.key === 'Enter') {
      event.preventDefault();
      this.triggerBelieverSearch();
    }
  }

  /** 對齊舊 NewSignupForm.cs:114-124（btnBelieverSearch_Click） */
  protected triggerBelieverSearch(): void {
    const trimmed = this.believerSearchTerm().trim();
    if (!trimmed) {
      this.believerSearchResults.set([]);
      this.believerHasSearched.set(false);
      return;
    }
    void this.searchBelievers(trimmed);
  }

  /**
   * 由程式觸發的信眾搜尋（＝舊 `LoadBelievers()`），與使用者按「搜尋」共用同一組 token 與旗標。
   * `believerSearchTerm` 刻意不在這裡設——使用者路徑不該被 trim 後的值回寫輸入框，
   * 代入新增路徑自己設（見 `prefillFromSignup`）。
   */
  private async searchBelievers(
    trimmed: string,
    pinSignupId?: string | null,
  ): Promise<SignupListItem[] | null> {
    this.believerSearching.set(true);
    this.believerHasSearched.set(true);
    return this.runBelieverSearch(trimmed, pinSignupId);
  }

  /**
   * @param pinSignupId 代入新增用：確保來源那筆報名一定在結果內（不被 200 列上限切掉）。
   * @returns 結果列；null＝這次查詢已過期（使用者又搜了別的）或查詢失敗。呼叫端**不要**改讀
   *          `believerSearchResults()`——stale 時那裡可能已是使用者自己搜的結果。
   */
  private async runBelieverSearch(
    trimmed: string,
    pinSignupId?: string | null,
  ): Promise<SignupListItem[] | null> {
    const token = ++this.believerSearchToken;
    try {
      // 兩路併查後合併，等效舊 BelieverView（Believers LEFT JOIN Signups）：
      //  (a) /signups：每筆報名一列（同一信眾報過幾次就幾列）
      //  (b) /believers?searchKey=：信眾主檔，用來補「從未報名過的信眾」——舊系統 LoadBelievers 由
      //      SignupView 換成 BelieverView 正是為了這個（註解「如果沒有報名過就查不到」）
      // 兩邊都是同一把關鍵字 OR 比對 Name/Phone/6組陽上/6組往生 14 欄（舊 NewSignupForm.cs:715-722）
      const [signupResp, believerResp] = await Promise.all([
        this.api.search({
          searchKey: trimmed,
          scopeName: true,
          scopePhone: true,
          scopeLivingName: true,
          scopeDeadName: true,
        }),
        this.believerApi.search({ searchKey: trimmed }),
      ]);
      if (token !== this.believerSearchToken) return null; // 舊查詢的回應，畫面已經換了輸入內容
      // /signups 依 Year/CeremonySort/NumberTitle/Number 全部 ascending 排序；反轉近似舊系統「新的在前」。
      // 常駐列表只 render 前 N 列（不顯示截斷提示，2026-07-17 使用者指定拿掉）：
      // 模糊字（如單字「陳」）可命中 2 萬+ 列，全部塞進 DOM 會卡死頁面
      const signupRows = signupResp.items.slice().reverse().slice(0, MAX_BELIEVER_RESULT_ROWS);
      // 代入新增：來源那筆若被上限切掉就沒有列可選中/高亮，使用者也看不到自己是從哪一筆代入的。
      // 破序把它提到最前（舊系統無列數上限故無此問題，屬刻意偏離的加值）。
      if (pinSignupId && !signupRows.some((r) => r.id === pinSignupId)) {
        const pinned = signupResp.items.find((r) => r.id === pinSignupId);
        if (pinned) signupRows.unshift(pinned);
      }
      // 未報名過的信眾接在最後（舊 BelieverView 依 Year desc 排序，Year 為 null 的本來就墊底）。
      // 另有獨立額度，不與報名列互相排擠——這批正是最需要被找到的（要幫他報名）。
      const withSignup = new Set(signupResp.items.map((r) => r.believerId));
      const believerOnlyRows = believerResp.items
        .filter((b) => !withSignup.has(b.id))
        .slice(0, MAX_BELIEVER_ONLY_ROWS)
        .map(makeSignupRowFromBeliever);
      const rows = [...signupRows, ...believerOnlyRows];
      this.believerSearchResults.set(rows);
      return rows;
    } catch (err) {
      if (token !== this.believerSearchToken) return null;
      this.errorMessage.set(toMessage(err));
      return null;
    } finally {
      if (token === this.believerSearchToken) this.believerSearching.set(false);
    }
  }

  /**
   * 點選結果列 → 以「點到的那筆報名」覆蓋整張表單（對齊舊 dgvBelievers_CellClick + BelieverSelected:991-1101）。
   * 列表保留不關閉，可隨時再點別筆改選（每次改選都重新覆蓋欄位，同舊系統）。
   *
   * 2026-07-27 客訴修正：先前版本一律帶信眾主檔（Believers）資料，導致點哪一筆報名都拿到同一份舊資料。
   * 舊系統帶的是該筆報名自身的姓名/電話/地址/名單/備註（Signups 快照，每次報名可不同），
   * 信眾主檔只當該欄為空時的 fallback（同舊 `signup != null ? signup.X : believer.X` 分支）。
   * 年份/法會/報名類型/編號/費用不帶（那是新的一筆要自己決定的）；預繳自 2026-07-31 起改為「帶」的一組。
   */
  protected pickBeliever(row: SignupListItem): Promise<boolean> {
    return this.applyBelieverRow(row, { markDirty: true });
  }

  /**
   * 以某列報名覆蓋表單。使用者點列與系統代入（代入新增）共用同一段邏輯，只差在要不要標髒。
   *
   * @param markDirty 使用者親手點列＝實質輸入 → true；系統代入 → false
   *   （內容 100% 來自系統、使用者一個字都沒打，關 overlay 不該被「尚未儲存」攔下；
   *   且代入新增模式本來就不存草稿，`pickBeliever` 標髒的理由在此不成立）。
   * @returns 是否真的套用（false＝該列無 believerId，或期間被更新的改選取代）
   */
  private async applyBelieverRow(
    row: SignupListItem,
    { markDirty }: { markDirty: boolean },
  ): Promise<boolean> {
    // 這個 early return 必須留在 `++this.pickToken` **之前**：唯有它不動 pickToken，
    // `prefillFromSignup` 才能靠 pickToken 沒變來判斷「可以安全跑 fallback」。
    if (!row.believerId) return false;
    // 改選 guard（2026-07-27）：整段有多個 await（信眾主檔 / 區域清單 / 預繳歷史），使用者在回應到齊前
    // 再點別列時，舊的慢回應會把新選的資料蓋掉、或與新選的混在一起（地址區域下拉尤其明顯）。
    // 同 believerSearchToken 手法：每次改選換一個 token，非最新的一律放棄寫入。
    const token = ++this.pickToken;
    const isStale = (): boolean => token !== this.pickToken;
    this.errorMessage.set(null); // 上一次操作留下的錯誤訊息不該掛在新選的信眾上
    // 主檔僅作 fallback 與「已選信眾」摘要用；取不到不阻斷選取（欄位改吃該筆報名值）
    const master = await this.believerApi.getById(row.believerId).catch((err: unknown) => {
      if (!isStale()) this.errorMessage.set(toMessage(err));
      return null;
    });
    if (isStale()) return false;
    this.selectedBeliever.set(master ?? makeBelieverStubFromSignup(row));
    this.pickedRowId.set(row.id);
    // **預繳取該筆報名自身值（2026-07-31 客訴，取代原本查 `GET /prepay?believerId&year`）**：
    // 預繳是掛在單筆 Signups 上的快照，而法會與普桌（SignupType 4）是分開報名的兩件事——
    // 法會預繳不等於普桌預繳。原作法一律帶「該信眾今年以前最新一筆」且完全不分報名類型，
    // 同一信眾點法會列與普桌列都拿到同一份（＝最新那筆法會的），普桌明明沒預繳卻顯示有。
    // 改成與姓名/備註/名單/地址同一規則（點哪筆帶哪筆）後自然隔離：該列沒預繳就是空白。
    // **刻意偏離舊系統**：舊 BelieverSelected:1102-1115 的跨類型「最新一筆」查詢同樣有此問題。
    //
    // **費用刻意不清**（2026-07-27 使用者指定）：費用不會從結果列帶入，唯一來源就是使用者自己輸入，
    // 清掉等於把已打好的金額吃掉。舊 BelieverSelected 也完全沒碰 txtFee（只在送出時讀值 + 數字驗證），
    // 故不清才是對齊舊系統。
    //
    // **編號欄（keepNumber/customNumber）同理刻意不清**（2026-07-31 使用者指定）：勾了「指定編號」
    // 再改選信眾就被取消勾選、數字也被吃掉，是客訴來源。舊 BelieverSelected 同樣完全沒碰
    // cbKeepNumber/txtNumber——只有 PanelFormEmpty()（＝我們的 resetBelow，按「取消」時）才清
    // （NewSignupForm.cs:853-854），所以不清才是對齊舊系統。
    this.form.patchValue({
      believerId: row.believerId,
      name: row.name ?? master?.name ?? '',
      phone: row.phone ?? master?.phone ?? '',
      // per-signup 覆寫欄（2026-07-21 方案 A）：/signups 已 COALESCE 回退信眾值，故直接取該筆
      employeeType: row.employeeType ?? master?.employeeType ?? 1,
      isFixedNumber: row.isFixedNumber,
      hallName: row.hallName ?? master?.hallName ?? '',
      remark: row.remark ?? '',
      prepayYear: row.prepayYear,
      prepayCeremonyCategoryId: row.prepayCeremonyCategoryId ?? '',
    });
    // 該筆報名有自己的地址就用它（只存 city/area 字串，無 zipcodeId → 以區域名稱比對）；
    // 整段皆空才退回信眾主檔（同舊 `signup.Zipcodes != null` 判斷）
    if (row.mailCity || row.mailAddress) {
      await this.applyAddress('mail', row.mailCity, null, row.mailZone, row.mailAddress, isStale);
    } else {
      await this.applyAddress(
        'mail', master?.mailCity ?? null, master?.mailZipcodeId ?? null,
        master?.mailArea ?? null, master?.mailAddress ?? null, isStale,
      );
    }
    if (isStale()) return false;
    if (row.textCity || row.textAddress) {
      await this.applyAddress('text', row.textCity, null, row.textZone, row.textAddress, isStale);
    } else {
      await this.applyAddress(
        'text', master?.textCity ?? null, master?.textZipcodeId ?? null,
        master?.textArea ?? null, master?.textAddress ?? null, isStale,
      );
    }
    if (isStale()) return false;
    this.form.controls.sameMailAddress.setValue(false);
    this.livingArray.setValue(pad6(row.livingNames));
    this.deadArray.setValue(pad6(row.deadNames));
    // 選信眾＝使用者的實質輸入（patchValue 本身不會標髒）。沒標髒的話，「選了信眾但一個字都沒改就切走」
    // 會被草稿的 dirty 條件擋掉 → 回來又是空白，正是這次要修的客訴情境。
    if (markDirty) {
      this.form.markAsDirty();
      this.dirtyChange.emit(true);
    }
    return true;
  }

  /** 插入模式：帶入目標群組 + 插入位置編號，並鎖定年/法會/類型（避免改掉群組使插入位失義）。 */
  private applyInsertContext(ins: InsertAtContext): void {
    this.form.patchValue({
      year: ins.year,
      ceremonyCategoryId: ins.ceremonyCategoryId,
      signupType: ins.signupType,
      keepNumber: true,
      customNumber: ins.number,
    });
    this.form.controls.year.disable();
    this.form.controls.ceremonyCategoryId.disable();
    this.form.controls.signupType.disable();
    this.form.controls.keepNumber.disable();
  }

  /** 對外暴露：由 overlay / route page 觸發儲存 */
  async submit(): Promise<void> {
    if (this.saving()) return;
    // 不靜默返回：標出未完成欄位並顯示訊息（對齊舊系統驗證必有 MessageBox 提示）
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      this.errorMessage.set('必填欄位未完成，請檢查標紅的欄位');
      return;
    }
    const v = this.form.getRawValue();
    this.saving.set(true);
    this.errorMessage.set(null);

    // 未選信眾 → 先自動建立新信眾再報名（對齊舊 btnConfirm_Click:186-223 selectedcount==0 分支；
    // API 層故意不做 inline 建立，由前端 orchestration：POST /believers → POST /signups）。
    // 員工類型/固定編號/堂號改為可編輯後（2026-07-21），新信眾用表單值建立（保持新信眾與這筆報名一致）。
    let believerId = v.believerId || this.selectedBeliever()?.id || '';
    if (!believerId && this.mode() === 'create') {
      try {
        const created = await this.believerApi.create({
          employeeType: v.employeeType,
          name: v.name,
          hallName: v.hallName || null,
          mailAddress: v.mailAddress,
          phone: v.phone || null,
          isFixedNumber: v.isFixedNumber,
          mailZipcodeId: v.mailZipcodeId ? Number(v.mailZipcodeId) : null,
          textZipcodeId: v.textZipcodeId ? Number(v.textZipcodeId) : null,
          textAddress: v.textAddress || null,
          livingNames: (v.livingNames as string[]).map((s) => (s && s.trim() ? s : null)),
          deadNames: (v.deadNames as string[]).map((s) => (s && s.trim() ? s : null)),
        });
        believerId = created.id;
        // 綁回表單：報名若失敗重送不會重複建信眾
        this.form.controls.believerId.setValue(believerId);
        this.selectedBeliever.set(created);
      } catch (err) {
        this.errorMessage.set(toMessage(err));
        this.saving.set(false);
        return;
      }
    }

    const body: CreateSignupRequest = {
      year: v.year,
      ceremonyCategoryId: v.ceremonyCategoryId,
      signupType: v.signupType,
      believerId,
      name: v.name,
      mailAddress: v.mailAddress,
      keepNumber: v.keepNumber,
      // 編輯模式編號必送（後端 PUT 編號必填、對齊舊 EditSignupForm 編號恆可改）；
      // 新增模式僅在勾「指定編號」時送，否則由系統自動配號。
      customNumber: this.mode() === 'edit' || v.keepNumber ? v.customNumber : null,
      fee: v.fee,
      phone: v.phone || null,
      // per-signup 覆寫欄（2026-07-21）：改由表單值送出，後端寫 Signups 自有欄、不回寫 Believer。
      // 清空時送**空字串**而非 null（2026-07-31）：null 會被 SignupView 的 COALESCE 補回信眾堂號，
      // 使用者刪了堂號存檔後又長回來（＝刪不掉）。空字串代表「這筆明確沒有堂號」。
      hallName: v.hallName.trim(),
      employeeType: v.employeeType,
      isFixedNumber: v.isFixedNumber,
      mailZipcodeId: v.mailZipcodeId ? Number(v.mailZipcodeId) : null,
      textZipcodeId: v.textZipcodeId ? Number(v.textZipcodeId) : null,
      textAddress: v.textAddress || null,
      // 不 trim 開頭/結尾：保留使用者刻意輸入的排版空格（如開頭全形空格把名字往下推作直書排版）。
      // 僅「純空白/空字串」→ null（與後端 NormalizeNames 一致）。詳見 docs/gotchas.md「姓名中間空格」。
      livingNames: (v.livingNames as string[]).map((s) => (s && s.trim() ? s : null)),
      deadNames: (v.deadNames as string[]).map((s) => (s && s.trim() ? s : null)),
      remark: v.remark || null,
      prepayYear: v.prepayYear,
      prepayCeremonyCategoryId: v.prepayCeremonyCategoryId || null,
    };
    try {
      const editing = this.signupId();
      let created: SignupListItem | null = null;
      if (this.isInsert()) created = await this.api.insertShift(body);
      else if (editing) await this.api.update(editing, body);
      else created = await this.api.create(body);
      this.form.markAsPristine();
      // markAsPristine 不會走 valueChanges，host 的 dirty 旗標要主動同步——否則存完仍留在
      // 表單上的資料會讓代入新增/插入模式關閉時誤跳「未儲存的變更」。
      this.dirtyChange.emit(false);
      // 草稿不在此作廢（2026-07-28 反轉 07-27 的作法）：存完資料仍原樣留在畫面上（見下方
      // keepOpen），切走再回來也要看到同一份，故交給離開時的 saveDraft 一律快照。
      // 存完可接著印這一筆的資料卡（舊 btnPrintDataCard 於此刻 Enabled = true）
      if (created) this.lastCreatedSignupId.set(created.id);
      // 新增類（非編輯、非插入）＝存完不關閉、資料留著；插入/編輯維持關閉。
      const keepOpen = !editing && !this.isInsert();
      this.saved.emit({ keepOpen });
      // 成功提示（對齊舊 NewSignupForm.cs:355「編號X，新增報名成功」的 CustomMessageForm）。
      // 舊系統順序是先重查列表再跳訊息，故排在 saved.emit() 之後。
      if (created) {
        await this.confirmDialog.ask({
          title: '新增報名成功',
          message: `編號${created.number ?? ''}，新增報名成功`,
          confirmLabel: '確定',
          hideCancel: true,
          // 訊息 20px + 確定鈕加寬一倍（2026-07-29 使用者指定）：編號要一眼看得到、確定要好按
          emphasis: true,
        });
      }
    } catch (err) {
      this.errorMessage.set(toMessage(err));
    } finally {
      this.saving.set(false);
    }
  }

  /**
   * Enter 一律不送出表單（2026-07-28 使用者指定：「確認」只能用滑鼠點）。
   *
   * 兩道防線：(1) 表單內已移除 `<button type="submit">` 與 `(ngSubmit)`，HTML 隱含送出失效；
   * (2) 這裡再攔一次 Enter——按鈕列已投影進 `<form>` 內，焦點若停在「確認」上，
   * 原生 button 的 Enter 啟動（keydown 的預設動作＝click）也會被這裡的 preventDefault 擋掉。
   *
   * 例外：`textarea`（備註要能換行）。信眾搜尋框自己的 Enter＝觸發搜尋，
   * 它在 target 階段就已處理完並 preventDefault（見 `onBelieverSearchKeydown`），不受影響。
   */
  protected onFormKeydown(event: KeyboardEvent): void {
    if (event.key !== 'Enter') return;
    if ((event.target as HTMLElement | null)?.tagName === 'TEXTAREA') return;
    event.preventDefault();
  }

  /**
   * 列印剛新增那一筆的資料卡（對齊舊 `btnPrintDataCard_Click:371-404`，該處以 `CurrentSignupID`
   * 取 SignupView 後送印）。走 PrintService：Electron 跳列印對話框後直接送印（紙張 21×14.8cm、
   * 邊界 0、100% 由主行程指定），瀏覽器退回開新分頁預覽。
   */
  async printDataCard(): Promise<void> {
    const id = this.lastCreatedSignupId();
    if (!id || this.printingDataCard()) return;
    this.printingDataCard.set(true);
    this.errorMessage.set(null);
    try {
      await this.print.printSingle('datacard', id);
    } catch (err) {
      this.errorMessage.set(toMessage(err));
    } finally {
      this.printingDataCard.set(false);
    }
  }

  /**
   * 取消：清成新的一筆（2026-07-21 使用者指定）。
   * 不關閉 overlay、不跳頁，只把「信眾與其以下」全部欄位清空，保留最上方法會資料（年份/法會/類型）
   * 作為連續輸入下一筆的固定情境。清除已選信眾、搜尋框與搜尋結果，回到全新的新增狀態。
   * 僅供新增模式使用（編輯模式的取消＝關閉，由 host 處理）。
   *
   * **例外：費用不清**（2026-07-28 使用者指定）——同一場法會連續輸入時金額多半固定，
   * 每按一次取消就要重打很煩。與「改選信眾不清費用」同一取捨（舊 BelieverSelected 亦未碰 txtFee）。
   */
  resetBelow(): void {
    // 還在路上的「改選信眾」回應作廢，否則按完取消後它會把欄位再填回來
    this.pickToken++;
    // 「清成新的一筆」＝回到還沒存檔的狀態 → 列印資料卡重新 disabled（舊系統重進 Step2 亦 Enabled=false:95）
    this.lastCreatedSignupId.set(null);
    // 信眾選取 / 搜尋狀態
    this.selectedBeliever.set(null);
    this.pickedRowId.set(null);
    this.believerSearchTerm.set('');
    this.believerSearchResults.set([]);
    this.believerHasSearched.set(false);
    this.believerSearching.set(false);
    this.duplicates.set([]);
    this.errorMessage.set(null);
    // 地址連動下拉的暫存資料
    this.mailAreas.set([]);
    this.textAreas.set([]);
    this.mailZipcode.set('');
    this.textZipcode.set('');
    // 保留法會資料（year/ceremonyCategoryId/signupType）與費用（fee），清除信眾以下其餘欄位
    this.form.patchValue({
      believerId: '',
      name: '',
      phone: '',
      employeeType: 1,
      isFixedNumber: false,
      hallName: '',
      mailCity: '',
      mailZipcodeId: '',
      mailAddress: '',
      sameMailAddress: false,
      textCity: '',
      textZipcodeId: '',
      textAddress: '',
      keepNumber: false,
      customNumber: null,
      remark: '',
      prepayYear: null,
      prepayCeremonyCategoryId: '',
    });
    this.livingArray.setValue(Array.from({ length: 6 }, () => ''));
    this.deadArray.setValue(Array.from({ length: 6 }, () => ''));
    this.form.markAsPristine();
    // 草稿不在此作廢（2026-07-28 反轉 07-27 的作法）：取消後畫面上還留著法會資料與費用，
    // 切走再回來也要是這個狀態，故交給離開時的 saveDraft 一律快照。
  }
}

/** 信眾搜尋常駐列表「報名紀錄列」最多 render 的列數（超過靜默截斷，請使用者縮小條件）。 */
const MAX_BELIEVER_RESULT_ROWS = 200;

/** 「從未報名過的信眾」補列的獨立額度（接在報名列之後；獨立額度避免被大量報名列擠掉）。 */
const MAX_BELIEVER_ONLY_ROWS = 50;

function pad6(arr: (string | null)[]): string[] {
  const out = [...arr];
  while (out.length < 6) out.push(null);
  return out.slice(0, 6).map((v) => v ?? '');
}

/**
 * 把「從未報名過的信眾」包成一列搜尋結果（等效舊 BelieverView 中 SignupID 為 null 的列）。
 * 報名相關欄位留空（年份/法會/編號/費用/備註/預繳），清單會顯示成空白格；
 * `id` 加 `believer:` 前綴避免與報名列的 SignupID 撞號（僅供 track / 選定列高亮用）。
 */
function makeSignupRowFromBeliever(b: BelieverListItem): SignupListItem {
  return {
    id: `believer:${b.id}`,
    year: 0,
    ceremonyCategoryId: '',
    ceremonyTitle: null,
    signupType: 1,
    numberTitle: null,
    number: null,
    fee: null,
    employee: b.employeeTypeTitle,
    employeeType: b.employeeType,
    believerId: b.id,
    name: b.name,
    hallName: b.hallName,
    phone: b.phone,
    isFixedNumber: b.isFixedNumber,
    livingNames: b.livingNames,
    deadNames: b.deadNames,
    mailCity: b.mailCity,
    mailZone: b.mailArea,
    mailZipcode: null,
    mailAddress: b.mailAddress,
    textCity: b.textCity,
    textZone: b.textArea,
    textZipcode: null,
    textAddress: b.textAddress,
    prepayYear: null,
    prepayCeremonyCategoryId: null,
    prepayCeremonyTitle: null,
    remark: null,
    adminName: null,
    createDate: null,
  };
}

function makeBelieverStubFromSignup(item: SignupListItem): BelieverListItem {
  return {
    id: item.believerId ?? '',
    // per-signup 覆寫欄改帶報名自身值（2026-07-21）；表單本身已直接持有這三欄，stub 僅供信眾摘要卡顯示
    employeeType: item.employeeType ?? 1,
    employeeTypeTitle: item.employee ?? '',
    hallName: item.hallName,
    name: item.name ?? '',
    phone: item.phone,
    isFixedNumber: item.isFixedNumber,
    mailZipcodeId: null,
    mailCity: item.mailCity,
    mailArea: item.mailZone,
    mailAddress: item.mailAddress,
    textZipcodeId: null,
    textCity: item.textCity,
    textArea: item.textZone,
    textAddress: item.textAddress,
    livingNames: item.livingNames,
    deadNames: item.deadNames,
  };
}
