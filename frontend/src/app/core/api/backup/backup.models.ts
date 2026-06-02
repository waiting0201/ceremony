/** 觸發 DB 備份的請求；customFileName 省略時後端用時戳檔名。 */
export interface BackupRequest {
  customFileName?: string | null;
  /** 備份後清除（截斷 + 收縮）SQL Server 交易紀錄檔。 */
  clearLog?: boolean;
}

/** 備份結果（對齊後端 BackupResponse）。 */
export interface BackupResult {
  fileName: string;
  fullPath: string;
  sizeBytes: number;
  startedAt: string;
  completedAt: string;
  /** 是否已清除交易紀錄檔。 */
  logCleared: boolean;
  /** FULL/BULK_LOGGED 模式下產生的 log backup 檔名（.trn）；SIMPLE 為 null。 */
  logBackupFileName?: string | null;
  /** 清 log 失敗原因（成功為 null）；備份本身仍成功。 */
  logClearError?: string | null;
}
