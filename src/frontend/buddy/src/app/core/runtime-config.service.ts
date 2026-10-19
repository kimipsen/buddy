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
  // Build version from git tags, baked into the image and served by its Caddyfile
  // (docs/versioning.md). Absent in local development; empty counts as absent.
  version?: string;
  // Source code link in the profile menu, so a family running its own fork can point it there.
  // Defaults to DEFAULT_REPOSITORY_URL when absent or empty (the image serves "" when the
  // REPOSITORY_URL environment variable isn't set).
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
    return nonEmpty(this.config?.version) ?? null;
  }

  get repositoryUrl(): string {
    return nonEmpty(this.config?.repositoryUrl) ?? DEFAULT_REPOSITORY_URL;
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

  // The image fills these in from environment variables, which come out as "" when unset, so an
  // empty value is as missing as an absent one.
  if (!isNonEmptyString(apiBaseUrl) || typeof keycloak !== 'object' || keycloak === null) {
    return false;
  }

  if (version !== undefined && typeof version !== 'string') {
    return false;
  }

  if (repositoryUrl !== undefined && typeof repositoryUrl !== 'string') {
    return false;
  }

  const fields = keycloak as Record<string, unknown>;

  return ['authority', 'clientId', 'realm', 'redirectPath'].every((field) =>
    isNonEmptyString(fields[field]),
  );
}

function isNonEmptyString(value: unknown): value is string {
  return typeof value === 'string' && value !== '';
}

function nonEmpty(value: string | undefined): string | undefined {
  return value === '' ? undefined : value;
}
