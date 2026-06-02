export interface ConfirmDialogConfig {
  title: string;
  message: string;
  confirmLabel?: string;
  cancelLabel?: string;
  danger?: boolean;
  /** 隱藏取消鈕：純訊息 / 結果提示用（單一確認鈕）。 */
  hideCancel?: boolean;
}
