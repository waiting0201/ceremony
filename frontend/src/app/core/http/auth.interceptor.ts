import {
  HttpErrorResponse,
  HttpEvent,
  HttpHandlerFn,
  HttpInterceptorFn,
  HttpRequest,
} from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, from, Observable, switchMap, throwError } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ApiError } from './api-error';
import { AuthStore } from '../auth/auth.store';

export const authInterceptor: HttpInterceptorFn = (
  req: HttpRequest<unknown>,
  next: HttpHandlerFn,
): Observable<HttpEvent<unknown>> => {
  const auth = inject(AuthStore);
  const router = inject(Router);

  const isApiCall = req.url.startsWith(environment.apiBaseUrl);
  const token = auth.token();

  const authed =
    isApiCall && token
      ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } })
      : req;

  /** 401 → 清 session 並導回登入；回傳同一個 ApiError 讓呼叫端照常處理。 */
  const handle401 = (apiErr: ApiError): ApiError => {
    if (apiErr.status === 401 && isApiCall && !req.url.endsWith('/auth/login')) {
      auth.clearSession();
      void router.navigateByUrl('/login');
    }
    return apiErr;
  };

  return next(authed).pipe(
    catchError((err: unknown) => {
      if (!(err instanceof HttpErrorResponse)) return throwError(() => err);
      // blob 請求的錯誤 body 要 await 才讀得到 → 只有這條路轉非同步，
      // 其餘維持同步，避免既有測試的時序假設全部鬆動。
      if (err.error instanceof Blob) {
        return from(ApiError.fromHttpAsync(err)).pipe(
          switchMap((apiErr) => throwError(() => handle401(apiErr))),
        );
      }
      return throwError(() => handle401(ApiError.fromHttp(err)));
    }),
  );
};
