import { Injectable, inject } from '@angular/core';
import { IpcService } from '../../core/services/ipc.service';
import type { IpcResult } from '../../core/models/ipc-result.model';

export interface Believer {
  BelieverID: string;
  EmployeeType: number;
  HallName: string | null;
  Name: string;
  Phone: string | null;
  MailZipcodeID: number | null;
  MailZipcode: string | null;
  MailAddress: string | null;
  MailCity?: string;
  MailArea?: string;
  TextZipcodeID: number | null;
  TextZipcode: string | null;
  TextAddress: string | null;
  TextCity?: string;
  TextArea?: string;
  LivingNameOne: string | null;
  LivingNameTwo: string | null;
  LivingNameThree: string | null;
  LivingNameFour: string | null;
  LivingNameFive: string | null;
  LivingNameSix: string | null;
  DeadNameOne: string | null;
  DeadNameTwo: string | null;
  DeadNameThree: string | null;
  DeadNameFour: string | null;
  DeadNameFive: string | null;
  DeadNameSix: string | null;
  IsFixedNumber: boolean;
}

export interface SearchParams {
  keyword?: string;
  page?: number;
  pageSize?: number;
}

@Injectable({ providedIn: 'root' })
export class BelieversService {
  private ipc = inject(IpcService);

  search(params: SearchParams): Promise<IpcResult<{ rows: Believer[]; total: number }>> {
    return this.ipc.invoke('believers:search', params);
  }

  get(id: string): Promise<IpcResult<Believer>> {
    return this.ipc.invoke('believers:get', id);
  }

  create(data: Partial<Believer>): Promise<IpcResult<Believer>> {
    return this.ipc.invoke('believers:create', data);
  }

  update(id: string, data: Partial<Believer>): Promise<IpcResult<Believer>> {
    return this.ipc.invoke('believers:update', id, data);
  }

  delete(id: string): Promise<IpcResult> {
    return this.ipc.invoke('believers:delete', id);
  }

  lookup(keyword: string): Promise<IpcResult<Believer[]>> {
    return this.ipc.invoke('believers:lookup', keyword);
  }
}
