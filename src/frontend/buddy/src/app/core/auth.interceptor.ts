import { HttpErrorResponse, HttpInterceptorFn, HttpRequest } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, from, switchMap, throwError } from 'rxjs';

import { AuthService } from './auth.service';
import { storePendingReturnUrl } from './pending-return-url';
import { RuntimeConfigService } from './runtime-config.service';
import { SESSION_EXPIRED_QUERY_PARAMS } from './session-expired';

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  const router = inject(Router);
  const runtimeConfig = inject(RuntimeConfigService);

  if (!req.url.startsWith(runtimeConfig.apiBaseUrl)) {
    return next(req);
  }

  return from(auth.getAccessToken()).pipe(
    switchMap((token) => {
      if (!token) {
        // Anonymous pages (share links, invite previews) call the API with no session at all, so
        // only a session that existed and ended goes back to /login. Its request could only 401.
        return auth.sessionExpired() ? sessionEnded(router, req) : next(req);
      }

      return next(req.clone({ setHeaders: { Authorization: `Bearer ${token}` } })).pipe(
        catchError((error: unknown) => {
          if (error instanceof HttpErrorResponse && error.status === 401) {
            auth.expireSession();
            return sessionEnded(router, req);
          }

          return throwError(() => error);
        }),
      );
    }),
  );
};

// Sends the user to /login (once, however many requests fail together) and still errors the
// request, so the caller's own error handling runs instead of its promise hanging.
function sessionEnded(router: Router, req: HttpRequest<unknown>) {
  if (!router.url.startsWith('/login')) {
    storePendingReturnUrl(router.url);
    void router.navigate(['/login'], { queryParams: SESSION_EXPIRED_QUERY_PARAMS });
  }

  return throwError(
    () =>
      new HttpErrorResponse({ status: 401, statusText: 'Unauthorized', url: req.urlWithParams }),
  );
}
