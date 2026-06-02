import {
  HttpErrorResponse,
  HttpEvent,
  HttpHandlerFn,
  HttpInterceptorFn,
  HttpRequest,
} from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, Observable, throwError } from 'rxjs';
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

  return next(authed).pipe(
    catchError((err: unknown) => {
      if (err instanceof HttpErrorResponse) {
        const apiErr = ApiError.fromHttp(err);
        if (apiErr.status === 401 && isApiCall && !req.url.endsWith('/auth/login')) {
          auth.clearSession();
          void router.navigateByUrl('/login');
        }
        return throwError(() => apiErr);
      }
      return throwError(() => err);
    }),
  );
};
