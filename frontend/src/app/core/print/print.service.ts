import { inject, Injectable } from '@angular/core';
import { AuthStore } from '../auth/auth.store';
import { ceremony, isElectron } from '../platform/electron';
import type { ReportPrintSetting } from '../platform/electron';
import { BatchPrintService } from '../reports/batch-print.service';
import type { BatchPrintOptions } from '../reports/batch-print.service';
import { ChunkedPrintService, SEGMENT_SIZE } from '../reports/chunked-print.service';
import { BatchPrintPanelService } from '../../shared/batch-print-panel/batch-print-panel.service';
import { ReportApi } from '../api/reports/report.api';
import type {
  BatchReportPlan,
  BatchReportRequest,
  SingleReportType,
} from '../api/reports/report.models';
import { openPdfInNewTab } from '../../shared/util/pdf';
import { PrintDialogService } from '../../shared/print-dialog/print-dialog.service';
import { UserFacingError } from '../errors/to-message';

/** 報表中文名與紙張尺寸（顯示用）。權威值在後端 ReportPageSizes，這裡只為了對話框有字可讀。 */
const REPORT_META: Record<SingleReportType, { label: string; paper: string }> = {
  datacard: { label: '資料卡', paper: '21 × 14.8 cm' },
  receipt: { label: '收據', paper: '21 × 29.7 cm' },
  tablet: { label: '薦牌', paper: '11.5 × 25.5 cm' },
  text: { label: '文牒', paper: '36.5 × 26.2 cm' },
  worship: { label: '普桌', paper: '21 × 29.6 cm' },
  worshipcard: { label: '普桌資料卡', paper: '21 × 14.8 cm' },
};

/**
 * 批次超過這個筆數就略過預覽：預覽 = renderer 先取檔，PDF 還要經 IPC structured clone
 * 再複製一份到 main，數百 MB 的批次會讓兩邊各佔一份 → OOM。
 * 200 筆的資料卡約 20 MB，是可接受的上限。
 */
const PREVIEW_MAX_SIGNUPS = 200;
/** 第二道保險：筆數不多但單筆很肥時只放棄 iframe 渲染，bytes 已在手上仍可正常送印。 */
const PREVIEW_MAX_BYTES = 64 * 1024 * 1024;

/**
 * 列印的唯一入口。
 *
 * Electron：跳自建列印對話框（PDF 預覽 + 印表機 / 份數 / 縮放）→ 主行程用指定的紙張
 * 與 100% 縮放 silent 送印。
 * 瀏覽器（ng serve / 測試）：沒有印表機能力，對話框以 preview-only 模式只做預覽，
 * 確認後退回既有的「開新分頁，使用者自行列印」。
 *
 * 為什麼要有這層：舊做法直接 openPdfInNewTab，紙張與縮放全交給檢視器與驅動 → 同一份 PDF
 * 在不同機器結果不同。背景與決策見 docs/blueprints/print-channel-electron.md。
 */
@Injectable({ providedIn: 'root' })
export class PrintService {
  private readonly api = inject(ReportApi);
  private readonly batch = inject(BatchPrintService);
  private readonly chunked = inject(ChunkedPrintService);
  private readonly panel = inject(BatchPrintPanelService);
  private readonly dialog = inject(PrintDialogService);
  private readonly auth = inject(AuthStore);

  /**
   * 單筆報表。兩條路都先由 renderer 取 blob——預覽需要 bytes 在前端，而單筆 PDF 不到 1 MB，
   * IPC 複製成本可忽略。取檔失敗時 ApiError 直接往上丟（「找不到報名」等中文訊息才出得來）。
   * @returns 是否真的送出（取消回 false）。
   */
  async printSingle(type: SingleReportType, signupId: string): Promise<boolean> {
    const pdf = await this.api.single(type, signupId);
    return this.confirmAndPrint(type, pdf.blob, pdf.pageSizeHeader);
  }

  /**
   * 批次報表。
   *
   * 先跟後端要清單（`batch/plan`）才知道要不要分段——`/file` 是 one-shot、單一大 PDF 又會爆
   * PdfSharp 的 2 GB 上限，所以「切幾段」必須在建任何 job 之前決定。
   *
   * - ≤ SEGMENT_SIZE：單段，走既有的 ProgressOverlay + 列印對話框，體驗與改版前相同。
   * - > SEGMENT_SIZE：分段模式，逐段渲染送印並顯示分段面板（可暫停、可單段重印）。
   *
   * 瀏覽器沒有印表機能力，維持既有的「單一大 PDF 開新分頁」——那是 dev 環境，不做分段。
   * @returns 是否真的送出（使用者取消皆回 false）。
   */
  async printBatch(req: BatchReportRequest, opts: BatchPrintOptions = {}): Promise<boolean> {
    if (!isElectron()) {
      const pdf = await this.batch.run(req, opts);
      if (!pdf) return false;
      return this.confirmAndPrint(req.reportType, pdf.blob, pdf.pageSizeHeader);
    }

    // 這一步的錯誤（編號錯誤／查無資料）直接往上丟，錯誤碼與 batch/jobs 完全相同
    const plan = await this.api.createBatchPlan(req);

    if (plan.total <= SEGMENT_SIZE) {
      // signupIds 帶進去：用 plan 已經選定的那一批，不讓後端再查一次而有機會不一致
      const pdf = await this.batch.run(
        { ...req, signupIds: plan.items.map((i) => i.id) },
        opts,
      );
      if (!pdf) return false;
      return this.confirmAndPrint(
        req.reportType,
        pdf.blob,
        pdf.pageSizeHeader,
        `共 ${plan.total} 筆`,
      );
    }

    return this.runChunked(req, plan);
  }

  /**
   * 大量列印：逐段渲染送印。列印設定只在第一段問一次（含該段預覽），其餘段沿用。
   * @returns 是否有任何一段送出。
   */
  private async runChunked(req: BatchReportRequest, plan: BatchReportPlan): Promise<boolean> {
    let choice: ReportPrintSetting | null = null;
    let printedAny = false;

    const run = this.chunked.start(
      req,
      plan.items,
      REPORT_META[req.reportType].label,
      async (type, blob, pageSizeHeader) => {
        const bytes = new Uint8Array(await blob.arrayBuffer());
        const sent = this.report(
          await this.bridge().printPdfBuffer(type, bytes, choice!, pageSizeHeader),
        );
        printedAny ||= sent;
        return sent;
      },
      async (firstSegmentPdf) => {
        const segments = Math.ceil(plan.total / SEGMENT_SIZE);
        choice = await this.askFor(req.reportType, {
          detail: `共 ${plan.total} 筆，分 ${segments} 段`,
          previewBlob: firstSegmentPdf,
        });
        return choice !== null;
      },
    );

    await this.panel.open(run);
    return printedAny;
  }

  /**
   * 列印 renderer 手上既有的 PDF（報表預覽頁）。
   * 瀏覽器不開對話框：呼叫端畫面上已經有全尺寸預覽，再疊一個預覽是重複。
   */
  async printBlob(
    type: SingleReportType,
    blob: Blob,
    pageSizeHeader?: string | null,
  ): Promise<boolean> {
    if (!isElectron()) {
      openPdfInNewTab(blob);
      return true;
    }
    return this.confirmAndPrint(type, blob, pageSizeHeader);
  }

  /** 預覽 → 確認 → 送印（Electron）／開新分頁（瀏覽器）。 */
  private async confirmAndPrint(
    type: SingleReportType,
    blob: Blob,
    pageSizeHeader?: string | null,
    detail?: string,
  ): Promise<boolean> {
    const tooBig = blob.size > PREVIEW_MAX_BYTES;
    const choice = await this.askFor(type, {
      detail,
      previewBlob: tooBig ? null : blob,
      previewNotice: tooBig ? '檔案較大，略過預覽' : undefined,
    });
    if (!choice) return false;

    if (!isElectron()) {
      openPdfInNewTab(blob);
      return true;
    }

    const bytes = new Uint8Array(await blob.arrayBuffer());
    return this.report(
      await this.bridge().printPdfBuffer(type, bytes, choice, pageSizeHeader),
    );
  }

  /**
   * 開對話框並套用上次記住的設定；使用者勾「記住」就寫回 print-settings.json。
   * 瀏覽器沒有 bridge，走 preview-only 模式（不查印表機、不存設定）。
   */
  private async askFor(
    type: SingleReportType,
    o: { detail?: string; previewBlob?: Blob | null; previewNotice?: string } = {},
  ): Promise<ReportPrintSetting | null> {
    const meta = REPORT_META[type];
    const base = { reportLabel: meta.label, paperLabel: meta.paper, ...o };

    if (!isElectron()) {
      const r = await this.dialog.ask({
        ...base,
        mode: 'preview-only',
        printers: [],
        copies: 1,
        scaleMode: 'actual',
      });
      return r ? { copies: 1, scaleMode: 'actual' } : null;
    }

    const bridge = this.bridge();
    const [printers, settings] = await Promise.all([
      bridge.listPrinters(),
      bridge.getPrintSettings(),
    ]);
    const saved = settings.byReportType[type] ?? {};

    const result = await this.dialog.ask({
      ...base,
      mode: 'printer',
      printers,
      deviceName: saved.deviceName,
      copies: saved.copies ?? 1,
      scaleMode: saved.scaleMode ?? 'actual',
    });
    if (!result) return null;

    const setting: ReportPrintSetting = {
      deviceName: result.deviceName,
      copies: result.copies,
      scaleMode: result.scaleMode,
    };
    if (result.remember) await bridge.savePrintSetting(type, setting);
    return setting;
  }

  /**
   * 送印失敗轉成例外，讓呼叫端沿用既有的錯誤提示流程；使用者取消則靜默。
   *
   * 用 UserFacingError 而不是原生 Error：主行程回的訊息（「列印逾時（印表機無回應）」
   * 「尚未連線」、sidecar 的 DomainException message）本來就是寫給使用者看的，
   * 走原生 Error 會被 toMessage 吞成「操作失敗，請稍後再試」。
   * 也不要偽造 ApiError——status / errorCode 是假的，會汙染日後依 errorCode 分支的程式。
   */
  private report(r: { ok: boolean; canceled?: boolean; error?: string }): boolean {
    if (r.ok) return true;
    if (r.canceled) return false;
    throw new UserFacingError(r.error ?? '列印失敗');
  }

  private bridge() {
    const b = ceremony();
    if (!b) throw new UserFacingError('列印功能僅在桌面版可用');
    return b;
  }

  private token(): string {
    return this.auth.token() ?? '';
  }
}
