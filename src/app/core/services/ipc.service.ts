import { Injectable } from '@angular/core';
import type { IpcResult } from '../models/ipc-result.model';

declare global {
  interface Window {
    electronAPI?: {
      dbTest: () => Promise<IpcResult<any>>;
      login: (username: string, password: string) => Promise<IpcResult<any>>;
      logout: () => Promise<IpcResult>;
      getSession: () => Promise<IpcResult<any>>;
      windowMinimize: () => Promise<void>;
      windowMaximize: () => Promise<void>;
      windowClose: () => Promise<void>;
      invoke: (channel: string, ...args: any[]) => Promise<any>;
    };
  }
}

@Injectable({ providedIn: 'root' })
export class IpcService {
  private get api() {
    return window.electronAPI;
  }

  get isElectron(): boolean {
    return !!this.api;
  }

  async invoke<T = any>(channel: string, ...args: any[]): Promise<IpcResult<T>> {
    if (!this.api) {
      return { success: false, message: '不在 Electron 環境中' };
    }
    return this.api.invoke(channel, ...args);
  }

  // Window controls
  minimize(): void {
    this.api?.windowMinimize();
  }
  maximize(): void {
    this.api?.windowMaximize();
  }
  close(): void {
    this.api?.windowClose();
  }
}
