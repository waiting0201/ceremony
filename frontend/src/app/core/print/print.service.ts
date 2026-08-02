import { inject, Injectable } from '@angular/core';
import { AuthStore } from '../auth/auth.store';
import { ceremony, isElectron } from '../platform/electron';
import { BatchPrintService } from '../reports/batch-print.service';
import type { BatchPrintOptions } from '../reports/batch-print.service';
import { ReportApi } from '../api/reports/report.api';
import type { BatchReportRequest, SingleReportType } from '../api/reports/report.models';
import { openPdfInNewTab } from '../../shared/util/pdf';
import { UserFacingError } from '../errors/to-message';

/**
 * 列印的唯一入口。
 *
 * **形狀對齊舊系統**（SignupForm.cs）：程式產出 PDF → 開預覽視窗 → 使用者按列印鈕 →
 * Windows 原生列印對話框選印表機／份數／紙張／方向／頁面範圍。
 * 我們不記任何列印偏好，也不指定任何送印參數——那正是 v2.3.7／v2.3.8 客訴的來源。
 *
 * Electron：PDF 由主行程直接向 sidecar 串流取檔（不經 renderer），開在 Chromium PDF 檢視器視窗。
 * 瀏覽器（ng serve / 測試）：沒有這個能力，退回既有的「開新分頁，使用者自行列印」。
 *
 * 背景與決策見 docs/blueprints/print-channel-electron.md。
 */
@Injectable({ providedIn: 'root' })
export class PrintService {
  private readonly api = inject(ReportApi);
  private readonly batch = inject(BatchPrintService);
  private readonly auth = inject(AuthStore);

  /**
   * 單筆報表。Electron 走串流取檔（renderer 完全不碰 PDF）；瀏覽器才需要自己抓 blob。
   * 取檔失敗時主行程會把後端的中文訊息原樣帶回來（「找不到報名」等）。
   * @returns 是否已開啟預覽視窗。
   */
  async printSingle(type: SingleReportType, signupId: string): Promise<boolean> {
    if (!isElectron()) {
      const pdf = await this.api.single(type, signupId);
      openPdfInNewTab(pdf.blob);
      return true;
    }
    return this.openInViewer(type, `/reports/${type}?signupId=${encodeURIComponent(signupId)}`);
  }

  /**
   * 批次報表：建背景 job → 進度 overlay → 完成後把成品開在預覽視窗。
   *
   * 合併成單一 PDF（＝舊系統 CombinePDFs），不分段。卡紙時的續印由原生列印對話框的
   * 「頁面範圍」承接——預覽視窗還開著，再按一次列印鈕填頁碼即可。
   *
   * @returns 是否已開啟預覽視窗（使用者中途取消回 false）。
   */
  async printBatch(req: BatchReportRequest, opts: BatchPrintOptions = {}): Promise<boolean> {
    if (!isElectron()) {
      const pdf = await this.batch.run(req, opts);
      if (!pdf) return false;
      openPdfInNewTab(pdf.blob);
      return true;
    }

    // 只等渲染完成，不取檔——成品由主行程串流下載，數百 MB 也不會經過 renderer
    const rendered = await this.batch.render(req, opts);
    if (!rendered) return false;

    return this.openInViewer(req.reportType, `/reports/batch/jobs/${rendered.jobId}/file`);
  }

  /**
   * 列印 renderer 手上既有的 PDF（報表預覽頁）。
   * 該頁已經有全尺寸預覽，這裡只是把同一份 PDF 送進檢視器視窗好按原生列印。
   */
  async printBlob(type: SingleReportType, blob: Blob): Promise<boolean> {
    if (!isElectron()) {
      openPdfInNewTab(blob);
      return true;
    }
    const bytes = new Uint8Array(await blob.arrayBuffer());
    return this.report(await this.bridge().openPdfInViewer(type, bytes));
  }

  /** 開啟診斷紀錄所在資料夾。輔助功能，失敗不打斷使用者。 */
  async openPrintLog(): Promise<void> {
    await ceremony()?.openPrintLogFolder().catch(() => undefined);
  }

  private async openInViewer(type: SingleReportType, apiPath: string): Promise<boolean> {
    return this.report(await this.bridge().openReportInViewer(type, apiPath, this.token()));
  }

  /**
   * 失敗轉成例外，讓呼叫端沿用既有的錯誤提示流程。
   *
   * 用 UserFacingError 而不是原生 Error：主行程回的訊息（「尚未連線」、sidecar 的
   * DomainException message）本來就是寫給使用者看的，走原生 Error 會被 toMessage
   * 吞成「操作失敗，請稍後再試」。也不偽造 ApiError——status / errorCode 是假的。
   */
  private report(r: { ok: boolean; error?: string }): boolean {
    if (r.ok) return true;
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
