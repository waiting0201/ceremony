import {
  ChangeDetectionStrategy,
  Component,
  computed,
  DestroyRef,
  effect,
  inject,
  input,
  output,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormArray, FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { SignupApi } from '../../core/api/signups/signup.api';
import type {
  CreateSignupRequest,
  SignupListItem,
} from '../../core/api/signups/signup.models';
import { CategoryApi } from '../../core/api/categories/category.api';
import type { CategoryNode } from '../../core/api/categories/category.models';
import { BelieverApi } from '../../core/api/believers/believer.api';
import type { BelieverListItem } from '../../core/api/believers/believer.models';
import { ZipcodeApi } from '../../core/api/zipcodes/zipcode.api';
import type { ZipcodeAreaItem } from '../../core/api/zipcodes/zipcode.models';
import { PrepayApi } from '../../core/api/prepay/prepay.api';
import { ApiError } from '../../core/http/api-error';
import { SIGNUP_TYPES } from '../../shared/util/signup-type';
import { flattenCategories, type FlatCategory } from '../../shared/util/categories';
import { currentTaiwanYear } from '../../shared/util/taiwan-year';
import { currentSeason, resolveSeasonRootId } from '../../shared/util/ceremony-season';

/**
 * 報名 create/edit 表單（不含 page layout / overlay shell）。
 *
 * 表單編排對齊舊 NewSignupForm.cs（單頁呈現，非兩步驟；mockup v4 決議單頁）：
 * 法會資料 → 信眾 → 基本資料 → 地址（城市/區域連動下拉 + 同寄件地址）→ 陽上/往生名單 → 編號/費用 → 備註/預繳。
 *
 * - signupId 有值 → 編輯模式
 * - fromSignupId 有值 → 代入新增模式（不帶 year/ceremony/type）
 * - 兩者都 null → 純新增模式
 *
 * 由外部容器（route page / overlay）呼叫 `submit()` 觸發儲存；成功 emit `saved`。
 */
@Component({
  selector: 'app-signup-edit-form',
  imports: [ReactiveFormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './signup-edit-form.component.html',
  styleUrl: './signup-edit-form.component.scss',
})
export class SignupEditFormComponent {
  private readonly api = inject(SignupApi);
  private readonly categoryApi = inject(CategoryApi);
  private readonly believerApi = inject(BelieverApi);
  private readonly zipcodeApi = inject(ZipcodeApi);
  private readonly prepayApi = inject(PrepayApi);
  private readonly fb = inject(FormBuilder);

  private readonly destroyRef = inject(DestroyRef);

  readonly signupId = input<string | null>(null);
  readonly fromSignupId = input<string | null>(null);
  readonly saved = output<void>();
  readonly cancelled = output<void>();
  readonly dirtyChange = output<boolean>();

  protected readonly signupTypes = SIGNUP_TYPES;
  protected readonly categories = signal<CategoryNode[]>([]);
  protected readonly flatCategories = computed<FlatCategory[]>(() =>
    flattenCategories(this.categories()),
  );

  protected readonly loading = signal(false);
  protected readonly saving = signal(false);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly selectedBeliever = signal<BelieverListItem | null>(null);

  /** 選定信眾的員工類型（唯讀顯示，對齊舊 dlEmployeeType；新版不於報名建立時改信眾屬性）。 */
  protected readonly employeeTypeTitle = computed<string>(
    () => this.selectedBeliever()?.employeeTypeTitle ?? '',
  );
  // 固定編號為信眾屬性，唯讀顯示（對齊舊 cbIsFixedNumber 帶出；新版不於報名改信眾屬性）
  protected readonly isFixedNumber = computed<boolean>(
    () => this.selectedBeliever()?.isFixedNumber ?? false,
  );
  // 堂號為信眾層級屬性，唯讀顯示（僅於信眾維護頁修改）。報名編輯不回寫 Believer，避免連動同信眾全部報名。
  // 見 docs/blueprints/signup-hallname-isolation.md
  protected readonly hallNameDisplay = computed<string>(
    () => this.selectedBeliever()?.hallName ?? '',
  );

  // 城市 / 區域連動下拉資料
  protected readonly cities = signal<string[]>([]);
  protected readonly mailAreas = signal<ZipcodeAreaItem[]>([]);
  protected readonly textAreas = signal<ZipcodeAreaItem[]>([]);
  protected readonly mailZipcode = signal('');
  protected readonly textZipcode = signal('');

  protected readonly believerSearchTerm = signal('');
  protected readonly believerSearchResults = signal<BelieverListItem[]>([]);
  protected readonly believerSearching = signal(false);
  protected readonly believerPickerOpen = signal(false);

  protected readonly mode = computed<'create' | 'edit'>(() =>
    this.signupId() ? 'edit' : 'create',
  );

  protected readonly form = this.fb.nonNullable.group({
    // 法會資料（舊 Step1）
    year: [currentTaiwanYear(), [Validators.required, Validators.min(1)]],
    ceremonyCategoryId: ['', [Validators.required]],
    signupType: [1, [Validators.required]],
    // 信眾
    believerId: ['', [Validators.required]],
    // 基本資料（堂號為信眾屬性，不在報名表單持有；唯讀顯示自 selectedBeliever）
    name: ['', [Validators.required, Validators.maxLength(50)]],
    phone: [''],
    // 地址（城市/區域連動；zipcodeId 以字串持有，submit 轉 number）
    mailCity: [''],
    mailZipcodeId: [''],
    mailAddress: ['', [Validators.required, Validators.maxLength(200)]],
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
    this.form.valueChanges
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.dirtyChange.emit(this.form.dirty));
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

  private async prefillFromSignup(signupId: string): Promise<void> {
    this.loading.set(true);
    try {
      const item = await this.api.getById(signupId);
      this.form.patchValue({
        believerId: item.believerId ?? '',
        name: item.name ?? '',
        phone: item.phone ?? '',
        remark: item.remark ?? '',
      });
      await this.applyAddress('mail', item.mailCity, null, item.mailZone, item.mailAddress);
      await this.applyAddress('text', item.textCity, null, item.textZone, item.textAddress);
      this.livingArray.setValue(pad6(item.livingNames));
      this.deadArray.setValue(pad6(item.deadNames));
      this.selectedBeliever.set(makeBelieverStubFromSignup(item));
      this.form.markAsPristine();
    } catch (err) {
      this.errorMessage.set(toMessage(err));
    } finally {
      this.loading.set(false);
    }
  }

  private async applyItem(item: SignupListItem): Promise<void> {
    this.form.patchValue({
      year: item.year,
      ceremonyCategoryId: item.ceremonyCategoryId,
      signupType: item.signupType,
      believerId: item.believerId ?? '',
      name: item.name ?? '',
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

  /** 同寄件地址 checkbox（對齊舊 cbSameMailAddress_CheckedChanged）。 */
  protected async onSameMailAddressChange(): Promise<void> {
    const checked = this.form.controls.sameMailAddress.value;
    if (checked) {
      const mailAddr = this.form.controls.mailAddress.value.trim();
      if (!mailAddr) {
        this.form.controls.sameMailAddress.setValue(false);
        this.errorMessage.set('請先輸入寄件地址');
        return;
      }
      this.errorMessage.set(null);
      const mailCity = this.form.controls.mailCity.value;
      const mailZipId = this.form.controls.mailZipcodeId.value;
      const mailZipNum = mailZipId ? Number(mailZipId) : null;
      await this.applyAddress('text', mailCity, mailZipNum, null, mailAddr);
    } else {
      await this.applyAddress('text', '', null, null, '');
    }
  }

  // ── 信眾搜尋 picker ───────────────────────────────────────────────

  protected openBelieverPicker(): void {
    this.believerPickerOpen.set(true);
    this.believerSearchTerm.set('');
    this.believerSearchResults.set([]);
  }

  protected closeBelieverPicker(): void {
    this.believerPickerOpen.set(false);
  }

  protected async searchBelievers(term: string): Promise<void> {
    const trimmed = term.trim();
    this.believerSearchTerm.set(term);
    if (!trimmed) {
      this.believerSearchResults.set([]);
      return;
    }
    this.believerSearching.set(true);
    try {
      const resp = await this.believerApi.search({ name: trimmed });
      this.believerSearchResults.set(resp.items);
    } catch (err) {
      this.errorMessage.set(toMessage(err));
    } finally {
      this.believerSearching.set(false);
    }
  }

  protected async pickBeliever(b: BelieverListItem): Promise<void> {
    this.selectedBeliever.set(b);
    this.form.patchValue({
      believerId: b.id,
      name: b.name,
      phone: b.phone ?? '',
    });
    await this.applyAddress('mail', b.mailCity, b.mailZipcodeId, b.mailArea, b.mailAddress);
    await this.applyAddress('text', b.textCity, b.textZipcodeId, b.textArea, b.textAddress);
    this.form.controls.sameMailAddress.setValue(false);
    this.livingArray.setValue(pad6(b.livingNames));
    this.deadArray.setValue(pad6(b.deadNames));
    await this.prefillPrepayHistory(b.id);
    this.closeBelieverPicker();
  }

  /**
   * 帶入該信眾今年(含)以前最新一筆報名的預繳資訊（對齊舊 NewSignupForm.BelieverSelected:1102-1115）。
   * 僅在最新報名有 PrepayYear 時才預填；查無或失敗則不動使用者既填值。
   */
  private async prefillPrepayHistory(believerId: string): Promise<void> {
    if (!believerId) return;
    try {
      const latest = await this.prepayApi.believerLatest(believerId, this.form.controls.year.value);
      if (latest.prepayYear != null) {
        this.form.patchValue({
          prepayYear: latest.prepayYear,
          prepayCeremonyCategoryId: latest.prepayCeremonyCategoryId ?? '',
        });
      }
    } catch {
      // 便利功能，失敗不阻斷選信眾流程
    }
  }

  /** 對外暴露：由 overlay / route page 觸發儲存 */
  async submit(): Promise<void> {
    if (this.form.invalid || this.saving()) return;
    const v = this.form.getRawValue();
    const body: CreateSignupRequest = {
      year: v.year,
      ceremonyCategoryId: v.ceremonyCategoryId,
      signupType: v.signupType,
      believerId: v.believerId || this.selectedBeliever()?.id || '',
      name: v.name,
      mailAddress: v.mailAddress,
      keepNumber: v.keepNumber,
      customNumber: v.keepNumber ? v.customNumber : null,
      fee: v.fee,
      phone: v.phone || null,
      // 堂號為信眾屬性，取自選定信眾（後端僅用於 SignupLog 快照，不回寫 Believer）
      hallName: this.selectedBeliever()?.hallName || null,
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
    this.saving.set(true);
    this.errorMessage.set(null);
    try {
      const editing = this.signupId();
      if (editing) await this.api.update(editing, body);
      else await this.api.create(body);
      this.form.markAsPristine();
      this.saved.emit();
    } catch (err) {
      this.errorMessage.set(toMessage(err));
    } finally {
      this.saving.set(false);
    }
  }

  protected onSubmitFromForm(): void {
    void this.submit();
  }
}

function pad6(arr: (string | null)[]): string[] {
  const out = [...arr];
  while (out.length < 6) out.push(null);
  return out.slice(0, 6).map((v) => v ?? '');
}

function makeBelieverStubFromSignup(item: SignupListItem): BelieverListItem {
  return {
    id: item.believerId ?? '',
    employeeType: 1,
    employeeTypeTitle: '',
    hallName: item.hallName,
    name: item.name ?? '',
    phone: item.phone,
    isFixedNumber: false,
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

function toMessage(err: unknown): string {
  return err instanceof ApiError ? err.message : '操作失敗，請稍後再試';
}
