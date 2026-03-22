import { Injectable, signal, computed } from '@angular/core';
import type { Session } from '../models/session.model';
import type { IpcResult } from '../models/ipc-result.model';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private _session = signal<Session | null>(null);

  readonly session = this._session.asReadonly();
  readonly isLoggedIn = computed(() => this._session() !== null);
  readonly adminName = computed(() => this._session()?.Name ?? '');

  async login(username: string, password: string): Promise<IpcResult<Session>> {
    if (!window.electronAPI) {
      return { success: false, message: '不在 Electron 環境中' };
    }
    const result = await window.electronAPI.login(username, password);
    if (result.success && result.data) {
      this._session.set(result.data);
    }
    return result;
  }

  async logout(): Promise<void> {
    if (window.electronAPI) {
      await window.electronAPI.logout();
    }
    this._session.set(null);
  }

  async checkSession(): Promise<boolean> {
    if (!window.electronAPI) return false;
    const result = await window.electronAPI.getSession();
    if (result.success && result.data) {
      this._session.set(result.data);
      return true;
    }
    return false;
  }
}
