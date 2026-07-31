/**
 * 分段列印的狀態模型。
 *
 * 為什麼要分段：實測 799 筆 datacard = 107 MB，19018 筆會爆 PdfSharp 的 2 GB MemoryStream；
 * 而且一個幾千頁的 spooler job 中途卡紙就得整批重印。切成固定大小的段之後，峰值記憶體
 * 與總筆數無關，卡紙也只要重印那一段。背景見 docs/blueprints/chunked-batch-printing.md
 */

export type SegmentStatus =
  | 'pending' // 還沒輪到
  | 'rendering' // 後端 job 正在渲染
  | 'printing' // 已交給主行程送印，等 spooler 收下
  | 'printed' // spooler 已收下（**不代表紙上有字**，所以永遠保留重印）
  | 'failed'
  | 'canceled';

export interface PrintSegment {
  /** 1-based，顯示用 */
  readonly index: number;
  readonly signupIds: string[];
  /** 這段的編號範圍（都沒配號時為 null）——卡紙時使用者要能把段對上手裡那疊紙 */
  readonly numberFrom: number | null;
  readonly numberTo: number | null;
  status: SegmentStatus;
  /** rendering 階段的即時進度（0..count） */
  rendered: number;
  errorMessage?: string;
}

export type ChunkedPrintPhase =
  | 'running'
  | 'paused'
  | 'done' // 所有段都跑完（可能含 failed），等使用者確認紙張後關閉
  | 'canceled';

export interface ChunkedPrintState {
  phase: ChunkedPrintPhase;
  reportLabel: string;
  total: number;
  segments: PrintSegment[];
  /** 全域錯誤（解析失敗等）；單段的錯誤放在 segment.errorMessage */
  errorMessage: string | null;
}

/** 已送印的筆數（＝真正推進度的量，不是「已渲染」）。 */
export function printedCount(segments: readonly PrintSegment[]): number {
  return segments.reduce((n, s) => (s.status === 'printed' ? n + s.signupIds.length : n), 0);
}

export function isSettled(phase: ChunkedPrintPhase): boolean {
  return phase === 'done' || phase === 'canceled';
}

/** 段標題，如「第 7 段：編號 1201–1400」；沒配號時退回筆數。 */
export function segmentLabel(seg: PrintSegment): string {
  const n = seg.signupIds.length;
  if (seg.numberFrom === null || seg.numberTo === null) return `第 ${seg.index} 段：${n} 筆`;
  if (seg.numberFrom === seg.numberTo) return `第 ${seg.index} 段：編號 ${seg.numberFrom}`;
  return `第 ${seg.index} 段：編號 ${seg.numberFrom}–${seg.numberTo}`;
}
