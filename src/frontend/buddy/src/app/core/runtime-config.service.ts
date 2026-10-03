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

    const config: unknown = await response.json();

    if (!isRuntimeConfig(config)) {
      throw new Error(
        'Runtime config is missing keycloak.authority/realm/clientId/redirectPath or apiBaseUrl.',
      );
    }

    this.config = config;
  }
}

export function isRuntimeConfig(value: unknown): value is RuntimeConfig {
  if (typeof value !== 'object' || value === null) {
    return false;
  }

  const { keycloak, apiBaseUrl } = value as Record<string, unknown>;

  if (typeof apiBaseUrl !== 'string' || typeof keycloak !== 'object' || keycloak === null) {
    return false;
  }

  const fields = keycloak as Record<string, unknown>;

  return ['authority', 'clientId', 'realm', 'redirectPath'].every(
    (field) => typeof fields[field] === 'string',
  );
}
