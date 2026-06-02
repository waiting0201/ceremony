import type { SignupListItem } from '../../core/api/signups/signup.models';
import { formatAvoidFour } from '../../shared/util/avoid-four';

export type SignupColumnId =
  | 'check'
  | 'year'
  | 'ceremonyTitle'
  | 'numberTitle'
  | 'number'
  | 'fee'
  | 'employee'
  | 'name'
  | 'remark'
  | 'hallName'
  | 'dead1' | 'dead2' | 'dead3' | 'dead4' | 'dead5' | 'dead6'
  | 'living1' | 'living2' | 'living3' | 'living4' | 'living5' | 'living6'
  | 'prepayYear'
  | 'prepayCeremonyTitle'
  | 'phone'
  | 'mailCity' | 'mailZone' | 'mailAddress'
  | 'textCity' | 'textZone' | 'textAddress'
  | 'adminName'
  | 'createDate';

export interface SignupColumnDef {
  id: SignupColumnId;
  label: string;
  width: number;
  toggleOnly?: boolean;
  cellClass?: 'dead' | 'meta-date' | 'remark';
  accessor?: (item: SignupListItem) => string;
  resizable?: boolean;
}

const formatDate = (s: string | null): string => {
  if (!s) return '';
  return s.length >= 16 ? s.slice(0, 16).replace('T', ' ') : s;
};

export const SIGNUP_COLUMNS: SignupColumnDef[] = [
  { id: 'check', label: '', width: 28 },
  { id: 'year', label: '年份', width: 56, accessor: (i) => String(i.year), resizable: true },
  { id: 'ceremonyTitle', label: '法會', width: 100, accessor: (i) => i.ceremonyTitle ?? '', resizable: true },
  { id: 'numberTitle', label: '類型', width: 50, accessor: (i) => i.numberTitle ?? '', resizable: true },
  { id: 'number', label: '編號', width: 64, accessor: (i) => formatAvoidFour(i.number), resizable: true },
  { id: 'fee', label: '費用', width: 60, toggleOnly: true, accessor: (i) => i.fee == null ? '' : String(i.fee), resizable: true },
  { id: 'employee', label: '員工', width: 56, toggleOnly: true, accessor: (i) => i.employee ?? '', resizable: true },
  { id: 'name', label: '姓名', width: 80, accessor: (i) => i.name ?? '', resizable: true },
  { id: 'remark', label: '備註', width: 140, cellClass: 'remark', accessor: (i) => i.remark ?? '', resizable: true },
  { id: 'hallName', label: '堂號', width: 60, toggleOnly: true, accessor: (i) => i.hallName ?? '', resizable: true },
  { id: 'dead1', label: '往生1', width: 70, cellClass: 'dead', accessor: (i) => i.deadNames[0] ?? '', resizable: true },
  { id: 'dead2', label: '往生2', width: 70, cellClass: 'dead', accessor: (i) => i.deadNames[1] ?? '', resizable: true },
  { id: 'dead3', label: '往生3', width: 70, cellClass: 'dead', accessor: (i) => i.deadNames[2] ?? '', resizable: true },
  { id: 'dead4', label: '往生3-1', width: 76, cellClass: 'dead', accessor: (i) => i.deadNames[3] ?? '', resizable: true },
  { id: 'dead5', label: '往生5', width: 70, cellClass: 'dead', accessor: (i) => i.deadNames[4] ?? '', resizable: true },
  { id: 'dead6', label: '往生6', width: 70, toggleOnly: true, cellClass: 'dead', accessor: (i) => i.deadNames[5] ?? '', resizable: true },
  { id: 'living1', label: '陽上1', width: 70, accessor: (i) => i.livingNames[0] ?? '', resizable: true },
  { id: 'living2', label: '陽上2', width: 70, accessor: (i) => i.livingNames[1] ?? '', resizable: true },
  { id: 'living3', label: '陽上3', width: 70, accessor: (i) => i.livingNames[2] ?? '', resizable: true },
  { id: 'living4', label: '陽上3-1', width: 76, accessor: (i) => i.livingNames[3] ?? '', resizable: true },
  { id: 'living5', label: '陽上5', width: 70, accessor: (i) => i.livingNames[4] ?? '', resizable: true },
  { id: 'living6', label: '陽上6', width: 70, toggleOnly: true, accessor: (i) => i.livingNames[5] ?? '', resizable: true },
  { id: 'prepayYear', label: '預繳年份', width: 78, accessor: (i) => i.prepayYear == null ? '' : String(i.prepayYear), resizable: true },
  { id: 'prepayCeremonyTitle', label: '預繳法會', width: 100, accessor: (i) => i.prepayCeremonyTitle ?? '', resizable: true },
  { id: 'phone', label: '聯絡電話', width: 110, accessor: (i) => i.phone ?? '', resizable: true },
  { id: 'mailCity', label: '寄件城市', width: 80, accessor: (i) => i.mailCity ?? '', resizable: true },
  { id: 'mailZone', label: '寄件區域', width: 80, accessor: (i) => i.mailZone ?? '', resizable: true },
  { id: 'mailAddress', label: '寄件地址', width: 180, accessor: (i) => i.mailAddress ?? '', resizable: true },
  { id: 'textCity', label: '文牒城市', width: 80, accessor: (i) => i.textCity ?? '', resizable: true },
  { id: 'textZone', label: '文牒區域', width: 80, accessor: (i) => i.textZone ?? '', resizable: true },
  { id: 'textAddress', label: '文牒地址', width: 180, accessor: (i) => i.textAddress ?? '', resizable: true },
  { id: 'adminName', label: '編輯者', width: 80, accessor: (i) => i.adminName ?? '', resizable: true },
  { id: 'createDate', label: '編輯日期', width: 130, cellClass: 'meta-date', accessor: (i) => formatDate(i.createDate), resizable: true },
];

export const SIGNUP_COL_MIN_WIDTH = 32;
export const SIGNUP_COL_MAX_WIDTH = 600;
