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
import { TranslationService } from './core/i18n/translation.service';
import { RuntimeConfigService } from './core/runtime-config.service';
import { routes } from './app.routes';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    // The feature flags need apiBaseUrl, so they load once the runtime config has. The browser
    // language's dictionary (a lazy chunk unless it's English) loads alongside, so the first
    // screen doesn't flash English.
    provideAppInitializer(async () => {
      const features = inject(FeaturesService);
      const translations = inject(TranslationService).ready();
      await inject(RuntimeConfigService).load();
      await Promise.all([features.load(), translations]);
    }),
    provideHttpClient(withInterceptors([authInterceptor])),
    provideRouter(routes),
  ],
};
