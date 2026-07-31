import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import type { TestRequest } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { authInterceptor } from '../http/auth.interceptor';
import type { BatchReportPlanItem } from '../api/reports/report.models';
import { buildSegments, ChunkedPrintService, SEGMENT_SIZE } from './chunked-print.service';
import { printedCount, segmentLabel } from './chunked-print.types';

describe('buildSegments', () => {
  const items = (n: number, startNumber = 1): BatchReportPlanItem[] =>
    Array.from({ length: n }, (_, i) => ({ id: `id-${i}`, number: startNumber + i }));

  it('依 SEGMENT_SIZE 切段，最後一段是餘數', () => {
    const segs = buildSegments(items(450));
    expect(segs.map((s) => s.signupIds.length)).toEqual([200, 200, 50]);
    expect(segs.map((s) => s.index)).toEqual([1, 2, 3]);
  });

  it('剛好整除不會多出空段', () => {
    expect(buildSegments(items(SEGMENT_SIZE * 2))).toHaveLength(2);
  });

  it('編號範圍取該段的最小與最大——卡紙時要能對上手裡那疊紙', () => {
    const segs = buildSegments(items(450, 1001));
    expect([segs[0].numberFrom, segs[0].numberTo]).toEqual([1001, 1200]);
    expect([segs[2].numberFrom, segs[2].numberTo]).toEqual([1401, 1450]);
    expect(segmentLabel(segs[2])).toBe('第 3 段：編號 1401–1450');
  });

  it('全部未配號 → 標題退回筆數，不顯示假的編號範圍', () => {
    const segs = buildSegments([{ id: 'a', number: null }, { id: 'b', number: null }]);
    expect(segs[0].numberFrom).toBeNull();
    expect(segmentLabel(segs[0])).toBe('第 1 段：2 筆');
  });
});

/**
 * 分段列印狀態機的行為鎖。重點是「中途出事不必整批重來」——
 * 單段失敗不能拖垮其他段，暫停要停在段邊界，重印要能單獨補。
 */
describe('ChunkedPrintService', () => {
  const BASE = 'http://localhost:5050/api/v1/reports';
  const JOB_ID = '11111111-1111-1111-1111-111111111111';

  let sut: ChunkedPrintService;
  let httpMock: HttpTestingController;
  /** 每次送印記一筆，用來斷言「印了幾段、順序如何」 */
  let printCalls: number[];
  let printOutcome: 'ok' | 'canceled' | 'throw';

  beforeEach(() => {
    printCalls = [];
    printOutcome = 'ok';
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
        provideRouter([]),
      ],
    });
    sut = TestBed.inject(ChunkedPrintService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  const items = (n: number): BatchReportPlanItem[] =>
    Array.from({ length: n }, (_, i) => ({ id: `id-${i}`, number: i + 1 }));

  async function nextRequest(method: string, urlPart: string): Promise<TestRequest> {
    for (let i = 0; i < 400; i++) {
      const found = httpMock.match((r) => r.method === method && r.url.includes(urlPart));
      if (found.length) return found[0];
      await new Promise((r) => setTimeout(r, 5));
    }
    throw new Error(`等不到 ${method} ${urlPart}`);
  }

  /** 讓一段完整跑完後端流程（建 job → 完成 → 取檔）。 */
  async function flushSegment(size: number): Promise<void> {
    (await nextRequest('POST', `${BASE}/batch/jobs`)).flush({
      jobId: JOB_ID,
      total: size,
      fileName: 'seg.pdf',
      reportType: 'datacard',
    });
    (await nextRequest('GET', `${BASE}/batch/jobs/${JOB_ID}`)).flush({
      jobId: JOB_ID,
      status: 'completed',
      total: size,
      completed: size,
      fileName: 'seg.pdf',
    });
    (await nextRequest('GET', `${BASE}/batch/jobs/${JOB_ID}/file`)).flush(
      new Blob(['%PDF-1.7'], { type: 'application/pdf' }),
    );
  }

  function startRun(count: number) {
    let seq = 0;
    return sut.start(
      { reportType: 'datacard' },
      items(count),
      '資料卡',
      async () => {
        printCalls.push(++seq);
        if (printOutcome === 'throw') throw new Error('印表機未回應');
        return printOutcome === 'ok';
      },
      async () => true,
    );
  }

  /** 等狀態機到達某個條件（狀態轉換是 async 的，不能同步斷言）。 */
  async function waitFor(check: () => boolean, what: string): Promise<void> {
    for (let i = 0; i < 400; i++) {
      if (check()) return;
      await new Promise((r) => setTimeout(r, 5));
    }
    throw new Error(`等不到：${what}`);
  }

  it('逐段送印，全部跑完後 phase 進入 done', async () => {
    const run = startRun(450);

    await flushSegment(200);
    await flushSegment(200);
    await flushSegment(50);

    await waitFor(() => run.state().phase === 'done', 'phase done');
    expect(printCalls).toEqual([1, 2, 3]);
    expect(run.state().segments.map((s) => s.status)).toEqual(['printed', 'printed', 'printed']);
    expect(printedCount(run.state().segments)).toBe(450);
  });

  it('單段渲染失敗不拖垮其他段：標記 failed 後繼續印下一段', async () => {
    const run = startRun(450);

    await flushSegment(200);

    // 第 2 段的 job 直接回 500
    (await nextRequest('POST', `${BASE}/batch/jobs`)).flush(
      { errorCode: 'INTERNAL_ERROR', message: '未預期的伺服器錯誤' },
      { status: 500, statusText: 'Server Error' },
    );

    await flushSegment(50);

    await waitFor(() => run.state().phase === 'done', 'phase done');
    const segs = run.state().segments;
    expect(segs.map((s) => s.status)).toEqual(['printed', 'failed', 'printed']);
    expect(segs[1].errorMessage).toBe('未預期的伺服器錯誤');
    // 失敗那段不計入已送印筆數
    expect(printedCount(segs)).toBe(250);
  });

  it('暫停：不中斷正在進行的那一段，但不再送下一段', async () => {
    const run = startRun(450);

    // 在第 1 段還沒渲染完時就按暫停——已交給 spooler 的段停不了，那是印表機的事
    (await nextRequest('POST', `${BASE}/batch/jobs`)).flush({
      jobId: JOB_ID,
      total: 200,
      fileName: 'seg.pdf',
      reportType: 'datacard',
    });
    run.pause();

    (await nextRequest('GET', `${BASE}/batch/jobs/${JOB_ID}`)).flush({
      jobId: JOB_ID,
      status: 'completed',
      total: 200,
      completed: 200,
      fileName: 'seg.pdf',
    });
    (await nextRequest('GET', `${BASE}/batch/jobs/${JOB_ID}/file`)).flush(new Blob(['%PDF-']));

    // 第 1 段照樣印完（pause 只擋下一段的送出），但不會有第 2 段的 job
    await waitFor(() => run.state().segments[0].status === 'printed', '第 1 段送印完成');
    expect(run.state().phase).toBe('paused');
    expect(printCalls).toEqual([1]);
    await new Promise((r) => setTimeout(r, 60));
    httpMock.expectNone((r) => r.method === 'POST');

    run.resume();
    await flushSegment(200);
    await flushSegment(50);
    await waitFor(() => run.state().phase === 'done', 'phase done');
    expect(printCalls).toEqual([1, 2, 3]);
    // resume 不會再問一次印表機設定（beforeFirstPrint 只在第一段跑）
    expect(run.state().segments.every((s) => s.status === 'printed')).toBe(true);
  });

  it('重印單段：重新建 job 重新渲染（不吃快取，資料才會是最新的）', async () => {
    const run = startRun(250); // 2 段

    await flushSegment(200);
    await flushSegment(50);
    await waitFor(() => run.state().phase === 'done', 'phase done');
    expect(printCalls).toEqual([1, 2]);

    const reprinting = run.reprint(1);
    const create = await nextRequest('POST', `${BASE}/batch/jobs`);
    // 重印帶的是「那一段」的 id，不是整批
    expect((create.request.body as { signupIds: string[] }).signupIds).toHaveLength(200);
    create.flush({ jobId: JOB_ID, total: 200, fileName: 'seg.pdf', reportType: 'datacard' });
    (await nextRequest('GET', `${BASE}/batch/jobs/${JOB_ID}`)).flush({
      jobId: JOB_ID,
      status: 'completed',
      total: 200,
      completed: 200,
      fileName: 'seg.pdf',
    });
    (await nextRequest('GET', `${BASE}/batch/jobs/${JOB_ID}/file`)).flush(new Blob(['%PDF-']));
    await reprinting;

    expect(printCalls).toEqual([1, 2, 3]);
    expect(run.state().segments[0].status).toBe('printed');
  });

  it('主迴圈跑著時 reprint 靜默不動作（兩邊會搶同一台印表機）', async () => {
    const run = startRun(450);
    expect(run.state().phase).toBe('running');

    await run.reprint(3);
    expect(printCalls).toEqual([]);

    await flushSegment(200);
    await flushSegment(200);
    await flushSegment(50);
    await waitFor(() => run.state().phase === 'done', 'phase done');
  });

  it('送印被系統層取消 → 該段標記已略過，不是失敗', async () => {
    printOutcome = 'canceled';
    const run = startRun(250);

    await flushSegment(200);
    await flushSegment(50);

    await waitFor(() => run.state().phase === 'done', 'phase done');
    expect(run.state().segments.map((s) => s.status)).toEqual(['canceled', 'canceled']);
    expect(printedCount(run.state().segments)).toBe(0);
  });

  it('送印丟例外 → 該段 failed 並保留主行程給的訊息', async () => {
    printOutcome = 'throw';
    const run = startRun(50);

    await flushSegment(50);

    await waitFor(() => run.state().phase === 'done', 'phase done');
    // toMessage 對非 UserFacingError 走 fallback，訊息不會外洩技術細節但一定有東西可看
    expect(run.state().segments[0].status).toBe('failed');
    expect(run.state().segments[0].errorMessage).toBeTruthy();
  });
});
