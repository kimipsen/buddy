import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { from, switchMap } from 'rxjs';

import { AuthService } from './auth.service';
import { RuntimeConfigService } from './runtime-config.service';

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  const runtimeConfig = inject(RuntimeConfigService);

  if (!req.url.startsWith(runtimeConfig.apiBaseUrl)) {
    return next(req);
  }

  return from(auth.getAccessToken()).pipe(
    switchMap((token) => {
      if (!token) {
        return next(req);
      }

      return next(req.clone({ setHeaders: { Authorization: `Bearer ${token}` } }));
    })
  );
};
