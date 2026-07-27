import { Injectable, signal } from '@angular/core';
import type { BelieverListItem } from '../../core/api/believers/believer.models';
import type { SignupListItem } from '../../core/api/signups/signup.models';

/** 新增報名表單的原始值快照（＝ SignupEditFormComponent.form.getRawValue() 的結構）。 */
export interface SignupDraftValue {
  year: number;
  ceremonyCategoryId: string;
  signupType: number;
  believerId: string;
  name: string;
  phone: string;
  employeeType: number;
  isFixedNumber: boolean;
  hallName: string;
  mailCity: string;
  mailZipcodeId: string;
  mailAddress: string;
  sameMailAddress: boolean;
  textCity: string;
  textZipcodeId: string;
  textAddress: string;
  livingNames: (string | null)[];
  deadNames: (string | null)[];
  keepNumber: boolean;
  customNumber: number | null;
  fee: number | null;
  remark: string;
  prepayYear: number | null;
  prepayCeremonyCategoryId: string;
}

/** 一份未完成的新增報名（表單值 + 信眾搜尋/選取的畫面狀態）。 */
export interface SignupDraft {
  value: SignupDraftValue;
  /** 已選信眾摘要卡的資料源；未選信眾時為 null。 */
  selectedBeliever: BelieverListItem | null;
  /** 常駐搜尋結果中高亮的那一列 id。 */
  pickedRowId: string | null;
  believerSearchTerm: string;
  believerSearchResults: SignupListItem[];
  believerHasSearched: boolean;
}

/**
 * 「新增報名」未完成內容的跨路由草稿（singleton，比照 [SignupSearchState]）。
 *
 * 由來（2026-07-27 客訴）：新增報名是 overlay（backdrop 蓋滿全螢幕），要切到其他功能頁
 * 一定得先關掉 overlay → 表單元件被銷毀 → 填到一半的資料全沒。使用者要求切走再回來時資料還在。
 *
 * 範圍與生命週期（2026-07-27 使用者定案）：
 * - **只保留在記憶體**：等同舊 WinForm「Form 還開著」的行為；系統關閉重開後草稿消失，
 *   避免隔天開機跑出前一天的舊資料。
 * - **只有純新增模式做草稿**：編輯既有報名（signupId）、代入新增（fromSignupId）、
 *   插入報名（insertAt）都有各自的資料來源，還原草稿只會互相打架。
 * - **靜默還原**：重開新增報名時直接把欄位填回去，不顯示提示列。
 * - 清除時機：儲存成功、或使用者按「取消」（＝清成新的一筆）。
 */
@Injectable({ providedIn: 'root' })
export class SignupDraftState {
  readonly draft = signal<SignupDraft | null>(null);

  save(draft: SignupDraft): void {
    this.draft.set(draft);
  }

  clear(): void {
    this.draft.set(null);
  }
}
