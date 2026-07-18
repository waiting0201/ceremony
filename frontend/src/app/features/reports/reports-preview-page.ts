import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  OnDestroy,
  OnInit,
  signal,
} from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { ReportApi } from '../../core/api/reports/report.api';
import type { SingleReportType } from '../../core/api/reports/report.models';
import { CategoryApi } from '../../core/api/categories/category.api';
import type { CategoryNode } from '../../core/api/categories/category.models';
import { ApiError } from '../../core/http/api-error';
import { flattenCategories, type FlatCategory } from '../../shared/util/categories';
import { SIGNUP_TYPES } from '../../shared/util/signup-type';
import { currentTaiwanYear } from '../../shared/util/taiwan-year';
import { NumericInputDirective } from '../../shared/directives/numeric-input.directive';

type Mode = 'single' | 'batch';

interface ReportTypeOption {
  value: SingleReportType;
  label: string;
}

const REPORT_TYPES: readonly ReportTypeOption[] = [
  { value: 'datacard', label: '報名資料卡' },
  { value: 'receipt', label: '收據' },
  { value: 'tablet', label: '薦牌' },
  { value: 'text', label: '文牒' },
  { value: 'worship', label: '普桌（限類型 4）' },
  { value: 'worshipcard', label: '普桌資料卡（限類型 4）' },
];

@Component({
  selector: 'app-reports-preview-page',
  imports: [ReactiveFormsModule, RouterLink, NumericInputDirective],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './reports-preview-page.html',
  styleUrl: './reports-preview-page.scss',
})
export class ReportsPreviewPage implements OnInit, OnDestroy {
  private readonly api = inject(ReportApi);
  private readonly categoryApi = inject(CategoryApi);
  private readonly fb = inject(FormBuilder);
  private readonly route = inject(ActivatedRoute);
  private readonly sanitizer = inject(DomSanitizer);

  protected readonly reportTypes = REPORT_TYPES;
  protected readonly signupTypes = SIGNUP_TYPES;
  protected readonly categories = signal<CategoryNode[]>([]);
  protected readonly flatCategories = computed<FlatCategory[]>(() =>
    flattenCategories(this.categories()),
  );

  protected readonly mode = signal<Mode>('single');
  protected readonly previewUrl = signal<SafeResourceUrl | null>(null);
  protected readonly fileName = signal<string | null>(null);
  protected readonly signupCount = signal<number | null>(null);
  protected readonly loading = signal(false);
  protected readonly errorMessage = signal<string | null>(null);
  private currentObjectUrl: string | null = null;

  protected readonly initialType = computed<SingleReportType>(() => {
    const t = this.route.snapshot.paramMap.get('type');
    const known = REPORT_TYPES.find((r) => r.value === t);
    return known?.value ?? 'datacard';
  });

  protected readonly singleForm = this.fb.nonNullable.group({
    type: ['datacard' as SingleReportType, [Validators.required]],
    signupId: ['', [Validators.required]],
  });

  protected readonly batchForm = this.fb.nonNullable.group({
    reportType: ['datacard' as SingleReportType, [Validators.required]],
    year: [currentTaiwanYear() as number | null],
    yearGte: [false],
    ceremonyCategoryId: [''],
    signupType: [null as number | null],
    numberStart: [1, [Validators.required, Validators.min(0)]],
    numberEnd: [50, [Validators.required, Validators.min(0)]],
  });

  ngOnInit(): void {
    this.singleForm.patchValue({ type: this.initialType() });
    this.batchForm.patchValue({ reportType: this.initialType() });
    void this.loadCategories();
  }

  ngOnDestroy(): void {
    this.releaseUrl();
  }

  protected setMode(m: Mode): void {
    this.mode.set(m);
  }

  private async loadCategories(): Promise<void> {
    try {
      const resp = await this.categoryApi.list();
      this.categories.set(resp.items);
    } catch (err) {
      this.errorMessage.set(toMessage(err));
    }
  }

  protected async generateSingle(): Promise<void> {
    if (this.singleForm.invalid || this.loading()) return;
    const { type, signupId } = this.singleForm.getRawValue();
    this.loading.set(true);
    this.errorMessage.set(null);
    this.signupCount.set(null);
    try {
      const { blob, fileName } = await this.api.single(type, signupId.trim());
      this.displayBlob(blob, fileName);
    } catch (err) {
      this.errorMessage.set(toMessage(err));
    } finally {
      this.loading.set(false);
    }
  }

  protected async generateBatch(): Promise<void> {
    if (this.batchForm.invalid || this.loading()) return;
    const v = this.batchForm.getRawValue();
    this.loading.set(true);
    this.errorMessage.set(null);
    this.signupCount.set(null);
    try {
      const { blob, fileName, signupCount } = await this.api.batch({
        reportType: v.reportType,
        numberStart: v.numberStart,
        numberEnd: v.numberEnd,
        year: v.year ?? null,
        yearGte: v.yearGte,
        ceremonyCategoryId: v.ceremonyCategoryId || null,
        signupType: v.signupType ?? null,
      });
      this.displayBlob(blob, fileName);
      this.signupCount.set(signupCount ?? null);
    } catch (err) {
      this.errorMessage.set(toMessage(err));
    } finally {
      this.loading.set(false);
    }
  }

  private displayBlob(blob: Blob, fileName: string): void {
    this.releaseUrl();
    const url = URL.createObjectURL(blob);
    this.currentObjectUrl = url;
    this.previewUrl.set(this.sanitizer.bypassSecurityTrustResourceUrl(url));
    this.fileName.set(fileName);
  }

  private releaseUrl(): void {
    if (this.currentObjectUrl) {
      URL.revokeObjectURL(this.currentObjectUrl);
      this.currentObjectUrl = null;
    }
  }

  protected closePreview(): void {
    this.releaseUrl();
    this.previewUrl.set(null);
    this.fileName.set(null);
    this.signupCount.set(null);
  }

  protected download(): void {
    if (!this.currentObjectUrl || !this.fileName()) return;
    const a = document.createElement('a');
    a.href = this.currentObjectUrl;
    a.download = this.fileName()!;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
  }

  protected openExternal(): void {
    if (!this.currentObjectUrl) return;
    window.open(this.currentObjectUrl, '_blank');
  }
}

function toMessage(err: unknown): string {
  return err instanceof ApiError ? err.message : '操作失敗，請稍後再試';
}
