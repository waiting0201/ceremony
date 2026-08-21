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
  /**
   * 數字輸入格（2026-08-21，用於報名列表右鍵「移動插入至…」）：在訊息下方多一格 number input，
   * 確認鈕在值非正整數時 disabled。給 `ConfirmDialogService.askNumber()` 用。
   *
   * 走擴充而非另做一顆 prompt dialog：全站對話框的樣式與字級（`emphasis`、`--font-size-md`）
   * 已在這支收斂，另起一套等於馬上分岔。
   */
  numberInput?: {
    label: string;
    initial?: number | null;
    /** 下限，預設 1。 */
    min?: number;
    /** 輸入格下方的小字說明。 */
    hint?: string;
  };
}
