import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../../environments/environment';
import type {
  BatchJobCreated,
  BatchJobState,
  BatchReportRequest,
  ReportPdf,
  SingleReportType,
} from './report.models';

@Injectable({ providedIn: 'root' })
export class ReportApi {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/reports`;

  /**
   * @param opts.debugGrid 薦牌「對位校正版」：同一筆資料 + 1cm 刻度格線（見 print.service.ts）。
   */
  async single(
    type: SingleReportType,
    signupId: string,
    opts: { debugGrid?: boolean } = {},
  ): Promise<ReportPdf> {
    let params = new HttpParams().set('signupId', signupId);
    if (opts.debugGrid) params = params.set('debugGrid', 'true');
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
    };
  }

  // ── 批次列印 job（唯一的批次路徑）─────────────────────────────────
  // Blueprint: docs/blueprints/api-endpoints/post-reports-batch-jobs.md
  // Electron 只用 createBatchJob + getBatchJob 等渲染完成，成品由主行程串流取檔；
  // getBatchJobFile 是給瀏覽器與報表預覽頁用的。

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
