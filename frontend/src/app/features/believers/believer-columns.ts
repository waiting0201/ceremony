import type { BelieverListItem } from '../../core/api/believers/believer.models';

// 欄位定義 1:1 對齊舊 BelieverForm.Designer.cs dgvBelievers 的可見欄位
// （HeaderText / Width / 顯示順序皆抽自 reference/old/Ceremony/BelieverForm.Designer.cs）。
// 隱藏欄（BelieverID / EmployeeType / MailZipcodeID / TextZipcodeID）不列入。
export type BelieverColumnId =
  | 'employeeTypeTitle'
  | 'hallName'
  | 'name'
  | 'phone'
  | 'mailCity' | 'mailZone' | 'mailAddress'
  | 'textCity' | 'textZone' | 'textAddress'
  | 'dead1' | 'dead2' | 'dead3' | 'dead4' | 'dead5' | 'dead6'
  | 'living1' | 'living2' | 'living3' | 'living4' | 'living5' | 'living6'
  | 'ops';

export interface BelieverColumnDef {
  id: BelieverColumnId;
  label: string;
  width: number;
  cellClass?: 'dead';
  /** 操作欄（⋮ kebab），由 template 特別渲染 */
  ops?: boolean;
  accessor?: (item: BelieverListItem) => string;
}

export const BELIEVER_COLUMNS: BelieverColumnDef[] = [
  { id: 'employeeTypeTitle', label: '員工', width: 51, accessor: (i) => i.employeeTypeTitle ?? '' },
  { id: 'hallName', label: '堂號', width: 51, accessor: (i) => i.hallName ?? '' },
  { id: 'name', label: '姓名', width: 80, accessor: (i) => i.name ?? '' },
  { id: 'phone', label: '聯絡電話', width: 83, accessor: (i) => i.phone ?? '' },
  { id: 'mailCity', label: '寄件城市', width: 83, accessor: (i) => i.mailCity ?? '' },
  { id: 'mailZone', label: '寄件區域', width: 83, accessor: (i) => i.mailArea ?? '' },
  { id: 'mailAddress', label: '寄件地址', width: 120, accessor: (i) => i.mailAddress ?? '' },
  { id: 'textCity', label: '文牒城市', width: 83, accessor: (i) => i.textCity ?? '' },
  { id: 'textZone', label: '文牒區域', width: 83, accessor: (i) => i.textArea ?? '' },
  { id: 'textAddress', label: '文牒地址', width: 120, accessor: (i) => i.textAddress ?? '' },
  { id: 'dead1', label: '往生1', width: 60, cellClass: 'dead', accessor: (i) => i.deadNames[0] ?? '' },
  { id: 'dead2', label: '往生2', width: 60, cellClass: 'dead', accessor: (i) => i.deadNames[1] ?? '' },
  { id: 'dead3', label: '往生3', width: 60, cellClass: 'dead', accessor: (i) => i.deadNames[2] ?? '' },
  { id: 'dead4', label: '往生3-1', width: 76, cellClass: 'dead', accessor: (i) => i.deadNames[3] ?? '' },
  { id: 'dead5', label: '往生5', width: 60, cellClass: 'dead', accessor: (i) => i.deadNames[4] ?? '' },
  { id: 'dead6', label: '往生6', width: 60, cellClass: 'dead', accessor: (i) => i.deadNames[5] ?? '' },
  { id: 'living1', label: '陽上1', width: 60, accessor: (i) => i.livingNames[0] ?? '' },
  { id: 'living2', label: '陽上2', width: 60, accessor: (i) => i.livingNames[1] ?? '' },
  { id: 'living3', label: '陽上3', width: 60, accessor: (i) => i.livingNames[2] ?? '' },
  { id: 'living4', label: '陽上3-1', width: 76, accessor: (i) => i.livingNames[3] ?? '' },
  { id: 'living5', label: '陽上5', width: 60, accessor: (i) => i.livingNames[4] ?? '' },
  { id: 'living6', label: '陽上6', width: 60, accessor: (i) => i.livingNames[5] ?? '' },
  { id: 'ops', label: '', width: 44, ops: true },
];
