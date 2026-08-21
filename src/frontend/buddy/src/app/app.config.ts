import { ApplicationConfig, inject, provideAppInitializer, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideRouter } from '@angular/router';

import { authInterceptor } from './core/auth.interceptor';
import { RuntimeConfigService } from './core/runtime-config.service';
import { routes } from './app.routes';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideAppInitializer(() => inject(RuntimeConfigService).load()),
    provideHttpClient(withInterceptors([authInterceptor])),
    provideRouter(routes)
  ]
};
