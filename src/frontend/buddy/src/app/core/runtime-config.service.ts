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
  // Build version from git tags, written by the Dockerfile at deploy time (docs/versioning.md).
  // Absent in local development.
  version?: string;
  // Source code link in the profile menu, so a family running its own fork can point it there.
  // Defaults to DEFAULT_REPOSITORY_URL.
  repositoryUrl?: string;
}

export const DEFAULT_REPOSITORY_URL = 'https://github.com/kimipsen/buddy';

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

  // Cosmetic, so null (rather than a throw) both before load() and when the config has none.
  get version(): string | null {
    return this.config?.version ?? null;
  }

  get repositoryUrl(): string {
    return this.config?.repositoryUrl ?? DEFAULT_REPOSITORY_URL;
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

  const { keycloak, apiBaseUrl, version, repositoryUrl } = value as Record<string, unknown>;

  if (typeof apiBaseUrl !== 'string' || typeof keycloak !== 'object' || keycloak === null) {
    return false;
  }

  if (version !== undefined && typeof version !== 'string') {
    return false;
  }

  if (repositoryUrl !== undefined && typeof repositoryUrl !== 'string') {
    return false;
  }

  const fields = keycloak as Record<string, unknown>;

  return ['authority', 'clientId', 'realm', 'redirectPath'].every(
    (field) => typeof fields[field] === 'string',
  );
}
