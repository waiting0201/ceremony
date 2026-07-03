export type SingleReportType = 'datacard' | 'receipt' | 'tablet' | 'text' | 'worship';

export interface BatchReportRequest {
  reportType: SingleReportType;
  numberStart?: number | null;
  numberEnd?: number | null;
  year?: number | null;
  yearGte?: boolean;
  ceremonyCategoryId?: string | null;
  signupType?: number | null;
  /** 勾選任意幾筆（不論編號是否連續）只印這幾筆；有給值時優先於 numberStart/numberEnd。 */
  signupIds?: string[] | null;
}

export interface ReportPdf {
  blob: Blob;
  fileName: string;
  signupCount?: number;
}
