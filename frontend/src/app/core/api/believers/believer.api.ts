import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../../environments/environment';
import type {
  BelieverListItem,
  BelieverListResponse,
  BelieverSearchQuery,
  BelieverUpsertRequest,
} from './believer.models';

@Injectable({ providedIn: 'root' })
export class BelieverApi {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/believers`;

  search(query: BelieverSearchQuery): Promise<BelieverListResponse> {
    let params = new HttpParams();
    if (query.name) params = params.set('name', query.name);
    if (query.phone) params = params.set('phone', query.phone);
    if (query.hallName) params = params.set('hallName', query.hallName);
    if (query.livingName) params = params.set('livingName', query.livingName);
    if (query.deadName) params = params.set('deadName', query.deadName);
    return firstValueFrom(this.http.get<BelieverListResponse>(this.base, { params }));
  }

  getById(id: string): Promise<BelieverListItem> {
    return firstValueFrom(this.http.get<BelieverListItem>(`${this.base}/${id}`));
  }

  create(body: BelieverUpsertRequest): Promise<BelieverListItem> {
    return firstValueFrom(this.http.post<BelieverListItem>(this.base, body));
  }

  update(id: string, body: BelieverUpsertRequest): Promise<BelieverListItem> {
    return firstValueFrom(this.http.put<BelieverListItem>(`${this.base}/${id}`, body));
  }

  remove(id: string): Promise<void> {
    return firstValueFrom(this.http.delete<void>(`${this.base}/${id}`));
  }
}
