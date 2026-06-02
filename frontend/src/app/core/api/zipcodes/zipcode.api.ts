import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../../environments/environment';
import type { ZipcodeAreasResponse, ZipcodeCitiesResponse } from './zipcode.models';

/**
 * 郵遞區號（城市 / 區域連動下拉資料來源）。
 * 對齊舊 NewSignupForm.LoadCity / dlMailCity_SelectedIndexChanged。
 */
@Injectable({ providedIn: 'root' })
export class ZipcodeApi {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/zipcodes`;

  cities(): Promise<ZipcodeCitiesResponse> {
    return firstValueFrom(this.http.get<ZipcodeCitiesResponse>(`${this.base}/cities`));
  }

  areas(city: string): Promise<ZipcodeAreasResponse> {
    return firstValueFrom(
      this.http.get<ZipcodeAreasResponse>(this.base, { params: { city } }),
    );
  }
}
