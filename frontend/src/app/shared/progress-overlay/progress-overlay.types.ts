export interface ProgressOverlayConfig {
  /** 標題，例如「批次列印中」 */
  title: string;
  /** 副標，例如報表名稱「報名資料卡」 */
  detail?: string;
  total: number;
  completed: number;
  /** 進度條下方的狀態字，例如「合併 PDF…」「下載中…」 */
  note?: string;
  /** 預設 true；下載階段會關掉，避免取消到一半的下載 */
  cancelable?: boolean;
  cancelLabel?: string;
}
