export type SingleReportType = 'datacard' | 'receipt' | 'tablet' | 'text' | 'worship' | 'worshipcard';

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

/** POST /reports/batch/jobs 的回應：job 已建立，總筆數在此就是確定值。 */
export interface BatchJobCreated {
  jobId: string;
  total: number;
  fileName: string;
  reportType: SingleReportType;
}

export type BatchJobStatus = 'running' | 'completed' | 'failed' | 'canceled';

/** GET /reports/batch/jobs/{id} 的回應。 */
export interface BatchJobState {
  jobId: string;
  status: BatchJobStatus;
  total: number;
  completed: number;
  fileName: string;
  errorCode?: string;
  message?: string;
}
