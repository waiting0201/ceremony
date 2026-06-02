import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../../environments/environment';
import type { BackupRequest, BackupResult } from './backup.models';

@Injectable({ providedIn: 'root' })
export class BackupApi {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/backup`;

  run(body: BackupRequest = {}): Promise<BackupResult> {
    return firstValueFrom(this.http.post<BackupResult>(this.base, body));
  }

  /** 下載 endpoint URL（Electron 原生下載用；auth header 由 main process 帶）。 */
  downloadUrl(fileName: string): string {
    return `${this.base}/${encodeURIComponent(fileName)}/download`;
  }

  /** 瀏覽器 fallback：抓 .bak blob（authInterceptor 會自動帶 Bearer token）。 */
  fetchBlob(fileName: string): Promise<Blob> {
    return firstValueFrom(this.http.get(this.downloadUrl(fileName), { responseType: 'blob' }));
  }
}
