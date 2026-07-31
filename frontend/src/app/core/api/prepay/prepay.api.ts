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

  /**
   * 取某信眾今年(含)以前最新報名的預繳資訊。
   *
   * **2026-07-31 起無呼叫端**（保留備用）：原由 `signup-edit-form.pickBeliever` 使用，但這支查詢
   * 不分報名類型，會把法會的預繳帶到普桌（SignupType 4）；選信眾改取該筆報名自身的預繳後即不再需要。
   * 見 docs/blueprints/api-endpoints/get-prepay-believer-latest.md。
   */
  believerLatest(believerId: string, year: number): Promise<BelieverLatestPrepay> {
    const params = new HttpParams().set('believerId', believerId).set('year', year);
    return firstValueFrom(this.http.get<BelieverLatestPrepay>(this.base, { params }));
  }
}
