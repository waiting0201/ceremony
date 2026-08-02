import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import type { TestRequest } from '@angular/common/http/testing';
import { PrintService } from './print.service';
import { AuthStore } from '../auth/auth.store';
import type { CeremonyBridge, PrintResult } from '../platform/electron';
import { environment } from '../../../environments/environment';

/**
 * PrintService 的契約（2026-08-02 改版後）：
 *
 * 1. Electron 單筆／批次都**不把 PDF 抓進 renderer**，而是把 API 路徑交給主行程串流取檔
 *    → 合併成一份的大批次不會 renderer + main 各佔一份。
 * 2. 批次先等 job 渲染完成（進度 overlay 由 BatchPrintService 負責），完成後才開預覽視窗；
 *    `/file` 是 one-shot，renderer 不能先取走。
 * 3. 主行程回錯誤時原樣拋成 UserFacingError（不能被 toMessage 吞成「操作失敗」）。
 * 4. 瀏覽器沒有 bridge → 退回開新分頁，且不呼叫任何 bridge 方法。
 */
describe('PrintService', () => {
  const BASE = `${environment.apiBaseUrl}/reports`;
  const JOB_ID = '11111111-1111-1111-1111-111111111111';
  const TOKEN = 'tok-1';

  let sut: PrintService;
  let httpMock: HttpTestingController;

  let viewerCalls: { reportType: string; apiPath: string; token: string }[];
  let bufferCalls: { reportType: string; bytes: Uint8Array }[];
  let viewerResult: PrintResult;
  let opened: Blob[];

  function fakeBridge(): CeremonyBridge {
    return {
      openReportInViewer: async (reportType: string, apiPath: string, token: string) => {
        viewerCalls.push({ reportType, apiPath, token });
        return viewerResult;
      },
      openPdfInViewer: async (reportType: string, bytes: Uint8Array) => {
        bufferCalls.push({ reportType, bytes });
        return viewerResult;
      },
      openPrintLogFolder: async () => ({ ok: true }),
    } as unknown as CeremonyBridge;
  }

  beforeEach(() => {
    viewerCalls = [];
    bufferCalls = [];
    opened = [];
    viewerResult = { ok: true };
    delete window.ceremony;

    // 開新分頁是瀏覽器路徑的終點，攔下來才能斷言「有沒有走到那裡」
    vi.spyOn(URL, 'createObjectURL').mockImplementation((b) => {
      opened.push(b as Blob);
      return 'blob:fake';
    });
    vi.spyOn(URL, 'revokeObjectURL').mockImplementation(() => undefined);
    vi.spyOn(window, 'open').mockImplementation(() => null);

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: AuthStore, useValue: { token: () => TOKEN } },
      ],
    });
    sut = TestBed.inject(PrintService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    delete window.ceremony;
    vi.restoreAllMocks();
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

  const jobDone = (total: number) => ({
    jobId: JOB_ID,
    status: 'completed',
    total,
    completed: total,
    fileName: 'batch.pdf',
  });

  it('單筆（Electron）：不抓 blob，直接把報表路徑交給主行程', async () => {
    asElectron();

    await expect(sut.printSingle('datacard', 's1')).resolves.toBe(true);

    expect(viewerCalls).toEqual([
      { reportType: 'datacard', apiPath: '/reports/datacard?signupId=s1', token: TOKEN },
    ]);
    // renderer 完全沒發出取 PDF 的請求
    httpMock.expectNone((r) => r.url.startsWith(BASE));
  });

  it('單筆（Electron）：signupId 有做 URL 編碼', async () => {
    asElectron();

    await sut.printSingle('tablet', 'a b&c');

    expect(viewerCalls[0].apiPath).toBe('/reports/tablet?signupId=a%20b%26c');
  });

  it('批次（Electron）：等 job 完成後開預覽，renderer 不取檔', async () => {
    asElectron();
    const run = sut.printBatch({ reportType: 'datacard', numberStart: 1, numberEnd: 500 });

    (await nextRequest('POST', '/batch/jobs')).flush({
      jobId: JOB_ID,
      total: 500,
      fileName: 'batch.pdf',
      reportType: 'datacard',
    });
    (await nextRequest('GET', `/batch/jobs/${JOB_ID}`)).flush(jobDone(500));

    await expect(run).resolves.toBe(true);

    expect(viewerCalls).toEqual([
      { reportType: 'datacard', apiPath: `/reports/batch/jobs/${JOB_ID}/file`, token: TOKEN },
    ]);
    // /file 是 one-shot：renderer 先取走的話主行程就拿不到了
    httpMock.expectNone((r) => r.url.endsWith('/file'));
  });

  it('批次（Electron）：使用者在進度 overlay 取消 → 不開預覽視窗', async () => {
    asElectron();
    const run = sut.printBatch({ reportType: 'datacard', numberStart: 1, numberEnd: 5 });

    (await nextRequest('POST', '/batch/jobs')).flush({
      jobId: JOB_ID,
      total: 5,
      fileName: 'batch.pdf',
      reportType: 'datacard',
    });
    (await nextRequest('GET', `/batch/jobs/${JOB_ID}`)).flush({
      jobId: JOB_ID,
      status: 'canceled',
      total: 5,
      completed: 2,
      fileName: 'batch.pdf',
    });

    await expect(run).resolves.toBe(false);
    expect(viewerCalls).toHaveLength(0);
  });

  it('主行程回錯誤 → 拋出可顯示的訊息，不被吞成「操作失敗」', async () => {
    asElectron();
    viewerResult = { ok: false, error: '尚未連線' };

    await expect(sut.printSingle('receipt', 's1')).rejects.toThrow('尚未連線');
  });

  it('printBlob（Electron）：把手上的 PDF bytes 交給檢視器視窗', async () => {
    asElectron();
    // jsdom 的 Blob 沒有 arrayBuffer()（瀏覽器與 Electron 都有）→ 這裡補上
    const bytes = new TextEncoder().encode('%PDF-1.7');
    const blob = Object.assign(new Blob([bytes], { type: 'application/pdf' }), {
      arrayBuffer: async () => bytes.buffer,
    }) as Blob;

    await expect(sut.printBlob('text', blob)).resolves.toBe(true);

    expect(bufferCalls).toHaveLength(1);
    expect(bufferCalls[0].reportType).toBe('text');
    expect(bufferCalls[0].bytes.byteLength).toBe(8);
  });

  it('瀏覽器：單筆自己抓 blob 開新分頁，不碰 bridge', async () => {
    const run = sut.printSingle('datacard', 's1');

    httpMock
      .expectOne((r) => r.url === `${BASE}/datacard`)
      .flush(new Blob(['%PDF-1.7'], { type: 'application/pdf' }));

    await expect(run).resolves.toBe(true);
    expect(opened).toHaveLength(1);
    expect(viewerCalls).toHaveLength(0);
  });

  it('瀏覽器：批次走 job 並取檔開新分頁', async () => {
    const run = sut.printBatch({ reportType: 'datacard', numberStart: 1, numberEnd: 3 });

    (await nextRequest('POST', '/batch/jobs')).flush({
      jobId: JOB_ID,
      total: 3,
      fileName: 'batch.pdf',
      reportType: 'datacard',
    });
    (await nextRequest('GET', `/batch/jobs/${JOB_ID}`)).flush(jobDone(3));
    (await nextRequest('GET', `/batch/jobs/${JOB_ID}/file`)).flush(
      new Blob(['%PDF-1.7'], { type: 'application/pdf' }),
    );

    await expect(run).resolves.toBe(true);
    expect(opened).toHaveLength(1);
  });
});
