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
import type { SingleReportType } from '../../core/api/reports/report.models';
import { BatchPrintService } from '../../core/reports/batch-print.service';
import { PrintService } from '../../core/print/print.service';
import { CategoryApi } from '../../core/api/categories/category.api';
import type { CategoryNode } from '../../core/api/categories/category.models';
import { toMessage } from '../../core/errors/to-message';
import { flattenCategories, type FlatCategory } from '../../shared/util/categories';
import { SIGNUP_TYPES } from '../../shared/util/signup-type';
import { currentTaiwanYear } from '../../shared/util/taiwan-year';
import { NumericInputDirective } from '../../shared/directives/numeric-input.directive';
import { isElectron } from '../../core/platform/electron';
import type { PrintFormState } from '../../core/platform/electron';

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
  private readonly batchPrint = inject(BatchPrintService);
  private readonly print = inject(PrintService);
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

  protected readonly previewUrl = signal<SafeResourceUrl | null>(null);
  protected readonly fileName = signal<string | null>(null);
  protected readonly signupCount = signal<number | null>(null);
  protected readonly loading = signal(false);
  protected readonly errorMessage = signal<string | null>(null);
  private currentObjectUrl: string | null = null;
  private currentBlob: Blob | null = null;
  protected readonly currentType = signal<SingleReportType | null>(null);
  protected readonly printing = signal(false);
  /** 診斷紀錄按鈕只在桌面版有意義（紀錄寫在 %APPDATA%/Ceremony/logs）。 */
  protected readonly isDesktop = isElectron();
  /** 自動選紙狀態；null = 非桌面版或讀不到，該格就不顯示。 */
  protected readonly printForm = signal<PrintFormState | null>(null);
  protected readonly printFormBusy = signal(false);

  protected readonly initialType = computed<SingleReportType>(() => {
    const t = this.route.snapshot.paramMap.get('type');
    const known = REPORT_TYPES.find((r) => r.value === t);
    return known?.value ?? 'datacard';
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
    this.batchForm.patchValue({ reportType: this.initialType() });
    void this.loadCategories();
    void this.print.printFormState().then((s) => this.printForm.set(s));
  }

  ngOnDestroy(): void {
    this.releaseUrl();
  }

  private async loadCategories(): Promise<void> {
    try {
      const resp = await this.categoryApi.list();
      this.categories.set(resp.items);
    } catch (err) {
      this.errorMessage.set(toMessage(err));
    }
  }

  protected async generateBatch(): Promise<void> {
    if (this.batchForm.invalid || this.loading()) return;
    const v = this.batchForm.getRawValue();
    this.loading.set(true);
    this.errorMessage.set(null);
    this.signupCount.set(null);
    try {
      // 走 job 版：進度 overlay 由 BatchPrintService 負責；回 null 代表使用者取消
      const resp = await this.batchPrint.run(
        {
          reportType: v.reportType,
          numberStart: v.numberStart,
          numberEnd: v.numberEnd,
          year: v.year ?? null,
          yearGte: v.yearGte,
          ceremonyCategoryId: v.ceremonyCategoryId || null,
          signupType: v.signupType ?? null,
        },
        { detail: REPORT_TYPES.find((r) => r.value === v.reportType)?.label },
      );
      if (!resp) return;
      this.displayBlob(resp.blob, resp.fileName, v.reportType);
      this.signupCount.set(resp.signupCount ?? null);
    } catch (err) {
      this.errorMessage.set(toMessage(err));
    } finally {
      this.loading.set(false);
    }
  }

  private displayBlob(blob: Blob, fileName: string, type: SingleReportType): void {
    this.releaseUrl();
    const url = URL.createObjectURL(blob);
    this.currentObjectUrl = url;
    // 保留 blob 供「列印」用：此頁的 batch job 已被取檔消耗，不能再叫 main 去取一次
    this.currentBlob = blob;
    this.currentType.set(type);
    this.previewUrl.set(this.sanitizer.bypassSecurityTrustResourceUrl(url));
    this.fileName.set(fileName);
  }

  private releaseUrl(): void {
    if (this.currentObjectUrl) {
      URL.revokeObjectURL(this.currentObjectUrl);
      this.currentObjectUrl = null;
    }
    this.currentBlob = null;
  }

  protected closePreview(): void {
    this.releaseUrl();
    this.currentType.set(null);
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

  /** 印歪 / 印不出來時的第一手證據：在檔案總管中選取今天的列印紀錄。 */
  protected async openPrintLog(): Promise<void> {
    await this.print.openPrintLog();
  }

  /**
   * 排障：叫出 Windows 的「列印喜好設定」，讓使用者改一次紙覆寫掉壞掉的驅動設定。
   * 失敗訊息走既有的 `errorMessage` 區塊——這顆按鈕沒反應會被當成程式壞掉。
   */
  protected async openPrinterPreferences(): Promise<void> {
    try {
      await this.print.openPrinterPreferences();
    } catch (err) {
      this.errorMessage.set(toMessage(err));
    }
  }

  /**
   * 自動選紙開關（決策 9d）。
   *
   * 只有桌面版有；狀態讀不到就整格不顯示（`printForm()` 維持 null）。
   * 關掉之後程式完全不去碰驅動設定，代價只是每次列印要自己在對話框選紙——
   * 這是現場遇到「按了列印鈕整個卡死」時**不必等我們出新版**的止血鍵。
   */
  protected async togglePrintForm(): Promise<void> {
    const cur = this.printForm();
    if (!cur || this.printFormBusy()) return;
    this.printFormBusy.set(true);
    try {
      this.printForm.set(await this.print.setPrintFormEnabled(!cur.enabled));
    } finally {
      this.printFormBusy.set(false);
    }
  }

  /** 把目前預覽中的 PDF 開在列印預覽視窗，使用者按工具列列印鈕走 Windows 原生對話框。 */
  protected async printPreview(): Promise<void> {
    const blob = this.currentBlob;
    const type = this.currentType();
    if (!blob || !type || this.printing()) return;
    this.printing.set(true);
    this.errorMessage.set(null);
    try {
      await this.print.printBlob(type, blob);
    } catch (err) {
      this.errorMessage.set(toMessage(err));
    } finally {
      this.printing.set(false);
    }
  }
}
