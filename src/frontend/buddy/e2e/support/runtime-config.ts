import { readFileSync } from 'node:fs';
import { join } from 'node:path';

// Read straight from the same file the app fetches at runtime (public/config/runtime-config.json)
// so Keycloak authority/realm/client-id used by e2e helpers can never drift from what the app uses.
export interface RuntimeConfig {
  keycloak: {
    authority: string;
    realm: string;
    clientId: string;
    redirectPath: string;
  };
  apiBaseUrl: string;
}

let cached: RuntimeConfig | null = null;

export function readRuntimeConfig(): RuntimeConfig {
  cached ??= JSON.parse(
    readFileSync(join(__dirname, '../../public/config/runtime-config.json'), 'utf-8'),
  ) as RuntimeConfig;

  return cached;
}
