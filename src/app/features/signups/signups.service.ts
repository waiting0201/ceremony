import { Injectable, inject } from '@angular/core';
import { IpcService } from '../../core/services/ipc.service';
import type { IpcResult } from '../../core/models/ipc-result.model';

export interface SignupView {
  SignupID: string;
  BelieverID: string | null;
  Year: number;
  CeremonyTitle: string;
  CeremonySort: number;
  SignupType: number;
  CeremonyCategoryID: string;
  NumberTitle: string | null;
  Number: number | null;
  Fee: number | null;
  Employee: string | null;
  Name: string | null;
  Remark: string | null;
  HallName: string | null;
  DeadNameOne: string | null;
  DeadNameTwo: string | null;
  DeadNameThree: string | null;
  DeadNameFour: string | null;
  DeadNameFive: string | null;
  DeadNameSix: string | null;
  LivingNameOne: string | null;
  LivingNameTwo: string | null;
  LivingNameThree: string | null;
  LivingNameFour: string | null;
  LivingNameFive: string | null;
  LivingNameSix: string | null;
  Phone: string | null;
  MailAddress: string | null;
  TextAddress: string | null;
  AdminName: string | null;
  Createdate: string;
  IsFixedNumber: boolean | null;
  PrepayYear: number | null;
  PrepayCeremonyTitle: string | null;
}

export interface SignupSearchParams {
  year?: number;
  ceremonyCategoryId?: string;
  signupType?: number;
  keyword?: string;
  dateFrom?: string;
  dateTo?: string;
  page?: number;
  pageSize?: number;
}

@Injectable({ providedIn: 'root' })
export class SignupsService {
  private ipc = inject(IpcService);

  search(params: SignupSearchParams): Promise<IpcResult<{ rows: SignupView[]; total: number }>> {
    return this.ipc.invoke('signups:search', params);
  }

  get(id: string): Promise<IpcResult<any>> {
    return this.ipc.invoke('signups:get', id);
  }

  create(data: any): Promise<IpcResult<any>> {
    return this.ipc.invoke('signups:create', data);
  }

  update(id: string, data: any): Promise<IpcResult<any>> {
    return this.ipc.invoke('signups:update', id, data);
  }

  delete(id: string): Promise<IpcResult> {
    return this.ipc.invoke('signups:delete', id);
  }

  getNextNumber(year: number, ccId: string, signupType: number): Promise<IpcResult<number>> {
    return this.ipc.invoke('signups:nextNumber', year, ccId, signupType);
  }
}
