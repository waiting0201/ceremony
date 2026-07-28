import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import type { TestRequest } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { ApiError } from '../http/api-error';
import { authInterceptor } from '../http/auth.interceptor';
import {
  ProgressOverlayService,
  type ProgressOverlayHandle,
} from '../../shared/progress-overlay/progress-overlay.service';
import type { ProgressOverlayConfig } from '../../shared/progress-overlay/progress-overlay.types';
import { BatchPrintService } from './batch-print.service';

/**
 * 批次列印 job 流程的行為鎖：POST 建 job → 輪詢進度 → 取檔／取消／失敗。
 */
describe('BatchPrintService', () => {
  const JOB_ID = '11111111-1111-1111-1111-111111111111';
  const BASE = 'http://localhost:5050/api/v1/reports';

  /** 攔下 overlay，記錄每次 update 的內容，並讓測試能主動觸發「使用者按取消」。 */
  class FakeOverlay {
    readonly updates: ProgressOverlayConfig[] = [];
    closed = false;
    private fireCancel!: () => void;

    open(config: ProgressOverlayConfig): ProgressOverlayHandle {
      let current: ProgressOverlayConfig = { cancelable: true, ...config };
      this.updates.push(current);
      const canceled = new Promise<void>((resolve) => (this.fireCancel = resolve));
      return {
        update: (patch) => {
          current = { ...current, ...patch };
          this.updates.push(current);
        },
        canceled,
        close: () => (this.closed = true),
      };
    }

    userCancels(): void {
      this.fireCancel();
    }
  }

  let overlay: FakeOverlay;
  let sut: BatchPrintService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    overlay = new FakeOverlay();
    TestBed.configureTestingModule({
      providers: [
        // 帶上 authInterceptor：HttpErrorResponse → ApiError 的轉換靠它，測試要與正式環境一致
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: ProgressOverlayService, useValue: overlay },
      ],
    });
    sut = TestBed.inject(BatchPrintService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  /** 等下一個符合的請求出現（輪詢是 setTimeout 驅動的，不能同步 expectOne）。 */
  async function nextRequest(method: string, urlPart: string): Promise<TestRequest> {
    for (let i = 0; i < 400; i++) {
      const found = httpMock.match((r) => r.method === method && r.url.includes(urlPart));
      if (found.length) return found[0];
      await new Promise((r) => setTimeout(r, 5));
    }
    throw new Error(`等不到 ${method} ${urlPart}`);
  }

  const created = { jobId: JOB_ID, total: 3, fileName: 'batch-datacard-1-9.pdf', reportType: 'datacard' };

  const state = (status: string, completed: number) => ({
    jobId: JOB_ID,
    status,
    total: 3,
    completed,
    fileName: 'batch-datacard-1-9.pdf',
  });

  it('建 job → 輪詢進度 → 取檔，回傳 PDF 並關閉 overlay', async () => {
    const run = sut.run({ reportType: 'datacard', numberStart: 1, numberEnd: 9 });

    (await nextRequest('POST', `${BASE}/batch/jobs`)).flush(created);

    (await nextRequest('GET', `${BASE}/batch/jobs/${JOB_ID}`)).flush(state('running', 1));
    (await nextRequest('GET', `${BASE}/batch/jobs/${JOB_ID}`)).flush(state('running', 3));
    (await nextRequest('GET', `${BASE}/batch/jobs/${JOB_ID}`)).flush(state('completed', 3));

    (await nextRequest('GET', `${BASE}/batch/jobs/${JOB_ID}/file`)).flush(
      new Blob(['%PDF-1.7'], { type: 'application/pdf' }),
      { headers: { 'X-Signup-Count': '3' } },
    );

    const result = await run;

    expect(result).not.toBeNull();
    expect(result!.fileName).toBe('batch-datacard-1-9.pdf');
    expect(result!.signupCount).toBe(3);
    expect(overlay.closed).toBe(true);

    // 進度確實從 0 推到 3；筆數跑滿但還在 running 時要提示正在合併
    expect(overlay.updates.map((u) => u.completed)).toEqual([0, 1, 3, 3]);
    expect(overlay.updates[2].note).toBe('合併 PDF…');
    expect(overlay.updates.at(-1)!.note).toBe('下載中…');
  });

  it('使用者取消 → 送出 DELETE，run() 回 null 且不取檔', async () => {
    const run = sut.run({ reportType: 'datacard', numberStart: 1, numberEnd: 9 });

    (await nextRequest('POST', `${BASE}/batch/jobs`)).flush(created);
    (await nextRequest('GET', `${BASE}/batch/jobs/${JOB_ID}`)).flush(state('running', 1));

    overlay.userCancels();

    (await nextRequest('DELETE', `${BASE}/batch/jobs/${JOB_ID}`)).flush(null, { status: 204, statusText: 'No Content' });

    await expect(run).resolves.toBeNull();
    expect(overlay.closed).toBe(true);
  });

  it('後端回 canceled → run() 回 null', async () => {
    const run = sut.run({ reportType: 'datacard', numberStart: 1, numberEnd: 9 });

    (await nextRequest('POST', `${BASE}/batch/jobs`)).flush(created);
    (await nextRequest('GET', `${BASE}/batch/jobs/${JOB_ID}`)).flush(state('canceled', 2));

    await expect(run).resolves.toBeNull();
    expect(overlay.closed).toBe(true);
  });

  it('job 失敗 → 丟出帶 errorCode 的 ApiError', async () => {
    const run = sut.run({ reportType: 'datacard', numberStart: 1, numberEnd: 9 });

    (await nextRequest('POST', `${BASE}/batch/jobs`)).flush(created);
    (await nextRequest('GET', `${BASE}/batch/jobs/${JOB_ID}`)).flush({
      ...state('failed', 1),
      errorCode: 'INTERNAL_ERROR',
      message: '未預期的伺服器錯誤',
    });

    await expect(run).rejects.toMatchObject({
      errorCode: 'INTERNAL_ERROR',
      message: '未預期的伺服器錯誤',
    });
    expect(overlay.closed).toBe(true);
  });

  it('建立 job 就失敗（例如編號錯誤）→ 錯誤直接往上拋，不開 overlay', async () => {
    const run = sut.run({ reportType: 'datacard', numberStart: 50, numberEnd: 10 });

    (await nextRequest('POST', `${BASE}/batch/jobs`)).flush(
      { errorCode: 'VALIDATION_INVALID', message: '編號錯誤' },
      { status: 400, statusText: 'Bad Request' },
    );

    await expect(run).rejects.toBeInstanceOf(ApiError);
    expect(overlay.updates).toHaveLength(0);
  });
});
