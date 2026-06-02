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
import { BelieverApi } from '../../core/api/believers/believer.api';
import type {
  BelieverListItem,
  BelieverUpsertRequest,
} from '../../core/api/believers/believer.models';
import { ZipcodeApi } from '../../core/api/zipcodes/zipcode.api';
import type { ZipcodeAreaItem } from '../../core/api/zipcodes/zipcode.models';
import { ApiError } from '../../core/http/api-error';

/**
 * 信眾 create/edit 表單。
 * - believer 有值 → 編輯模式；null → 新增模式
 * - 由外部容器呼叫 `submit()` 觸發儲存；成功 emit `saved`
 *
 * 地址區塊使用城市→區域連動下拉（對齊舊 WinForms 信眾表單）。
 * bp: docs/blueprints/api-endpoints/believer-upsert.md
 */
@Component({
  selector: 'app-believer-edit-form',
  imports: [ReactiveFormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './believer-edit-form.component.html',
  styleUrl: './believer-edit-form.component.scss',
})
export class BelieverEditFormComponent {
  private readonly api = inject(BelieverApi);
  private readonly zipcodeApi = inject(ZipcodeApi);
  private readonly fb = inject(FormBuilder);
  private readonly destroyRef = inject(DestroyRef);

  readonly believer = input<BelieverListItem | null>(null);
  readonly saved = output<void>();
  readonly dirtyChange = output<boolean>();

  protected readonly saving = signal(false);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly mode = computed<'create' | 'edit'>(() =>
    this.believer() ? 'edit' : 'create',
  );

  // 城市 / 區域連動下拉資料
  protected readonly cities = signal<string[]>([]);
  protected readonly mailAreas = signal<ZipcodeAreaItem[]>([]);
  protected readonly textAreas = signal<ZipcodeAreaItem[]>([]);
  protected readonly mailZipcode = signal('');
  protected readonly textZipcode = signal('');

  protected readonly form = this.fb.nonNullable.group({
    employeeType: [1, [Validators.required, Validators.min(1), Validators.max(3)]],
    name: ['', [Validators.required, Validators.maxLength(50)]],
    hallName: [''],
    phone: [''],
    isFixedNumber: [false],
    // 地址（城市/區域連動；zipcodeId 以字串持有，submit 轉 number）
    mailCity: [''],
    mailZipcodeId: [''],
    mailAddress: ['', [Validators.required, Validators.maxLength(200)]],
    sameMailAddress: [false],
    textCity: [''],
    textZipcodeId: [''],
    textAddress: [''],
    livingNames: this.fb.array(Array.from({ length: 6 }, () => this.fb.control(''))),
    deadNames: this.fb.array(Array.from({ length: 6 }, () => this.fb.control(''))),
  });

  protected get livingArray(): FormArray { return this.form.controls.livingNames; }
  protected get deadArray(): FormArray { return this.form.controls.deadNames; }

  get isDirty(): boolean { return this.form.dirty; }

  constructor() {
    void this.loadCities();
    effect(() => {
      const b = this.believer();
      if (b) void this.applyItem(b);
      else void this.reset();
    });
    this.form.valueChanges
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.dirtyChange.emit(this.form.dirty));
  }

  private async loadCities(): Promise<void> {
    try {
      const resp = await this.zipcodeApi.cities();
      this.cities.set(resp.items);
    } catch (err) {
      this.errorMessage.set(toMessage(err));
    }
  }

  private async applyItem(item: BelieverListItem): Promise<void> {
    // 先 reset 非地址欄位
    this.form.patchValue({
      employeeType: item.employeeType,
      name: item.name,
      hallName: item.hallName ?? '',
      phone: item.phone ?? '',
      isFixedNumber: item.isFixedNumber,
      sameMailAddress: false,
    });
    this.livingArray.setValue(pad6(item.livingNames));
    this.deadArray.setValue(pad6(item.deadNames));

    // 再 await 地址連動（需非同步載入區域清單）
    await this.applyAddress('mail', item.mailCity, item.mailZipcodeId, item.mailArea, item.mailAddress);
    await this.applyAddress('text', item.textCity, item.textZipcodeId, item.textArea, item.textAddress);

    this.form.markAsPristine();
  }

  private async reset(): Promise<void> {
    this.form.patchValue({
      employeeType: 1,
      name: '',
      hallName: '',
      phone: '',
      isFixedNumber: false,
      mailCity: '',
      mailZipcodeId: '',
      mailAddress: '',
      sameMailAddress: false,
      textCity: '',
      textZipcodeId: '',
      textAddress: '',
    });
    this.livingArray.setValue(['', '', '', '', '', '']);
    this.deadArray.setValue(['', '', '', '', '', '']);
    this.mailAreas.set([]);
    this.textAreas.set([]);
    this.mailZipcode.set('');
    this.textZipcode.set('');
    this.form.markAsPristine();
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
   * zipcodeId 優先；無 zipcodeId 時退而以區域名稱比對。
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

  async submit(): Promise<void> {
    if (this.form.invalid || this.saving()) return;
    const v = this.form.getRawValue();
    const body: BelieverUpsertRequest = {
      employeeType: v.employeeType,
      name: v.name,
      mailAddress: v.mailAddress,
      hallName: v.hallName || null,
      phone: v.phone || null,
      isFixedNumber: v.isFixedNumber,
      // 字串 zipcodeId 轉回 number|null，保持 BelieverUpsertRequest 契約不變
      mailZipcodeId: v.mailZipcodeId ? Number(v.mailZipcodeId) : null,
      textZipcodeId: v.textZipcodeId ? Number(v.textZipcodeId) : null,
      textAddress: v.textAddress || null,
      // 不 trim 開頭/結尾：保留刻意排版空格（與後端 NormalizeNames 一致）。詳見 docs/gotchas.md「姓名中間空格」。
      livingNames: (v.livingNames as string[]).map((s) => (s && s.trim() ? s : null)),
      deadNames: (v.deadNames as string[]).map((s) => (s && s.trim() ? s : null)),
    };
    this.saving.set(true);
    this.errorMessage.set(null);
    try {
      const existing = this.believer();
      if (existing) await this.api.update(existing.id, body);
      else await this.api.create(body);
      this.form.markAsPristine();
      this.saved.emit();
    } catch (err) {
      this.errorMessage.set(toMessage(err));
    } finally {
      this.saving.set(false);
    }
  }

  protected onSubmitFromForm(): void { void this.submit(); }
}

function pad6(arr: (string | null)[]): string[] {
  const out = [...arr];
  while (out.length < 6) out.push(null);
  return out.slice(0, 6).map((v) => v ?? '');
}

function toMessage(err: unknown): string {
  return err instanceof ApiError ? err.message : '操作失敗，請稍後再試';
}
