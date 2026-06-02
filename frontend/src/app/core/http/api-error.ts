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
}
