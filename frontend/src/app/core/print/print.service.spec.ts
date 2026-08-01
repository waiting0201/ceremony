import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import type { TestRequest } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { authInterceptor } from '../http/auth.interceptor';
import { PrintDialogService } from '../../shared/print-dialog/print-dialog.service';
import type {
  PrintDialogRequest,
  PrintDialogResult,
} from '../../shared/print-dialog/print-dialog.types';
import {
  ProgressOverlayService,
  type ProgressOverlayHandle,
} from '../../shared/progress-overlay/progress-overlay.service';
import type { ProgressOverlayConfig } from '../../shared/progress-overlay/progress-overlay.types';
import { BatchPrintPanelService } from '../../shared/batch-print-panel/batch-print-panel.service';
import type { CeremonyBridge, PrintResult } from '../platform/electron';
import { UserFacingError } from '../errors/to-message';
import type { ChunkedPrintRun } from '../reports/chunked-print.service';
import { isSettled } from '../reports/chunked-print.types';
import { installBlobPolyfill } from '../../testing/blob-polyfill';
import { PrintService } from './print.service';

/**
 * 列印通道的行為鎖。重點在三件事：
 * 1. 送印前一定經過預覽對話框，且拿到的是真的 PDF bytes。
 * 2. X-Report-Page-Size 有一路帶到主行程（沒帶會靜默退化成 fallback 紙張表）。
 * 3. 主行程回的失敗訊息不會被吞成「操作失敗，請稍後再試」。
 */
describe('PrintService', () => {
  const BASE = 'http://localhost:5050/api/v1/reports';
  const JOB_ID = '11111111-1111-1111-1111-111111111111';

  interface PdfBufferCall {
    reportType: string;
    bytes: Uint8Array;
    pageSizeHeader?: string | null;
  }

  interface DiagnoseCall {
    action: 'viewer' | 'log';
    reportType?: string;
    bytes?: Uint8Array;
  }

  const DRIVER = { scale: 'driver', orientation: 'driver', paper: 'driver' } as const;

  let asked: PrintDialogRequest[];
  let diagnoseCalls: DiagnoseCall[];
  let dialogAnswer: PrintDialogResult | null;
  let pdfBufferCalls: PdfBufferCall[];
  /** 分段面板被開啟幾次（測試用 fake：一跑完就自動關，真面板要等使用者按「關閉」）。 */
  let panelOpens: ChunkedPrintRun[];
  let printResult: PrintResult;
  let savedSettings: { reportType: string }[];
  let sut: PrintService;
  let httpMock: HttpTestingController;

  /** 只實作 PrintService 會用到的部分；其餘留白，用到就會在型別層被抓出來。 */
  function fakeBridge(): CeremonyBridge {
    const partial: Partial<CeremonyBridge> = {
      listPrinters: async () => [
        { name: 'HP-1', displayName: 'HP LaserJet', isDefault: true, status: 0 },
      ],
      getPrintSettings: async () => ({ version: 2 as const, byReportType: {} }),
      savePrintSetting: async (reportType: string) => {
        savedSettings.push({ reportType });
        return { version: 2 as const, byReportType: {} };
      },
      printPdfBuffer: async (reportType, bytes, _o, pageSizeHeader) => {
        pdfBufferCalls.push({ reportType, bytes, pageSizeHeader });
        return printResult;
      },
      openPdfInViewer: async (reportType, bytes) => {
        diagnoseCalls.push({ action: 'viewer', reportType, bytes });
        return { ok: true };
      },
      openPrintLogFolder: async () => {
        diagnoseCalls.push({ action: 'log' });
        return { ok: true };
      },
    };
    return partial as CeremonyBridge;
  }

  class FakeOverlay {
    open(config: ProgressOverlayConfig): ProgressOverlayHandle {
      void config;
      return {
        update: () => undefined,
        canceled: new Promise<void>(() => undefined), // 永不取消
        close: () => undefined,
      };
    }
  }

  beforeEach(() => {
    installBlobPolyfill(); // jsdom 的 Blob 沒有 arrayBuffer()，送印那步會炸
    asked = [];
    diagnoseCalls = [];
    pdfBufferCalls = [];
    panelOpens = [];
    savedSettings = [];
    dialogAnswer = { copies: 1, ...DRIVER, remember: false };
    printResult = { ok: true };

    TestBed.configureTestingModule({
      providers: [
        // 帶上 authInterceptor：HttpErrorResponse → ApiError 的轉換靠它，測試要與正式環境一致
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: ProgressOverlayService, useValue: new FakeOverlay() },
        {
          provide: PrintDialogService,
          useValue: {
            ask: (req: PrintDialogRequest) => {
              asked.push(req);
              return Promise.resolve(dialogAnswer);
            },
          },
        },
        {
          // 真面板要等使用者按「關閉」；測試裡等分段跑完（phase 進入終態）就視為關閉
          provide: BatchPrintPanelService,
          useValue: {
            open: async (run: ChunkedPrintRun) => {
              panelOpens.push(run);
              for (let i = 0; i < 2000; i++) {
                if (isSettled(run.state().phase)) return;
                await new Promise((r) => setTimeout(r, 2));
              }
              throw new Error('分段列印沒有進入終態');
            },
          },
        },
      ],
    });
    sut = TestBed.inject(PrintService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    delete window.ceremony;
    httpMock.verify();
  });

  const asElectron = () => (window.ceremony = fakeBridge());

  /** 輪詢是 setTimeout 驅動的，不能同步 expectOne。 */
  async function nextRequest(method: string, urlPart: string): Promise<TestRequest> {
    for (let i = 0; i < 400; i++) {
      const found = httpMock.match((r) => r.method === method && r.url.includes(urlPart));
      if (found.length) return found[0];
      await new Promise((r) => setTimeout(r, 5));
    }
    throw new Error(`等不到 ${method} ${urlPart}`);
  }

  /** N 筆的 plan，編號從 1 連號（前端只靠 items 的順序切段）。 */
  function planOf(total: number) {
    return {
      reportType: 'datacard',
      fileName: 'batch.pdf',
      total,
      items: Array.from({ length: total }, (_, i) => ({
        id: `00000000-0000-0000-0000-${String(i).padStart(12, '0')}`,
        number: i + 1,
      })),
    };
  }

  const jobDone = (total: number) => ({
    jobId: JOB_ID,
    status: 'completed',
    total,
    completed: total,
    fileName: 'seg.pdf',
  });

  function flushSinglePdf(type = 'datacard', pageSize: string | null = '210000x148000'): void {
    httpMock
      .expectOne((r) => r.url === `${BASE}/${type}`)
      .flush(new Blob(['%PDF-1.7'], { type: 'application/pdf' }), {
        headers: pageSize ? { 'X-Report-Page-Size': pageSize } : {},
      });
  }

  it('單筆（Electron）：對話框收到預覽 blob，送印帶上 bytes 與紙張 header', async () => {
    asElectron();
    const run = sut.printSingle('datacard', 's1');
    flushSinglePdf();

    await expect(run).resolves.toBe(true);

    expect(asked).toHaveLength(1);
    expect(asked[0].mode).toBe('printer');
    expect(asked[0].previewBlob).toBeInstanceOf(Blob);
    expect(asked[0].previewNotice).toBeUndefined();

    expect(pdfBufferCalls).toHaveLength(1);
    expect(pdfBufferCalls[0].reportType).toBe('datacard');
    expect(pdfBufferCalls[0].bytes.length).toBeGreaterThan(0);
    expect(pdfBufferCalls[0].pageSizeHeader).toBe('210000x148000');
  });

  /**
   * 診斷區是客訴「進不去印表機設定」的解：檢視器那條路會落到 Windows 原生列印對話框，
   * 有「印表機內容」按鈕，而且就是 v2.3.6 以前的送印路徑（現場對照組的基準線）。
   */
  it('診斷區把動作接到主行程，且不影響手上這次列印的結果', async () => {
    asElectron();
    const run = sut.printSingle('datacard', 's1');
    flushSinglePdf();
    await run;

    const diagnose = asked[0].onDiagnose;
    expect(diagnose).toBeTypeOf('function');

    diagnose!('log');
    diagnose!('viewer');
    // viewer 那條要先 blob.arrayBuffer()（polyfill 走 FileReader 事件），一個 tick 不夠
    for (let i = 0; i < 100 && diagnoseCalls.length < 2; i++) {
      await new Promise((r) => setTimeout(r, 0));
    }

    expect(diagnoseCalls.map((c) => c.action)).toEqual(['log', 'viewer']);
    // 略過預覽的大檔也要能診斷 → bytes 走的是 diagnoseBlob 而不是 previewBlob
    expect(diagnoseCalls[1].reportType).toBe('datacard');
    expect(diagnoseCalls[1].bytes!.length).toBeGreaterThan(0);
  });

  it('瀏覽器（preview-only）不給診斷區——那些能力只有 Electron 有', async () => {
    const run = sut.printSingle('datacard', 's1');
    flushSinglePdf();
    await run;

    expect(asked[0].mode).toBe('preview-only');
    expect(asked[0].onDiagnose).toBeUndefined();
  });

  it('使用者在對話框按取消 → 回 false，完全不呼叫主行程', async () => {
    asElectron();
    dialogAnswer = null;

    const run = sut.printSingle('receipt', 's1');
    flushSinglePdf('receipt');

    await expect(run).resolves.toBe(false);
    expect(pdfBufferCalls).toHaveLength(0);
  });

  it('勾「記住設定」才寫回 print-settings.json', async () => {
    asElectron();
    dialogAnswer = { copies: 2, ...DRIVER, remember: true };

    const run = sut.printSingle('tablet', 's1');
    flushSinglePdf('tablet');
    await run;

    expect(savedSettings).toEqual([{ reportType: 'tablet' }]);
  });

  it('主行程送印失敗 → 丟 UserFacingError 且保留原訊息（不得被吞成通用字串）', async () => {
    asElectron();
    printResult = { ok: false, error: '列印逾時（印表機無回應）' };

    const run = sut.printSingle('datacard', 's1');
    flushSinglePdf();

    await expect(run).rejects.toBeInstanceOf(UserFacingError);
    await expect(run).rejects.toThrow('列印逾時（印表機無回應）');
  });

  it('主行程回 canceled（使用者在系統層取消）→ 靜默回 false，不當成錯誤', async () => {
    asElectron();
    printResult = { ok: false, canceled: true };

    const run = sut.printSingle('datacard', 's1');
    flushSinglePdf();

    await expect(run).resolves.toBe(false);
  });

  it('批次單段（≤ SEGMENT_SIZE）：先取 plan → 一個 job → 有預覽 → printPdfBuffer', async () => {
    asElectron();
    const run = sut.printBatch({ reportType: 'datacard', numberStart: 1, numberEnd: 9 });

    (await nextRequest('POST', `${BASE}/batch/plan`)).flush(planOf(3));

    const create = await nextRequest('POST', `${BASE}/batch/jobs`);
    // plan 選定的那批要原樣帶進 job，不讓後端再查一次而有機會不一致
    expect((create.request.body as { signupIds: string[] }).signupIds).toHaveLength(3);
    create.flush({ jobId: JOB_ID, total: 3, fileName: 'batch.pdf', reportType: 'datacard' });

    (await nextRequest('GET', `${BASE}/batch/jobs/${JOB_ID}`)).flush(jobDone(3));
    (await nextRequest('GET', `${BASE}/batch/jobs/${JOB_ID}/file`)).flush(
      new Blob(['%PDF-1.7'], { type: 'application/pdf' }),
      { headers: { 'X-Signup-Count': '3', 'X-Report-Page-Size': '210000x148000' } },
    );

    await expect(run).resolves.toBe(true);
    expect(asked[0].previewBlob).toBeInstanceOf(Blob);
    expect(asked[0].detail).toBe('共 3 筆');
    expect(pdfBufferCalls[0].pageSizeHeader).toBe('210000x148000');
    expect(panelOpens).toHaveLength(0); // 單段不開分段面板，體驗與改版前相同
  });

  it('批次大量：切成多段逐段送印，對話框只問一次且帶第 1 段預覽', async () => {
    asElectron();
    const run = sut.printBatch({ reportType: 'datacard', numberStart: 1, numberEnd: 9999 });

    (await nextRequest('POST', `${BASE}/batch/plan`)).flush(planOf(450)); // → 3 段（200/200/50）

    const segmentSizes: number[] = [];
    for (let seg = 0; seg < 3; seg++) {
      const create = await nextRequest('POST', `${BASE}/batch/jobs`);
      const ids = (create.request.body as { signupIds: string[] }).signupIds;
      segmentSizes.push(ids.length);
      create.flush({
        jobId: JOB_ID,
        total: ids.length,
        fileName: 'seg.pdf',
        reportType: 'datacard',
      });

      (await nextRequest('GET', `${BASE}/batch/jobs/${JOB_ID}`)).flush(jobDone(ids.length));
      (await nextRequest('GET', `${BASE}/batch/jobs/${JOB_ID}/file`)).flush(
        new Blob(['%PDF-1.7'], { type: 'application/pdf' }),
        { headers: { 'X-Report-Page-Size': '210000x148000' } },
      );
    }

    await expect(run).resolves.toBe(true);

    expect(segmentSizes).toEqual([200, 200, 50]);
    // 每段各送印一次，且都帶到紙張 header
    expect(pdfBufferCalls).toHaveLength(3);
    expect(pdfBufferCalls.every((c) => c.pageSizeHeader === '210000x148000')).toBe(true);
    // 列印設定只問一次：不能每段都跳一次對話框
    expect(asked).toHaveLength(1);
    expect(asked[0].previewBlob).toBeInstanceOf(Blob);
    expect(asked[0].detail).toBe('共 450 筆，分 3 段');
    expect(panelOpens).toHaveLength(1);
  });

  it('批次大量：使用者在第 1 段的對話框按取消 → 一段都不印', async () => {
    asElectron();
    dialogAnswer = null;
    const run = sut.printBatch({ reportType: 'datacard', numberStart: 1, numberEnd: 9999 });

    (await nextRequest('POST', `${BASE}/batch/plan`)).flush(planOf(450));

    const create = await nextRequest('POST', `${BASE}/batch/jobs`);
    create.flush({ jobId: JOB_ID, total: 200, fileName: 'seg.pdf', reportType: 'datacard' });
    (await nextRequest('GET', `${BASE}/batch/jobs/${JOB_ID}`)).flush(jobDone(200));
    (await nextRequest('GET', `${BASE}/batch/jobs/${JOB_ID}/file`)).flush(
      new Blob(['%PDF-1.7'], { type: 'application/pdf' }),
    );

    await expect(run).resolves.toBe(false);
    expect(pdfBufferCalls).toHaveLength(0);
    // 沒有第 2 段的 job：整批中止，不是只跳過第 1 段
    httpMock.expectNone((r) => r.method === 'POST' && r.url.endsWith('/batch/jobs'));
  });

  it('plan 就失敗（編號錯誤）→ 錯誤往上拋，不建任何 job', async () => {
    asElectron();
    const run = sut.printBatch({ reportType: 'datacard', numberStart: 50, numberEnd: 10 });

    (await nextRequest('POST', `${BASE}/batch/plan`)).flush(
      { errorCode: 'VALIDATION_INVALID', message: '編號錯誤' },
      { status: 400, statusText: 'Bad Request' },
    );

    await expect(run).rejects.toMatchObject({ errorCode: 'VALIDATION_INVALID' });
    httpMock.expectNone((r) => r.url.endsWith('/batch/jobs'));
  });

  it('瀏覽器：對話框走 preview-only，確認後開新分頁，完全不碰 bridge', async () => {
    const origOpen = window.open;
    const origCreate = URL.createObjectURL;
    const origRevoke = URL.revokeObjectURL;
    const opened: string[] = [];
    window.open = ((url: string) => {
      opened.push(url);
      return null;
    }) as typeof window.open;
    URL.createObjectURL = () => 'blob:fake';
    URL.revokeObjectURL = () => undefined;

    try {
      const run = sut.printSingle('datacard', 's1');
      flushSinglePdf();

      await expect(run).resolves.toBe(true);
      expect(asked[0].mode).toBe('preview-only');
      expect(asked[0].printers).toEqual([]);
      expect(opened).toEqual(['blob:fake']);
    } finally {
      window.open = origOpen;
      URL.createObjectURL = origCreate;
      URL.revokeObjectURL = origRevoke;
    }
  });

  it('printBlob 在瀏覽器不開對話框（呼叫端畫面上已有全尺寸預覽）', async () => {
    const origOpen = window.open;
    const origCreate = URL.createObjectURL;
    const origRevoke = URL.revokeObjectURL;
    window.open = (() => null) as typeof window.open;
    URL.createObjectURL = () => 'blob:fake';
    URL.revokeObjectURL = () => undefined;

    try {
      await expect(sut.printBlob('datacard', new Blob(['%PDF-']))).resolves.toBe(true);
      expect(asked).toHaveLength(0);
    } finally {
      window.open = origOpen;
      URL.createObjectURL = origCreate;
      URL.revokeObjectURL = origRevoke;
    }
  });

  it('printBlob 在 Electron 開對話框並把呼叫端給的紙張 header 傳下去', async () => {
    asElectron();

    await expect(sut.printBlob('text', new Blob(['%PDF-']), '365000x262000')).resolves.toBe(true);

    expect(asked[0].mode).toBe('printer');
    expect(pdfBufferCalls[0].pageSizeHeader).toBe('365000x262000');
  });
});
