import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../../environments/environment';
import type { BelieverLatestPrepay, PrepayLoadRequest, PrepayLoadResponse } from './prepay.models';

@Injectable({ providedIn: 'root' })
export class PrepayApi {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/prepay`;

  load(body: PrepayLoadRequest): Promise<PrepayLoadResponse> {
    return firstValueFrom(this.http.post<PrepayLoadResponse>(`${this.base}/load`, body));
  }

  /** 取某信眾今年(含)以前最新報名的預繳資訊（新增報名選信眾時自動帶入）。 */
  believerLatest(believerId: string, year: number): Promise<BelieverLatestPrepay> {
    const params = new HttpParams().set('believerId', believerId).set('year', year);
    return firstValueFrom(this.http.get<BelieverLatestPrepay>(this.base, { params }));
  }
}
