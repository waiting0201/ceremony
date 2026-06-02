import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../../environments/environment';
import type { BatchReportRequest, ReportPdf, SingleReportType } from './report.models';

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
    };
  }

  async batch(req: BatchReportRequest): Promise<ReportPdf> {
    const resp = await firstValueFrom(
      this.http.post(`${this.base}/batch`, req, {
        observe: 'response',
        responseType: 'blob',
      }),
    );
    const countHeader = resp.headers.get('x-signup-count');
    return {
      blob: resp.body!,
      fileName:
        extractFileName(resp.headers.get('content-disposition')) ??
        `batch-${req.reportType}-${req.numberStart}-${req.numberEnd}.pdf`,
      signupCount: countHeader ? Number(countHeader) : undefined,
    };
  }
}

function extractFileName(disposition: string | null): string | null {
  if (!disposition) return null;
  const star = /filename\*=UTF-8''([^;]+)/i.exec(disposition);
  if (star) return decodeURIComponent(star[1].trim().replace(/^"|"$/g, ''));
  const plain = /filename="?([^";]+)"?/i.exec(disposition);
  return plain ? plain[1].trim() : null;
}
