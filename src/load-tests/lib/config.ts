// Everything a run can be told from outside, read once from k6's __ENV. The defaults are the
// buddy-load Compose stack (docker-compose.load.yml), where k6 runs on the same network as the API.
// See docs/backend/analysis/load-testing-k6.md.

const env = (name: string, fallback: string): string => __ENV[name] || fallback;

export const config = {
  baseUrl: env('BASE_URL', 'http://api:8080').replace(/\/$/, ''),
  keycloakUrl: env('KEYCLOAK_URL', 'http://keycloak:8080').replace(/\/$/, ''),
  realm: 'buddy',
  // The public SPA client: it has directAccessGrantsEnabled, the same flow the e2e loginAs fixture
  // and the screenshot demo family use (.devcontainer/keycloak/buddy-realm.json).
  clientId: 'buddy-frontend',
  keycloakAdmin: {
    username: env('KEYCLOAK_ADMIN_USER', 'admin'),
    password: env('KEYCLOAK_ADMIN_PASSWORD', 'admin'),
  },
  // Every seeded identity starts with this prefix, so a run on the dev stack can find and remove
  // its own users and never touches alice/bob/carol, the e2e users or the demo family.
  userPrefix: `load.${env('RUN_ID', Date.now().toString(36))}`,
  password: 'load-test-pw-not-a-secret',
  families: Number(env('FAMILIES', '10')),
  historyDays: Number(env('SEED_HISTORY_DAYS', '0')),
  timeZone: 'Europe/Copenhagen',
  // 0 turns think time off (breakpoint); otherwise a VU waits 3-10 s between actions like a person.
  thinkTime: env('THINK_TIME', '1') !== '0',
  // Remove the seeded users and their data in teardown. On by default; run.sh turns it off on the
  // load stack (CLEANUP=0), which stack.sh down throws away anyway.
  cleanup: env('CLEANUP', '1') !== '0',
};

// A run never goes anywhere it wasn't explicitly allowed to: the load stack, the local dev API, or
// a host named in ALLOW_TARGET. Production is not on the list (load-testing-k6.md, "Environment").
const allowedHosts = [
  'api',
  'localhost',
  '127.0.0.1',
  ...(__ENV.ALLOW_TARGET ? [__ENV.ALLOW_TARGET] : []),
];

export function assertAllowedTarget(): void {
  const host = /^https?:\/\/([^/:]+)/.exec(config.baseUrl)?.[1] ?? '';

  if (!allowedHosts.includes(host)) {
    throw new Error(
      `BASE_URL host '${host}' is not an allowed load-test target (${allowedHosts.join(', ')}). ` +
        'Set ALLOW_TARGET=<host> to run against it on purpose.',
    );
  }
}

// The pass/fail contract shared by every profile (load-testing-k6.md, "Thresholds"). The numbers
// are the proposed starting values; calibration runs replace them.
export const thresholds: Record<string, string[]> = {
  http_req_failed: ['rate<0.005'],
  'http_req_duration{kind:read}': ['p(95)<300', 'p(99)<800'],
  'http_req_duration{kind:write}': ['p(95)<500', 'p(99)<1200'],
  'http_req_duration{kind:ical}': ['p(95)<800'],
  'group_duration{group:::guardian-morning}': ['p(95)<1500'],
  checks: ['rate>0.99'],
  server_errors: ['count==0'],
};

// The same thresholds, but a breach stops the run (stress, breakpoint): it records where the
// stack gave out instead of hammering a dying stack for the rest of the stages.
export function abortingThresholds(
  base: Record<string, string[]> = thresholds,
): Record<string, { threshold: string; abortOnFail: boolean; delayAbortEval: string }[]> {
  return Object.fromEntries(
    Object.entries(base).map(([metric, rules]) => [
      metric,
      rules.map((threshold) => ({ threshold, abortOnFail: true, delayAbortEval: '30s' })),
    ]),
  );
}
