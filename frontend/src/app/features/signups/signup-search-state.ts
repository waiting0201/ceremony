import { Injectable, signal } from '@angular/core';
import type { SignupListItem } from '../../core/api/signups/signup.models';

export interface SignupSearchFormSnapshot {
  /** 「全部」模式：忽略以下所有條件顯示全部報名（條件值仍保留於本快照，取消勾選即還原）。 */
  isAll: boolean;
  year: number | null;
  isScope: boolean;
  ceremonyCategoryId: string;
  signupType: number;
  number: number | null;
  isFixedNumber: boolean;
  searchKey: string;
  scopeName: boolean;
  scopeLivingName: boolean;
  scopeDeadName: boolean;
  scopePhone: boolean;
  scopeRemark: boolean;
}

/**
 * 跨路由的搜尋狀態快取（singleton）。
 * 進入 /signups/:id/edit 或 /signups/:id/logs 再回 /signups 時，
 * 由本服務還原上次的搜尋條件與結果，避免重打 API。
 *
 * 若使用者在 edit 頁完成修改/新增/刪除，會 set markStale()，
 * 列表頁下次 mount 偵測到 stale = true 會自動重查。
 */
@Injectable({ providedIn: 'root' })
export class SignupSearchState {
  readonly form = signal<SignupSearchFormSnapshot | null>(null);
  readonly results = signal<SignupListItem[]>([]);
  readonly total = signal(0);
  readonly hasSearched = signal(false);
  readonly selectedIds = signal<ReadonlySet<string>>(new Set());
  readonly stale = signal(false);
  /** 進入「全部」模式前是否已搜尋過 → 取消勾選時決定要重查條件還是回到未搜尋的空狀態。 */
  readonly searchedBeforeAll = signal(false);

  markStale(): void {
    this.stale.set(true);
  }

  clearStale(): void {
    this.stale.set(false);
  }

  reset(): void {
    this.form.set(null);
    this.results.set([]);
    this.total.set(0);
    this.hasSearched.set(false);
    this.selectedIds.set(new Set());
    this.stale.set(false);
    this.searchedBeforeAll.set(false);
  }
}
