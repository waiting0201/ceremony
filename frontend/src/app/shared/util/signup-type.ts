export interface SignupTypeOption {
  value: number;
  label: string;
  numberTitle: string;
}

export const SIGNUP_TYPES: readonly SignupTypeOption[] = [
  { value: 1, label: '一般報名', numberTitle: 'No' },
  { value: 2, label: '寺方', numberTitle: '寺' },
  { value: 3, label: '觀音會', numberTitle: '觀' },
  { value: 4, label: '普桌', numberTitle: '普' },
  { value: 5, label: '郵寄', numberTitle: '郵' },
];

export function signupTypeLabel(value: number | null | undefined): string {
  if (value == null) return '';
  return SIGNUP_TYPES.find((t) => t.value === value)?.label ?? `類型${value}`;
}
