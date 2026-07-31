import { inject, Injectable } from '@angular/core';
import { AuthStore } from '../auth/auth.store';
import { ceremony, isElectron } from '../platform/electron';
import type { ReportPrintSetting } from '../platform/electron';
import { BatchPrintService } from '../reports/batch-print.service';
import type { BatchPrintOptions } from '../reports/batch-print.service';
import { ReportApi } from '../api/reports/report.api';
import type { BatchReportRequest, SingleReportType } from '../api/reports/report.models';
import { openPdfInNewTab } from '../../shared/util/pdf';
import { PrintDialogService } from '../../shared/print-dialog/print-dialog.service';

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
 * 列印的唯一入口。
 *
 * Electron：跳自建列印對話框（選印表機 / 份數 / 縮放）→ 主行程用指定的紙張與 100% 縮放送印。
 * 瀏覽器（ng serve / 測試）：沒有印表機能力，退回既有的「開新分頁預覽，使用者自行列印」。
 *
 * 為什麼要有這層：舊做法直接 openPdfInNewTab，紙張與縮放全交給檢視器與驅動 → 同一份 PDF
 * 在不同機器結果不同。背景與決策見 docs/blueprints/print-channel-electron.md。
 */
@Injectable({ providedIn: 'root' })
export class PrintService {
  private readonly api = inject(ReportApi);
  private readonly batch = inject(BatchPrintService);
  private readonly dialog = inject(PrintDialogService);
  private readonly auth = inject(AuthStore);

  /** 單筆報表。@returns 是否真的送出（取消回 false）。 */
  async printSingle(type: SingleReportType, signupId: string): Promise<boolean> {
    if (!isElectron()) {
      const pdf = await this.api.single(type, signupId);
      openPdfInNewTab(pdf.blob);
      return true;
    }

    const choice = await this.askFor(type);
    if (!choice) return false;

    const path = `/reports/${type}?signupId=${encodeURIComponent(signupId)}`;
    return this.report(await this.bridge().printReport(type, path, this.token(), choice));
  }

  /**
   * 批次報表。job 的進度 overlay 與取消仍由 BatchPrintService 負責，
   * Electron 只在完成後接手取檔送印（/file 是 one-shot，不能兩邊都取）。
   * @returns 是否真的送出（使用者取消 job 或取消列印皆回 false）。
   */
  async printBatch(
    req: BatchReportRequest,
    // takeFile 由本服務決定（Electron 一律 false），呼叫端只給 overlay 文案
    opts: Omit<BatchPrintOptions, 'takeFile'> = {},
  ): Promise<boolean> {
    if (!isElectron()) {
      const pdf = await this.batch.run(req, opts);
      if (!pdf) return false;
      openPdfInNewTab(pdf.blob);
      return true;
    }

    const job = await this.batch.run(req, { ...opts, takeFile: false });
    if (!job) return false;

    // 對話框刻意排在 job 完成之後：先讓使用者看到「共幾筆」再決定印表機與份數，
    // 也避免對話框開著時 job 逾時（後端 job TTL 10 分鐘）。
    const choice = await this.askFor(req.reportType, `共 ${job.total} 筆`);
    if (!choice) return false;

    return this.report(
      await this.bridge().printBatchJob(req.reportType, job.jobId, this.token(), choice),
    );
  }

  /** 列印 renderer 手上既有的 PDF（報表預覽頁）。 */
  async printBlob(type: SingleReportType, blob: Blob): Promise<boolean> {
    if (!isElectron()) {
      openPdfInNewTab(blob);
      return true;
    }

    const choice = await this.askFor(type);
    if (!choice) return false;

    const bytes = new Uint8Array(await blob.arrayBuffer());
    return this.report(await this.bridge().printPdfBuffer(type, bytes, choice));
  }

  /** 開對話框並套用上次記住的設定；使用者勾「記住」就寫回 print-settings.json。 */
  private async askFor(
    type: SingleReportType,
    detail?: string,
  ): Promise<ReportPrintSetting | null> {
    const bridge = this.bridge();
    const [printers, settings] = await Promise.all([
      bridge.listPrinters(),
      bridge.getPrintSettings(),
    ]);
    const saved = settings.byReportType[type] ?? {};
    const meta = REPORT_META[type];

    const result = await this.dialog.ask({
      reportLabel: meta.label,
      paperLabel: meta.paper,
      detail,
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

  /** 送印失敗轉成例外，讓呼叫端沿用既有的錯誤提示流程；使用者取消則靜默。 */
  private report(r: { ok: boolean; canceled?: boolean; error?: string }): boolean {
    if (r.ok) return true;
    if (r.canceled) return false;
    throw new Error(r.error ?? '列印失敗');
  }

  private bridge() {
    const b = ceremony();
    if (!b) throw new Error('列印功能僅在桌面版可用');
    return b;
  }

  private token(): string {
    return this.auth.token() ?? '';
  }
}
