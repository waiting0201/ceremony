import { Injectable, inject } from '@angular/core';
import { IpcService } from '../../core/services/ipc.service';
import type { IpcResult } from '../../core/models/ipc-result.model';

export interface Admin {
  AdminID: number;
  Name: string | null;
  Username: string;
  Password: string;
  IsEnabled: boolean;
}

@Injectable({ providedIn: 'root' })
export class AdminsService {
  private ipc = inject(IpcService);

  list(): Promise<IpcResult<Admin[]>> {
    return this.ipc.invoke<Admin[]>('admins:list');
  }

  create(data: Partial<Admin>): Promise<IpcResult<Admin>> {
    return this.ipc.invoke<Admin>('admins:create', data);
  }

  update(id: number, data: Partial<Admin>): Promise<IpcResult<Admin>> {
    return this.ipc.invoke<Admin>('admins:update', id, data);
  }

  delete(id: number): Promise<IpcResult> {
    return this.ipc.invoke('admins:delete', id);
  }
}
