export type SingleReportType = 'datacard' | 'receipt' | 'tablet' | 'text' | 'worship';

export interface BatchReportRequest {
  reportType: SingleReportType;
  numberStart: number;
  numberEnd: number;
  year?: number | null;
  yearGte?: boolean;
  ceremonyCategoryId?: string | null;
  signupType?: number | null;
}

export interface ReportPdf {
  blob: Blob;
  fileName: string;
  signupCount?: number;
}
