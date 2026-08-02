import { inject, Injectable } from '@angular/core';
import { ReportApi } from '../api/reports/report.api';
import type { BatchReportRequest, ReportPdf } from '../api/reports/report.models';
import { ApiError } from '../http/api-error';
import { ProgressOverlayService } from '../../shared/progress-overlay/progress-overlay.service';

/** 輪詢間隔。localhost 回圈、單一使用者，成本可忽略；比「渲染完一筆」還密，視覺上等同 push。 */
const POLL_INTERVAL_MS = 250;
/** 保險絲：超過就主動取消 job，避免 overlay 永遠關不掉。 */
const MAX_WAIT_MS = 10 * 60 * 1000;

/** {@link BatchPrintService.render} 的結果：job 已渲染完成，成品還在伺服器上等人取。 */
export interface RenderedBatch {
  jobId: string;
  fileName: string;
}

export interface BatchPrintOptions {
  /** overlay 標題，預設「批次列印中」 */
  title?: string;
  /** overlay 副標，通常是報表名稱 */
  detail?: string;
}

/**
 * 單一批次 job 的流程總管：建 job → 顯示進度 overlay → 輪詢 → 取檔 / 取消。
 *
 * 兩種收尾方式：
 * - {@link render}：只等渲染完成，回 jobId。Electron 走這條——成品由主行程串流取檔，
 *   合併成一份的批次可達數百 MB，經 renderer 就是白白多一份記憶體。
 * - {@link run}：連成品 blob 一起取回。瀏覽器（ng serve）與報表預覽頁走這條。
 *
 * 放在 core/ 是因為 signups 與 reports 兩個 feature 都要用；放任一 feature 會造成跨 feature 相依。
 * Blueprint: docs/blueprints/api-endpoints/post-reports-batch-jobs.md
 */
@Injectable({ providedIn: 'root' })
export class BatchPrintService {
  private readonly api = inject(ReportApi);
  private readonly overlay = inject(ProgressOverlayService);

  /**
   * 建 job 並等到渲染完成。**不取檔**——`/file` 是 one-shot，留給真正要拿成品的人呼叫。
   * @returns jobId 與成品檔名；使用者中途取消時回 `null`。
   * @throws {ApiError} 建立失敗（編號錯誤／查無資料…）或渲染失敗
   */
  async render(
    req: BatchReportRequest,
    opts: BatchPrintOptions = {},
  ): Promise<RenderedBatch | null> {
    // 這一步的錯誤直接往上丟：狀態碼與訊息與同步版完全相同，呼叫端錯誤處理不用改
    const job = await this.api.createBatchJob(req);

    const handle = this.overlay.open({
      title: opts.title ?? '批次列印中',
      detail: opts.detail,
      total: job.total,
      completed: 0,
    });

    let canceledByUser = false;
    void handle.canceled.then(() => (canceledByUser = true));

    const deadline = Date.now() + MAX_WAIT_MS;

    try {
      for (;;) {
        if (canceledByUser) {
          await this.cancelQuietly(job.jobId);
          return null;
        }

        if (Date.now() > deadline) {
          await this.cancelQuietly(job.jobId);
          throw new ApiError(408, 'BATCH_JOB_TIMEOUT', '批次列印逾時，已中止');
        }

        const state = await this.api.getBatchJob(job.jobId);

        switch (state.status) {
          case 'running':
            handle.update({
              completed: state.completed,
              // 筆數跑滿但還沒結束＝伺服器正在合併 PDF，講清楚免得看起來卡住
              note: state.completed >= state.total ? '合併 PDF…' : undefined,
            });
            break;

          case 'completed':
            handle.update({ completed: state.total, note: '開啟預覽…', cancelable: false });
            return { jobId: job.jobId, fileName: state.fileName || job.fileName };

          case 'canceled':
            return null;

          case 'failed':
            throw new ApiError(
              500,
              state.errorCode ?? 'INTERNAL_ERROR',
              state.message ?? '未預期的伺服器錯誤',
            );
        }

        await delay(POLL_INTERVAL_MS);
      }
    } finally {
      handle.close();
    }
  }

  /**
   * 同 {@link render}，但連成品一起取回 renderer。
   * @returns 成品 PDF；使用者中途取消時回 `null`。
   */
  async run(req: BatchReportRequest, opts: BatchPrintOptions = {}): Promise<ReportPdf | null> {
    const rendered = await this.render(req, opts);
    if (!rendered) return null;
    return this.api.getBatchJobFile(rendered.jobId, rendered.fileName);
  }

  /** 取消是 best-effort：使用者已經要走了，這裡再失敗也沒有補救動作可做。 */
  private async cancelQuietly(jobId: string): Promise<void> {
    try {
      await this.api.cancelBatchJob(jobId);
    } catch {
      /* 忽略 */
    }
  }
}

function delay(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms));
}
