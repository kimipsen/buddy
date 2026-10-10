import { Injectable, computed, inject, signal } from '@angular/core';

import type { Schemas } from './api/schemas';
import { RuntimeConfigService } from './runtime-config.service';

// GET /features: the optional features this installation offers, set by its operator in the API's
// configuration (docs/backend/analysis/feature-flags.md). Sub-flags already follow their parent.
export type InstallationFeatures = Schemas['InstallationFeatures'];

export type FeatureName = keyof InstallationFeatures;

const ALL_ON: InstallationFeatures = {
  mealplans: true,
  mealplanAiAssistant: true,
  mealplanImport: true,
  medicines: true,
  sleepDiary: true,
  houseRules: true,
  pickups: true,
  babysitters: true,
  workLocations: true,
  printing: true,
  progress: true,
  taskLibrary: true,
  help: true,
};

export const FEATURES_LOAD_TIMEOUT_MS = 3000;

@Injectable({ providedIn: 'root' })
export class FeaturesService {
  private readonly runtimeConfig = inject(RuntimeConfigService);

  // All on until load() resolves, and after a failed load: the API enforces the flags anyway, so
  // the worst case is a page showing its usual load error, not a blank app.
  private readonly flags = signal<InstallationFeatures>(ALL_ON);

  // Whether this installation has any feature turned off (the help page says so).
  readonly someOff = computed(() => Object.values(this.flags()).some((on) => !on));

  enabled(name: FeatureName): boolean {
    return this.flags()[name];
  }

  // For registries whose entries belong to an optional feature (help topics and sections).
  offers(item: { readonly feature?: FeatureName }): boolean {
    return item.feature === undefined || this.enabled(item.feature);
  }

  // fetch, not HttpClient: this runs in an app initializer, before the router, and the endpoint is
  // anonymous -- the auth interceptor's token refresh and /login redirect have no business here.
  // The app initializer awaits this, so a stalled API gets FEATURES_LOAD_TIMEOUT_MS before the app
  // starts with every feature on, rather than a blank screen until the browser gives up.
  async load(): Promise<void> {
    const controller = new AbortController();
    const timeout = setTimeout(() => controller.abort(), FEATURES_LOAD_TIMEOUT_MS);

    try {
      const response = await fetch(`${this.runtimeConfig.apiBaseUrl}/features`, {
        signal: controller.signal,
      });

      if (!response.ok) {
        throw new Error(`${response.status} ${response.statusText}`);
      }

      this.flags.set({ ...ALL_ON, ...((await response.json()) as Partial<InstallationFeatures>) });
    } catch (error) {
      console.warn('Could not load the feature flags; showing every feature.', error);
    } finally {
      clearTimeout(timeout);
    }
  }
}
