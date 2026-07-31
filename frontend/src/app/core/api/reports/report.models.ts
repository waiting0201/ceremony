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
  /**
   * X-Report-Page-Size 原字串（微米，如 "210000x148000"）。權威值在後端 ReportPageSizes；
   * 送印時原樣交給主行程，沒帶就會靜默退化成 electron/paper.ts 的 fallback 表。
   */
  pageSizeHeader?: string;
}

/**
 * POST /reports/batch/plan 的回應：這批要印哪些報名，但還沒渲染。
 * 大量列印靠它切段——選取的權威在後端，前端只負責切，不重查一次。
 */
export interface BatchReportPlan {
  reportType: SingleReportType;
  fileName: string;
  total: number;
  items: BatchReportPlanItem[];
}

export interface BatchReportPlanItem {
  id: string;
  /** 報名編號，可能為 null（尚未配號）；用來顯示「第 7 段：編號 1201–1400」 */
  number: number | null;
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
