import { Pipe, type PipeTransform } from '@angular/core';

const SIGNUP_TYPE_MAP: Record<number, string> = {
  1: '一般',
  2: '預繳',
  3: '特殊',
};

@Pipe({ name: 'signupType', standalone: true })
export class SignupTypePipe implements PipeTransform {
  transform(value: number | null | undefined): string {
    if (value == null) return '';
    return SIGNUP_TYPE_MAP[value] ?? `類型${value}`;
  }
}
