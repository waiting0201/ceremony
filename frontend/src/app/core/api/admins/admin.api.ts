import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../../environments/environment';
import type {
  AdminListItem,
  AdminListResponse,
  CreateAdminRequest,
  UpdateAdminRequest,
} from './admin.models';

@Injectable({ providedIn: 'root' })
export class AdminApi {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/admins`;

  list(): Promise<AdminListResponse> {
    return firstValueFrom(this.http.get<AdminListResponse>(this.base));
  }

  create(body: CreateAdminRequest): Promise<AdminListItem> {
    return firstValueFrom(this.http.post<AdminListItem>(this.base, body));
  }

  update(id: number, body: UpdateAdminRequest): Promise<AdminListItem> {
    return firstValueFrom(this.http.put<AdminListItem>(`${this.base}/${id}`, body));
  }

  remove(id: number): Promise<void> {
    return firstValueFrom(this.http.delete<void>(`${this.base}/${id}`));
  }
}
