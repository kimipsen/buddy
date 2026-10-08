import { readFileSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';

import { deferOnboarding } from './onboarding-api';
import { SEEDED_GUARDIANS } from './seeded-users';

const RUNTIME_CONFIG_PATH = join(__dirname, '../../public/config/runtime-config.json');
const BACKUP_PATH = `${RUNTIME_CONFIG_PATH}.e2e-backup`;

// Wherever `npx playwright test` runs -- inside the devcontainer's app service (task
// test:e2e:frontend) or directly on a bare CI runner (.github/workflows/e2e-tests.yml) -- both the
// test runner's own Node process AND the browser it launches run there too, not on a developer's
// host machine. Keycloak and Mailpit are sibling containers: reachable by hostname from inside the
// devcontainer, but only via their published localhost ports from a bare runner. The checked-in
// runtime-config.json's localhost URLs are correct for a real developer's *host* browser (reached
// through forwarded ports) but wrong for both of those, so swap in the right values for the
// duration of the run and restore the original afterward (global-teardown.ts).
export default async function globalSetup(): Promise<void> {
  const original = readFileSync(RUNTIME_CONFIG_PATH, 'utf-8');
  writeFileSync(BACKUP_PATH, original);

  const config = JSON.parse(original) as { keycloak: { authority: string } };
  config.keycloak.authority = process.env['CI'] ? 'http://localhost:9080' : 'http://keycloak:8080';
  writeFileSync(RUNTIME_CONFIG_PATH, `${JSON.stringify(config, null, 2)}\n`);

  // On a fresh database the seeded guardians have no groups or children either, so the app would
  // send them into the guided setup; every spec using them expects the dashboard (runs after the
  // webServer entries have started, so the API is up).
  for (const guardian of SEEDED_GUARDIANS) {
    await deferOnboarding(guardian);
  }
}
