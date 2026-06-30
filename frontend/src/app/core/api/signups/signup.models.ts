export interface SignupListItem {
  id: string;
  year: number;
  ceremonyCategoryId: string;
  ceremonyTitle: string | null;
  signupType: number;
  numberTitle: string | null;
  number: number | null;
  fee: number | null;
  employee: string | null;
  believerId: string | null;
  name: string | null;
  hallName: string | null;
  phone: string | null;
  isFixedNumber: boolean;
  livingNames: (string | null)[];
  deadNames: (string | null)[];
  mailCity: string | null;
  mailZone: string | null;
  mailZipcode: string | null;
  mailAddress: string | null;
  textCity: string | null;
  textZone: string | null;
  textZipcode: string | null;
  textAddress: string | null;
  prepayYear: number | null;
  prepayCeremonyCategoryId: string | null;
  prepayCeremonyTitle: string | null;
  remark: string | null;
  adminName: string | null;
  createDate: string | null;
}

export interface SignupListResponse {
  items: SignupListItem[];
  total: number;
}

export interface SignupSearchQuery {
  year?: number | null;
  isScope?: boolean;
  ceremonyCategoryId?: string | null;
  signupType?: number | null;
  number?: number | null;
  searchKey?: string | null;
  scopeName?: boolean;
  scopeLivingName?: boolean;
  scopeDeadName?: boolean;
  scopePhone?: boolean;
  scopeRemark?: boolean;
  isFixedNumber?: boolean;
}

export interface CreateSignupRequest {
  year: number;
  ceremonyCategoryId: string;
  signupType: number;
  believerId: string;
  name: string;
  mailAddress: string;
  keepNumber?: boolean;
  customNumber?: number | null;
  fee?: number | null;
  phone?: string | null;
  hallName?: string | null;
  mailZipcodeId?: number | null;
  textZipcodeId?: number | null;
  textAddress?: string | null;
  livingNames?: (string | null)[];
  deadNames?: (string | null)[];
  remark?: string | null;
  prepayYear?: number | null;
  prepayCeremonyCategoryId?: string | null;
}

/** 重複報名警示項：某信眾在同一 (year, ceremonyCategoryId) 既有的報名（忽略 signupType）。 */
export interface SignupDuplicateItem {
  signupId: string;
  signupType: number;
  numberTitle: string | null;
  number: number | null;
  name: string | null;
}

export interface SignupDuplicateListResponse {
  items: SignupDuplicateItem[];
  total: number;
}

export interface SignupLogItem {
  id: string;
  signupId: string;
  year: number;
  ceremonyCategoryTitle: string | null;
  signupType: number;
  numberTitle: string | null;
  number: number | null;
  hallName: string | null;
  name: string | null;
  phone: string | null;
  fee: number | null;
  livingNames: (string | null)[];
  deadNames: (string | null)[];
  mailCity: string | null;
  mailZone: string | null;
  mailAddress: string | null;
  textCity: string | null;
  textZone: string | null;
  textAddress: string | null;
  remark: string | null;
  prepayYear: number | null;
  prepayCeremonyCategoryTitle: string | null;
  admin: string | null;
  createDate: string | null;
}

export interface SignupLogListResponse {
  items: SignupLogItem[];
  total: number;
}
