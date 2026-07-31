import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../../environments/environment';
import type {
  BatchJobCreated,
  BatchJobState,
  BatchReportPlan,
  BatchReportRequest,
  ReportPdf,
  SingleReportType,
} from './report.models';

@Injectable({ providedIn: 'root' })
export class ReportApi {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/reports`;

  async single(type: SingleReportType, signupId: string): Promise<ReportPdf> {
    const params = new HttpParams().set('signupId', signupId);
    const resp = await firstValueFrom(
      this.http.get(`${this.base}/${type}`, {
        params,
        observe: 'response',
        responseType: 'blob',
      }),
    );
    return {
      blob: resp.body!,
      fileName: extractFileName(resp.headers.get('content-disposition')) ?? `${type}-${signupId}.pdf`,
      pageSizeHeader: resp.headers.get('x-report-page-size') ?? undefined,
    };
  }

  // ── 批次列印 job（有進度回報與取消）───────────────────────────────
  // 舊的同步 POST /reports/batch 後端仍保留（相容契約 + 整合測試），但前端一律走 job 版，
  // 所以這裡不再提供 batch()。Blueprint: docs/blueprints/api-endpoints/post-reports-batch-jobs.md

  /**
   * 解析批次範圍，取得要印的報名清單（不渲染）。大量列印切段用。
   * 錯誤碼／訊息與 createBatchJob 完全相同（後端共用 ResolveAsync）。
   */
  createBatchPlan(req: BatchReportRequest): Promise<BatchReportPlan> {
    return firstValueFrom(this.http.post<BatchReportPlan>(`${this.base}/batch/plan`, req));
  }

  /** 建立批次列印工作。驗證與查詢仍是同步的 → 錯誤碼／訊息與同步版完全相同。 */
  createBatchJob(req: BatchReportRequest): Promise<BatchJobCreated> {
    return firstValueFrom(this.http.post<BatchJobCreated>(`${this.base}/batch/jobs`, req));
  }

  getBatchJob(jobId: string): Promise<BatchJobState> {
    return firstValueFrom(this.http.get<BatchJobState>(`${this.base}/batch/jobs/${jobId}`));
  }

  /** 取出成品 PDF。伺服器端取完即釋放，同一個 job 只能取一次。 */
  async getBatchJobFile(jobId: string, fallbackName: string): Promise<ReportPdf> {
    const resp = await firstValueFrom(
      this.http.get(`${this.base}/batch/jobs/${jobId}/file`, {
        observe: 'response',
        responseType: 'blob',
      }),
    );
    const countHeader = resp.headers.get('x-signup-count');
    return {
      blob: resp.body!,
      // job 已經給過檔名，header 只是備援（跨源時需要後端 WithExposedHeaders 才讀得到）
      fileName: extractFileName(resp.headers.get('content-disposition')) ?? fallbackName,
      signupCount: countHeader ? Number(countHeader) : undefined,
      pageSizeHeader: resp.headers.get('x-report-page-size') ?? undefined,
    };
  }

  /** 取消工作。冪等，失敗與否呼叫端不需在意。 */
  cancelBatchJob(jobId: string): Promise<void> {
    return firstValueFrom(this.http.delete<void>(`${this.base}/batch/jobs/${jobId}`));
  }
}

function extractFileName(disposition: string | null): string | null {
  if (!disposition) return null;
  const star = /filename\*=UTF-8''([^;]+)/i.exec(disposition);
  if (star) return decodeURIComponent(star[1].trim().replace(/^"|"$/g, ''));
  const plain = /filename="?([^";]+)"?/i.exec(disposition);
  return plain ? plain[1].trim() : null;
}
