import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  OnInit,
  signal,
} from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { PrepayApi } from '../../core/api/prepay/prepay.api';
import type { PrepayLoadResponse } from '../../core/api/prepay/prepay.models';
import { CategoryApi } from '../../core/api/categories/category.api';
import type { CategoryNode } from '../../core/api/categories/category.models';
import { ApiError } from '../../core/http/api-error';
import { PREPAY_GROUPS } from '../../shared/util/prepay-groups';
import { currentTaiwanYear } from '../../shared/util/taiwan-year';

@Component({
  selector: 'app-prepay-page',
  imports: [ReactiveFormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './prepay-page.html',
  styleUrl: './prepay-page.scss',
})
export class PrepayPage implements OnInit {
  private readonly api = inject(PrepayApi);
  private readonly categoryApi = inject(CategoryApi);
  private readonly fb = inject(FormBuilder);

  protected readonly prepayGroups = PREPAY_GROUPS;
  protected readonly categories = signal<CategoryNode[]>([]);
  // 對齊舊 LoadPrepayForm.LoadSelectCeremony / LoadCeremony：只列根法會（ParentID==null）依 Sort，不含子法會。
  protected readonly rootCategories = computed(() =>
    [...this.categories()].sort((a, b) => a.sort - b.sort),
  );

  // 對齊舊 LoadSelectYear（來源：本年起往前 5 年）與 LoadYear（目標：本年 + 明年）。
  protected readonly sourceYears = Array.from({ length: 5 }, (_, i) => currentTaiwanYear() - i);
  protected readonly targetYears = [currentTaiwanYear(), currentTaiwanYear() + 1];

  protected readonly result = signal<PrepayLoadResponse | null>(null);
  protected readonly loading = signal(false);
  protected readonly errorMessage = signal<string | null>(null);

  protected readonly form = this.fb.nonNullable.group({
    sourceYear: [currentTaiwanYear() - 1, [Validators.required, Validators.min(1)]],
    sourceCeremonyId: ['', [Validators.required]],
    targetYear: [currentTaiwanYear(), [Validators.required, Validators.min(1)]],
    targetCeremonyId: ['', [Validators.required]],
    believerGroup: [1, [Validators.required]],
  });

  ngOnInit(): void {
    void this.loadCategories();
  }

  private async loadCategories(): Promise<void> {
    try {
      const resp = await this.categoryApi.list();
      this.categories.set(resp.items);
    } catch (err) {
      this.errorMessage.set(toMessage(err));
    }
  }

  protected async load(): Promise<void> {
    if (this.form.invalid || this.loading()) return;

    // 對齊舊 LoadPrepayForm.btnConfirm_Click：載入前二次確認。
    const v = this.form.getRawValue();
    const groupLabel = PREPAY_GROUPS.find((g) => g.code === Number(v.believerGroup))?.label ?? '';
    const ceremonyTitle =
      this.rootCategories().find((c) => c.id === v.sourceCeremonyId)?.title ?? '';
    if (!confirm(`是否載入${groupLabel}${v.sourceYear}年${ceremonyTitle}法會預繳資料？`)) return;

    this.loading.set(true);
    this.errorMessage.set(null);
    this.result.set(null);
    try {
      const resp = await this.api.load(this.form.getRawValue());
      this.result.set(resp);
    } catch (err) {
      this.errorMessage.set(toMessage(err));
    } finally {
      this.loading.set(false);
    }
  }
}

function toMessage(err: unknown): string {
  return err instanceof ApiError ? err.message : '操作失敗，請稍後再試';
}
