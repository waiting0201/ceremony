import { Pipe, type PipeTransform } from '@angular/core';

@Pipe({ name: 'taiwanDate', standalone: true })
export class TaiwanDatePipe implements PipeTransform {
  transform(value: string | Date | null | undefined, format: 'full' | 'short' = 'full'): string {
    if (!value) return '';
    const d = typeof value === 'string' ? new Date(value) : value;
    if (isNaN(d.getTime())) return '';

    const year = d.getFullYear() - 1911;
    const month = String(d.getMonth() + 1).padStart(2, '0');
    const day = String(d.getDate()).padStart(2, '0');

    return format === 'short' ? `${year}/${month}/${day}` : `民國 ${year} 年 ${month} 月 ${day} 日`;
  }
}
