import { Injectable } from '@angular/core';

export interface KeycloakConfig {
  authority: string;
  clientId: string;
  realm: string;
  redirectPath: string;
}

export interface RuntimeConfig {
  keycloak: KeycloakConfig;
  apiBaseUrl: string;
}

@Injectable({ providedIn: 'root' })
export class RuntimeConfigService {
  private config: RuntimeConfig | null = null;

  get keycloak(): KeycloakConfig {
    if (!this.config) {
      throw new Error('Runtime config has not been loaded.');
    }

    return this.config.keycloak;
  }

  get apiBaseUrl(): string {
    if (!this.config) {
      throw new Error('Runtime config has not been loaded.');
    }

    return this.config.apiBaseUrl;
  }

  async load(): Promise<void> {
    const response = await fetch('/config/runtime-config.json', { cache: 'no-cache' });

    if (!response.ok) {
      throw new Error(`Unable to load runtime config: ${response.status} ${response.statusText}`);
    }

    this.config = await response.json() as RuntimeConfig;
  }
}
