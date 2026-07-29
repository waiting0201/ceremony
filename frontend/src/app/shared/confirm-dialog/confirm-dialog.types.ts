export interface ConfirmDialogConfig {
  title: string;
  message: string;
  confirmLabel?: string;
  cancelLabel?: string;
  danger?: boolean;
  /** 隱藏取消鈕：純訊息 / 結果提示用（單一確認鈕）。 */
  hideCancel?: boolean;
  /**
   * 強調樣式：訊息 20px + 確認鈕加寬一倍（2026-07-29 使用者指定，用於「新增報名成功」）。
   *
   * 只給「成敗結果」這種要一眼看到的提示；一般確認對話框維持預設字級，避免全站對話框一起變大。
   */
  emphasis?: boolean;
}
