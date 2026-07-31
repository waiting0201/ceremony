import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { authInterceptor } from '../../http/auth.interceptor';
import { installBlobPolyfill } from '../../../testing/blob-polyfill';
import { ReportApi } from './report.api';

describe('ReportApi', () => {
  const BASE = 'http://localhost:5050/api/v1/reports';
  let sut: ReportApi;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    installBlobPolyfill(); // 錯誤 body 解析要 Blob.text()，jsdom 沒有
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
        provideRouter([]),
      ],
    });
    sut = TestBed.inject(ReportApi);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('single() 讀出 X-Report-Page-Size（紙張權威值，缺了就只能用 fallback 表）', async () => {
    const p = sut.single('datacard', 's1');

    const req = httpMock.expectOne((r) => r.url === `${BASE}/datacard`);
    req.flush(new Blob(['%PDF-'], { type: 'application/pdf' }), {
      headers: {
        'X-Report-Page-Size': '210000x148000',
        'Content-Disposition': 'attachment; filename="datacard-1.pdf"',
      },
    });

    const pdf = await p;
    expect(pdf.pageSizeHeader).toBe('210000x148000');
    expect(pdf.fileName).toBe('datacard-1.pdf');
  });

  it('header 缺席時 pageSizeHeader 是 undefined，不會變成空字串', async () => {
    const p = sut.single('datacard', 's1');
    httpMock.expectOne((r) => r.url === `${BASE}/datacard`).flush(new Blob(['%PDF-']));
    expect((await p).pageSizeHeader).toBeUndefined();
  });

  /**
   * responseType:'blob' 的錯誤 body 也是 Blob，同步的 ApiError.fromHttp 判不到 errorCode
   * → 使用者只看得到英文的 "Http failure response for …"。這條鎖住中文訊息出得來。
   */
  it('blob 端點失敗 → 從 Blob body 解出後端的 errorCode 與中文訊息', async () => {
    const p = sut.single('datacard', 'missing');

    httpMock.expectOne((r) => r.url === `${BASE}/datacard`).flush(
      new Blob([JSON.stringify({ errorCode: 'SIGNUP_NOT_FOUND', message: '找不到報名' })], {
        type: 'application/json',
      }),
      { status: 404, statusText: 'Not Found' },
    );

    await expect(p).rejects.toMatchObject({
      status: 404,
      errorCode: 'SIGNUP_NOT_FOUND',
      message: '找不到報名',
    });
  });

  it('錯誤 body 不是 JSON（HTML 錯誤頁）→ 落回 INTERNAL_ERROR，不會拋解析例外', async () => {
    const p = sut.single('datacard', 'x');

    httpMock
      .expectOne((r) => r.url === `${BASE}/datacard`)
      .flush(new Blob(['<html>500</html>'], { type: 'text/html' }), {
        status: 500,
        statusText: 'Server Error',
      });

    await expect(p).rejects.toMatchObject({ status: 500, errorCode: 'INTERNAL_ERROR' });
  });
});
