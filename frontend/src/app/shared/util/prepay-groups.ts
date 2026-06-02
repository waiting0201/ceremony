export interface PrepayGroupOption {
  code: number;
  label: string;
}

export const PREPAY_GROUPS: readonly PrepayGroupOption[] = [
  { code: 1, label: '非員工一般' },
  { code: 2, label: '地藏殿員工一般' },
  { code: 3, label: '寺方' },
  { code: 4, label: '觀音會' },
  { code: 5, label: '大殿員工郵撥' },
  { code: 6, label: '非員工郵撥' },
];
