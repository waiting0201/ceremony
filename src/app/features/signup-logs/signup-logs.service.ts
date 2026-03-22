import { Injectable, inject } from '@angular/core';
import { IpcService } from '../../core/services/ipc.service';
import type { IpcResult } from '../../core/models/ipc-result.model';

export interface SignupLog {
  SignupLogID: string;
  SignupID: string;
  Year: number;
  CeremonyCategoryTitle: string;
  SignupType: number;
  Name: string;
  Admin: string;
  Createdate: string;
  [key: string]: any;
}

@Injectable({ providedIn: 'root' })
export class SignupLogsService {
  private ipc = inject(IpcService);

  search(params: any): Promise<IpcResult<{ rows: SignupLog[]; total: number }>> {
    return this.ipc.invoke('signup-logs:search', params);
  }
}
