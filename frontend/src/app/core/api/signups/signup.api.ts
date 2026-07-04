import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../../environments/environment';
import type {
  CreateSignupRequest,
  SignupDuplicateListResponse,
  SignupListItem,
  SignupListResponse,
  SignupLogListResponse,
  SignupSearchQuery,
} from './signup.models';

@Injectable({ providedIn: 'root' })
export class SignupApi {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/signups`;

  search(query: SignupSearchQuery): Promise<SignupListResponse> {
    let params = new HttpParams();
    if (query.year != null) params = params.set('year', query.year);
    if (query.isScope) params = params.set('isScope', 'true');
    if (query.ceremonyCategoryId) params = params.set('ceremonyCategoryId', query.ceremonyCategoryId);
    if (query.signupType != null) params = params.set('signupType', query.signupType);
    if (query.number != null) params = params.set('number', query.number);
    if (query.searchKey) params = params.set('searchKey', query.searchKey);
    if (query.scopeName) params = params.set('scopeName', 'true');
    if (query.scopeLivingName) params = params.set('scopeLivingName', 'true');
    if (query.scopeDeadName) params = params.set('scopeDeadName', 'true');
    if (query.scopePhone) params = params.set('scopePhone', 'true');
    if (query.isFixedNumber) params = params.set('isFixedNumber', 'true');
    return firstValueFrom(this.http.get<SignupListResponse>(this.base, { params }));
  }

  getById(id: string): Promise<SignupListItem> {
    return firstValueFrom(this.http.get<SignupListItem>(`${this.base}/${id}`));
  }

  /**
   * 重複報名警示：查某信眾在同一 (year, ceremonyCategoryId) 既有的報名（忽略 signupType）。
   * 編輯模式帶 excludeSignupId 排除自己。
   */
  checkDuplicates(q: {
    year: number;
    ceremonyCategoryId: string;
    believerId: string;
    excludeSignupId?: string | null;
  }): Promise<SignupDuplicateListResponse> {
    let params = new HttpParams()
      .set('year', q.year)
      .set('ceremonyCategoryId', q.ceremonyCategoryId)
      .set('believerId', q.believerId);
    if (q.excludeSignupId) params = params.set('excludeSignupId', q.excludeSignupId);
    return firstValueFrom(
      this.http.get<SignupDuplicateListResponse>(`${this.base}/duplicates`, { params }),
    );
  }

  listLogs(id: string): Promise<SignupLogListResponse> {
    return firstValueFrom(this.http.get<SignupLogListResponse>(`${this.base}/${id}/logs`));
  }

  create(body: CreateSignupRequest): Promise<SignupListItem> {
    return firstValueFrom(this.http.post<SignupListItem>(this.base, body));
  }

  /**
   * 插入報名於指定編號（body.customNumber），並把同群組內 Number ≥ 該編號的既有報名 +1 順移。
   * 對應列表右鍵「在此前插入」。
   */
  insertShift(body: CreateSignupRequest): Promise<SignupListItem> {
    return firstValueFrom(this.http.post<SignupListItem>(`${this.base}/insert-shift`, body));
  }

  update(id: string, body: CreateSignupRequest): Promise<SignupListItem> {
    return firstValueFrom(this.http.put<SignupListItem>(`${this.base}/${id}`, body));
  }

  remove(id: string): Promise<void> {
    return firstValueFrom(this.http.delete<void>(`${this.base}/${id}`));
  }

  async exportExcel(query: SignupSearchQuery): Promise<{ blob: Blob; fileName: string }> {
    const resp = await firstValueFrom(
      this.http.post(`${this.base}/export`, query, { observe: 'response', responseType: 'blob' }),
    );
    const fileName = extractFileName(resp.headers.get('content-disposition')) ?? 'signups.xlsx';
    return { blob: resp.body!, fileName };
  }
}

function extractFileName(disposition: string | null): string | null {
  if (!disposition) return null;
  const star = /filename\*=UTF-8''([^;]+)/i.exec(disposition);
  if (star) return decodeURIComponent(star[1].trim().replace(/^"|"$/g, ''));
  const plain = /filename="?([^";]+)"?/i.exec(disposition);
  return plain ? plain[1].trim() : null;
}
