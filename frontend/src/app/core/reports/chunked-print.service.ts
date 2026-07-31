import { inject, Injectable, signal, type WritableSignal } from '@angular/core';
import { ReportApi } from '../api/reports/report.api';
import type {
  BatchReportPlanItem,
  BatchReportRequest,
  SingleReportType,
} from '../api/reports/report.models';
import { ApiError } from '../http/api-error';
import { toMessage, UserFacingError } from '../errors/to-message';
import type { ChunkedPrintState, PrintSegment, SegmentStatus } from './chunked-print.types';

/**
 * 每段的筆數。與 PrintService 的預覽門檻同值（約 27 MB／段）：段夠小才能預覽，
 * 也讓峰值記憶體與總筆數無關。另有一個硬理由——Dapper 的 `WHERE SignupID IN @Ids`
 * 會展開成 N 個參數，SQL Server 上限 2100，所以段大小本來就不能太大。
 */
export const SEGMENT_SIZE = 200;

/** 輪詢間隔，與 BatchPrintService 一致（localhost 回圈，成本可忽略）。 */
const POLL_INTERVAL_MS = 250;
/** 單段渲染的保險絲。一段只有 200 筆（實測約 1.7s），2 分鐘還沒好就是出事了。 */
const SEGMENT_TIMEOUT_MS = 2 * 60_000;

/**
 * 送印一段所需的能力，由 PrintService 注入——避免 core/reports 反向相依 core/print。
 * @returns false = 使用者在系統層取消（該段標記略過，不是失敗）；丟例外 = 真的失敗。
 */
export interface SegmentPrinter {
  (type: SingleReportType, blob: Blob, pageSizeHeader?: string): Promise<boolean>;
}

/**
 * 分段列印的執行控制。UI（batch-print-panel）綁 `state`，按鈕呼叫這些方法。
 */
export interface ChunkedPrintRun {
  readonly state: WritableSignal<ChunkedPrintState>;
  pause(): void;
  resume(): void;
  /** 停止送出後續段落；已交給 spooler 的段停不了（那是印表機的事）。 */
  cancel(): void;
  /** 重印單一段落。只在非 running 時可用（避免與主迴圈搶印表機）。 */
  reprint(index: number): Promise<void>;
}

/**
 * 大量列印：把一批報名切成固定大小的段，逐段渲染並送印。
 *
 * 為什麼不做成單一大 PDF：實測 799 筆 = 107 MB、19018 筆直接爆 PdfSharp 的 2 GB MemoryStream；
 * 而且幾千頁的單一 spooler job 中途卡紙就得整批重印——那才是現場真正的痛點。
 *
 * 為什麼分段在前端而不是後端：暫停、重印、逐段預覽都需要逐段控制，後端做分段仍得把段的
 * 概念傳到前端。後端只多一個 `POST /reports/batch/plan`（選取權威仍在 ResolveAsync）。
 *
 * 為什麼不重用 BatchPrintService：那支一定會開 ProgressOverlay，而這裡的進度介面是分段面板；
 * 共通的只有「建 job → 輪詢 → 取檔」二十行，硬抽共用反而要在兩種進度模型間加抽象層。
 */
@Injectable({ providedIn: 'root' })
export class ChunkedPrintService {
  private readonly api = inject(ReportApi);

  /**
   * 建立一次分段列印。回傳的 run 由呼叫端負責接到 UI 上。
   *
   * `items` 由呼叫端先向 `batch/plan` 取得——呼叫端本來就要靠 total 決定要不要分段，
   * 這裡再查一次只會多打一次 DB，還可能因為期間資料變動而與已顯示的筆數不一致。
   *
   * 第一段刻意在 `beforeFirstPrint` 拿到 blob 時才問印表機設定：使用者先看到第一段的預覽
   * 再決定印表機與份數，之後所有段沿用同一組設定（不會每段都跳一次對話框）。
   *
   * @param beforeFirstPrint 回 false 代表使用者取消，整批不印。
   */
  start(
    req: BatchReportRequest,
    items: readonly BatchReportPlanItem[],
    reportLabel: string,
    print: SegmentPrinter,
    beforeFirstPrint: (firstSegmentPdf: Blob) => Promise<boolean>,
  ): ChunkedPrintRun {
    const state = signal<ChunkedPrintState>({
      phase: 'running',
      reportLabel,
      total: items.length,
      segments: buildSegments(items),
      errorMessage: null,
    });

    const run: ChunkedPrintRun = {
      state,
      pause: () => patch(state, (s) => (s.phase === 'running' ? { phase: 'paused' } : {})),
      resume: () => {
        if (state().phase !== 'paused') return;
        patch(state, () => ({ phase: 'running' }));
        void this.drive(state, req, print, beforeFirstPrint);
      },
      cancel: () => patch(state, () => ({ phase: 'canceled' })),
      reprint: (index) => this.reprintSegment(state, req, print, index),
    };

    void this.drive(state, req, print, beforeFirstPrint);
    return run;
  }

  /** 主迴圈：逐段渲染送印。可被 pause / cancel 中斷，resume 時再進來一次。 */
  private async drive(
    state: WritableSignal<ChunkedPrintState>,
    req: BatchReportRequest,
    print: SegmentPrinter,
    beforeFirstPrint: (pdf: Blob) => Promise<boolean>,
  ): Promise<void> {
    try {
      // resume 進來時第一段早就印過了，不能再問一次印表機設定
      let askedForSettings = state().segments.some((s) => s.status !== 'pending');

      for (;;) {
        const phase = state().phase;
        if (phase === 'canceled') return;
        if (phase === 'paused') return; // resume 會再叫一次 drive

        const next = state().segments.find((s) => s.status === 'pending');
        if (!next) {
          patch(state, () => ({ phase: 'done' }));
          return;
        }

        const pdf = await this.renderSegment(state, req, next.index);
        if (!pdf) continue; // 失敗或取消，狀態已寫進該段

        if (!askedForSettings) {
          askedForSettings = true;
          if (!(await beforeFirstPrint(pdf.blob))) {
            // 使用者在列印對話框按取消 → 整批不印
            patchSegment(state, next.index, () => ({ status: 'canceled' }));
            patch(state, () => ({ phase: 'canceled' }));
            return;
          }
        }

        await this.printSegment(state, print, req.reportType, next.index, pdf);
      }
    } catch (err) {
      patch(state, () => ({ phase: 'done', errorMessage: toMessage(err) }));
    }
  }

  /** 重印單一段：重新建 job 重新渲染（不快取，順帶確保資料是最新的）。 */
  private async reprintSegment(
    state: WritableSignal<ChunkedPrintState>,
    req: BatchReportRequest,
    print: SegmentPrinter,
    index: number,
  ): Promise<void> {
    if (state().phase === 'running') return; // 主迴圈正在用印表機
    patchSegment(state, index, () => ({ status: 'pending', rendered: 0, errorMessage: undefined }));

    const pdf = await this.renderSegment(state, req, index);
    if (!pdf) return;
    await this.printSegment(state, print, req.reportType, index, pdf);
  }

  /** 建 job → 輪詢 → 取檔。失敗時把訊息寫進該段並回 null（不中斷整批）。 */
  private async renderSegment(
    state: WritableSignal<ChunkedPrintState>,
    req: BatchReportRequest,
    index: number,
  ): Promise<{ blob: Blob; pageSizeHeader?: string } | null> {
    const seg = state().segments.find((s) => s.index === index);
    if (!seg) return null;

    patchSegment(state, index, () => ({ status: 'rendering', rendered: 0 }));

    try {
      // signupIds 優先於編號區間 → 這段就是這段，不受條件變動影響
      const job = await this.api.createBatchJob({ ...req, signupIds: seg.signupIds });
      const deadline = Date.now() + SEGMENT_TIMEOUT_MS;

      for (;;) {
        if (state().phase === 'canceled') {
          await this.cancelQuietly(job.jobId);
          patchSegment(state, index, () => ({ status: 'canceled' }));
          return null;
        }
        if (Date.now() > deadline) {
          await this.cancelQuietly(job.jobId);
          throw new UserFacingError('這一段渲染逾時，請重印此段');
        }

        const s = await this.api.getBatchJob(job.jobId);
        if (s.status === 'completed') {
          patchSegment(state, index, () => ({ rendered: s.total }));
          const pdf = await this.api.getBatchJobFile(job.jobId, s.fileName || job.fileName);
          return { blob: pdf.blob, pageSizeHeader: pdf.pageSizeHeader };
        }
        if (s.status === 'canceled') {
          patchSegment(state, index, () => ({ status: 'canceled' }));
          return null;
        }
        if (s.status === 'failed') {
          throw new ApiError(500, s.errorCode ?? 'INTERNAL_ERROR', s.message ?? '未預期的伺服器錯誤');
        }

        patchSegment(state, index, () => ({ rendered: s.completed }));
        await delay(POLL_INTERVAL_MS);
      }
    } catch (err) {
      patchSegment(state, index, () => ({ status: 'failed', errorMessage: toMessage(err) }));
      return null;
    }
  }

  /**
   * 送印一段。成功只代表 spooler 收下了——紙上有沒有字是之後的事，
   * 所以狀態叫 `printed` 而非 `completed`，且面板永遠保留重印鈕。
   */
  private async printSegment(
    state: WritableSignal<ChunkedPrintState>,
    print: SegmentPrinter,
    type: SingleReportType,
    index: number,
    pdf: { blob: Blob; pageSizeHeader?: string },
  ): Promise<void> {
    patchSegment(state, index, () => ({ status: 'printing' }));
    try {
      const sent = await print(type, pdf.blob, pdf.pageSizeHeader);
      patchSegment(state, index, () => ({ status: sent ? 'printed' : 'canceled' }));
    } catch (err) {
      patchSegment(state, index, () => ({ status: 'failed', errorMessage: toMessage(err) }));
    }
    // blob 用完即棄：同時只有一段在記憶體，峰值才與總筆數無關
  }

  private async cancelQuietly(jobId: string): Promise<void> {
    try {
      await this.api.cancelBatchJob(jobId);
    } catch {
      /* 使用者已經要走了，這裡再失敗也沒有補救動作 */
    }
  }
}

/** 依 SEGMENT_SIZE 切段。清單順序來自後端的 `ORDER BY Number`，所以編號範圍是連續的。 */
export function buildSegments(items: readonly BatchReportPlanItem[]): PrintSegment[] {
  const segments: PrintSegment[] = [];
  for (let i = 0; i < items.length; i += SEGMENT_SIZE) {
    const slice = items.slice(i, i + SEGMENT_SIZE);
    const numbers = slice.map((x) => x.number).filter((n): n is number => n !== null);
    segments.push({
      index: segments.length + 1,
      signupIds: slice.map((x) => x.id),
      numberFrom: numbers.length ? Math.min(...numbers) : null,
      numberTo: numbers.length ? Math.max(...numbers) : null,
      status: 'pending',
      rendered: 0,
    });
  }
  return segments;
}

function patch(
  state: WritableSignal<ChunkedPrintState>,
  fn: (s: ChunkedPrintState) => Partial<ChunkedPrintState>,
): void {
  state.update((s) => ({ ...s, ...fn(s) }));
}

function patchSegment(
  state: WritableSignal<ChunkedPrintState>,
  index: number,
  fn: (s: PrintSegment) => Partial<PrintSegment> & { status?: SegmentStatus },
): void {
  state.update((s) => ({
    ...s,
    segments: s.segments.map((seg) => (seg.index === index ? { ...seg, ...fn(seg) } : seg)),
  }));
}

function delay(ms: number): Promise<void> {
  return new Promise((r) => setTimeout(r, ms));
}
