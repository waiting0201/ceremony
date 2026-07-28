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
  /** 剛新增那筆的 id（＝「列印資料卡」鈕的啟用狀態）；沒存過檔時為 null。 */
  lastCreatedSignupId: string | null;
  /** 離開當下表單是否為髒（回來後原樣還原，讓 host 的「未儲存的變更」旗標一致）。 */
  dirty: boolean;
}

/**
 * 「新增報名」未完成內容的跨路由草稿（singleton，比照 [SignupSearchState]）。
 *
 * 由來（2026-07-27 客訴）：新增報名是 overlay（backdrop 蓋滿全螢幕），要切到其他功能頁
 * 一定得先關掉 overlay → 表單元件被銷毀 → 填到一半的資料全沒。使用者要求切走再回來時資料還在。
 *
 * 範圍與生命週期：
 * - **一律快照，離開前畫面長怎樣、回來就長怎樣**（2026-07-28 使用者定案，取代 07-27 的
 *   「只有髒表單才存、儲存成功/按取消就清掉」）：離開時無條件把當下畫面存起來，包含存檔成功後
 *   仍留在畫面上的那筆、以及按「取消」後保留的法會資料＋費用。原本那兩個清除時機造成
 *   「畫面上看得到、切走再回來卻不見」，與使用者要的「回來要跟離開前一樣」相牴觸。
 * - **只保留在記憶體**：等同舊 WinForm「Form 還開著」的行為；系統關閉重開後草稿消失，
 *   避免隔天開機跑出前一天的舊資料。這是唯一的清除時機。
 * - **只有純新增模式做草稿**：編輯既有報名（signupId）、代入新增（fromSignupId）、
 *   插入報名（insertAt）都有各自的資料來源，還原草稿只會互相打架。
 * - **靜默還原**：重開新增報名時直接把欄位填回去，不顯示提示列。
 * - ⚠ 取捨：存檔成功那筆會被一起帶回來，且「確認」鈕仍可按 → 重按會再新增一筆。這與
 *   「存完不關閉、資料留著」既有的風險同一個（舊系統亦然），要開乾淨的下一筆請按「取消」。
 */
@Injectable({ providedIn: 'root' })
export class SignupDraftState {
  readonly draft = signal<SignupDraft | null>(null);

  save(draft: SignupDraft): void {
    this.draft.set(draft);
  }
}
