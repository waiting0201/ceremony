import { computed, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { signalStore, withComputed, withMethods, withState, patchState } from '@ngrx/signals';
import { environment } from '../../../environments/environment';
import type { AuthUser, LoginRequest, LoginResponse } from './auth.models';

interface AuthState {
  user: AuthUser | null;
  token: string | null;
}

// session 僅存於記憶體、不持久化（不寫 localStorage/sessionStorage）：
// 每次 App 啟動或 DB 連線成功後 renderer 會重新載入 → 記憶體狀態清空 →
// authGuard 看不到 token → 強制回登入頁。確保「必須登入才能進首頁」，
// 不會因殘留舊 token（可能來自不同 DB / 已失效）而跳過登入。
const EMPTY_STATE: AuthState = { user: null, token: null };

export const AuthStore = signalStore(
  { providedIn: 'root' },
  withState<AuthState>(EMPTY_STATE),
  withComputed(({ user, token }) => ({
    isLoggedIn: computed(() => user() !== null && token() !== null),
    adminId: computed(() => user()?.id ?? null),
    username: computed(() => user()?.username ?? null),
    displayName: computed(() => user()?.name ?? user()?.username ?? null),
  })),
  withMethods((store) => {
    const http = inject(HttpClient);
    return {
      async login(credentials: LoginRequest): Promise<void> {
        const result = await firstValueFrom(
          http.post<LoginResponse>(`${environment.apiBaseUrl}/auth/login`, credentials),
        );
        patchState(store, { user: result.user, token: result.token });
      },
      async logout(): Promise<void> {
        if (store.token()) {
          try {
            await firstValueFrom(
              http.post(`${environment.apiBaseUrl}/auth/logout`, {}),
            );
          } catch {
            /* token may already be revoked / expired — proceed with local clear */
          }
        }
        patchState(store, EMPTY_STATE);
      },
      clearSession(): void {
        patchState(store, EMPTY_STATE);
      },
    };
  }),
);
