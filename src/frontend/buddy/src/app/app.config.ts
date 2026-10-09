import {
  ApplicationConfig,
  inject,
  provideAppInitializer,
  provideBrowserGlobalErrorListeners,
} from '@angular/core';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideRouter } from '@angular/router';

import { authInterceptor } from './core/auth.interceptor';
import { FeaturesService } from './core/features.service';
import { RuntimeConfigService } from './core/runtime-config.service';
import { routes } from './app.routes';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    // The feature flags need apiBaseUrl, so they load once the runtime config has.
    provideAppInitializer(async () => {
      const features = inject(FeaturesService);
      await inject(RuntimeConfigService).load();
      await features.load();
    }),
    provideHttpClient(withInterceptors([authInterceptor])),
    provideRouter(routes),
  ],
};
