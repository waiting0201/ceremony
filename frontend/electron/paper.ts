// 報表紙張尺寸（微米）與 X-Report-Page-Size header 解析。
//
// 權威值在後端 Ceremony.Domain.Reports.ReportPageSizes，透過 X-Report-Page-Size response header
// 帶到這裡；下表只是 header 缺失時的 fallback（舊版 sidecar / 未知 report type），命中 fallback 會 log 警告。
//
// 刻意不 import 'electron'：這樣本檔是純函式，可被單元測試直接載入。
// 契約見 docs/blueprints/print-channel-electron.md。

export interface PageSizeMicrons {
  width: number;
  height: number;
}

export type ReportType = 'datacard' | 'receipt' | 'tablet' | 'text' | 'worship' | 'worshipcard';

/** fallback 表：必須與後端 ReportPageSizes 保持一致（1cm = 10000µm）。 */
export const REPORT_PAGE_MICRONS: Record<ReportType, PageSizeMicrons> = {
  datacard: { width: 210000, height: 148000 }, // A5 橫 21×14.8cm
  receipt: { width: 210000, height: 297000 }, // A4 直 21×29.7cm（雙聯 2 頁）
  tablet: { width: 115000, height: 255000 }, // 薦牌窄長 11.5×25.5cm
  text: { width: 365000, height: 262000 }, // 文牒超寬 36.5×26.2cm
  worship: { width: 210000, height: 296000 }, // 普桌 21×29.6cm
  worshipcard: { width: 210000, height: 148000 }, // 普桌資料卡 A5 橫
};

/**
 * 解析 X-Report-Page-Size（如 "210000x148000"，單位微米）。
 * 格式不符 / 非正整數 / 超出合理範圍（1cm～100cm）一律回 null 讓呼叫端退回 fallback 表——
 * 寧可用已知的舊值，也不要把垃圾值送進印表機驅動。
 */
export function parsePageSizeHeader(raw: string | null | undefined): PageSizeMicrons | null {
  if (!raw) return null;
  const m = /^(\d+)x(\d+)$/i.exec(raw.trim());
  if (!m) return null;
  const width = Number(m[1]);
  const height = Number(m[2]);
  const sane = (v: number) => Number.isInteger(v) && v >= 10_000 && v <= 1_000_000;
  return sane(width) && sane(height) ? { width, height } : null;
}

/** header 優先、fallback 表次之；兩者都沒有就回 null（送印時不指定 pageSize，讓驅動用預設）。 */
export function resolvePageSize(
  reportType: string,
  header: string | null | undefined,
): { size: PageSizeMicrons | null; source: 'header' | 'fallback' | 'none' } {
  const fromHeader = parsePageSizeHeader(header);
  if (fromHeader) return { size: fromHeader, source: 'header' };

  const fallback = REPORT_PAGE_MICRONS[reportType as ReportType];
  if (fallback) return { size: fallback, source: 'fallback' };

  return { size: null, source: 'none' };
}

/** 給 UI 顯示用，如「21 × 14.8 cm」。 */
export function formatPageSizeCm(size: PageSizeMicrons | null): string {
  if (!size) return '印表機預設';
  const cm = (v: number) => String(Math.round(v / 100) / 100);
  return `${cm(size.width)} × ${cm(size.height)} cm`;
}
