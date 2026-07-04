export interface PrepayGroupOption {
  code: number;
  label: string;
}

// 標籤文字對齊舊 LoadPrepayForm.LoadBeliever（詞序照舊系統，勿改成「非員工一般」等新詞序）。
export const PREPAY_GROUPS: readonly PrepayGroupOption[] = [
  { code: 1, label: '一般非員工' },
  { code: 2, label: '一般地藏殿員工' },
  { code: 3, label: '寺方' },
  { code: 4, label: '觀音會' },
  { code: 5, label: '郵撥大殿員工' },
  { code: 6, label: '郵撥非員工' },
];
