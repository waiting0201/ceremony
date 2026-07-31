import { HttpErrorResponse } from '@angular/common/http';

export interface ApiErrorBody {
  errorCode: string;
  message: string;
  traceId?: string;
}

export class ApiError extends Error {
  constructor(
    readonly status: number,
    readonly errorCode: string,
    message: string,
    readonly traceId?: string,
  ) {
    super(message);
    this.name = 'ApiError';
  }

  static fromHttp(err: HttpErrorResponse): ApiError {
    const body = err.error as Partial<ApiErrorBody> | string | null;
    if (body && typeof body === 'object' && body.errorCode) {
      return new ApiError(
        err.status,
        body.errorCode,
        body.message ?? '未預期的伺服器錯誤',
        body.traceId,
      );
    }
    if (err.status === 0) {
      return new ApiError(0, 'NETWORK_ERROR', '無法連線到伺服器，請確認網路與 API 服務狀態');
    }
    return new ApiError(err.status, 'INTERNAL_ERROR', err.message || '未預期的伺服器錯誤');
  }

  /**
   * responseType:'blob' 的請求失敗時，`err.error` 是 Blob（{ errorCode, message } JSON 包在裡面），
   * 同步的 {@link fromHttp} 判不到 errorCode → 使用者看到英文的
   * "Http failure response for …: 404 Not Found"，後端的「找不到報名」永遠出不來。
   * Blob → text 是非同步的，所以另開這條路；只有真的是 Blob 時才走，其餘完全沿用同步規則。
   */
  static async fromHttpAsync(err: HttpErrorResponse): Promise<ApiError> {
    if (err.error instanceof Blob && err.error.size > 0) {
      try {
        const body = JSON.parse(await err.error.text()) as Partial<ApiErrorBody>;
        if (body?.errorCode) {
          return new ApiError(
            err.status,
            body.errorCode,
            body.message ?? '未預期的伺服器錯誤',
            body.traceId,
          );
        }
      } catch {
        // 不是 JSON（HTML 錯誤頁，或 body 其實是 PDF）→ 落回同步規則
      }
    }
    return ApiError.fromHttp(err);
  }
}
