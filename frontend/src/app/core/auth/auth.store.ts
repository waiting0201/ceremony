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

const STORAGE_KEY = 'ceremony.auth.v1';

function loadFromStorage(): AuthState {
  if (typeof localStorage === 'undefined') {
    return { user: null, token: null };
  }
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (!raw) return { user: null, token: null };
    const parsed = JSON.parse(raw) as AuthState;
    if (parsed.user && parsed.token) return parsed;
  } catch {
    /* ignore corrupt storage */
  }
  return { user: null, token: null };
}

function saveToStorage(state: AuthState): void {
  if (typeof localStorage === 'undefined') return;
  if (state.user && state.token) {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(state));
  } else {
    localStorage.removeItem(STORAGE_KEY);
  }
}

export const AuthStore = signalStore(
  { providedIn: 'root' },
  withState<AuthState>(loadFromStorage()),
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
        const next: AuthState = { user: result.user, token: result.token };
        patchState(store, next);
        saveToStorage(next);
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
        const next: AuthState = { user: null, token: null };
        patchState(store, next);
        saveToStorage(next);
      },
      clearSession(): void {
        const next: AuthState = { user: null, token: null };
        patchState(store, next);
        saveToStorage(next);
      },
    };
  }),
);
