import { check, sleep } from 'k6';
import http from 'k6/http';
import { Counter } from 'k6/metrics';

import { bearer } from '../lib/auth.ts';
import { serverErrors } from '../lib/checks.ts';
import { config } from '../lib/config.ts';
import { setupRun, teardownRun } from '../lib/run.ts';
import type { RunData } from '../journeys/common.ts';

// One user sending requests as fast as one VU can, against the production limits (rate-limiting.md:
// a 200-token bucket refilled at 5 per second). The limiter must answer 429 rate_limited with a
// Retry-After, never a 5xx, and the user must get a 200 again once the bucket has refilled.

const rateLimited = new Counter('rate_limited_responses');

export const options = {
  scenarios: {
    flood: { executor: 'constant-vus', exec: 'flood', vus: 1, duration: __ENV.DURATION || '30s' },
    recovery: {
      executor: 'per-vu-iterations',
      exec: 'recovery',
      vus: 1,
      iterations: 1,
      startTime: __ENV.RECOVERY_AT || '75s',
    },
  },
  thresholds: {
    rate_limited_responses: ['count>0'],
    server_errors: ['count==0'],
    checks: ['rate==1'],
  },
  setupTimeout: '5m',
  teardownTimeout: '5m',
};

export const setup = (): RunData => setupRun(1);
export const teardown = teardownRun;

const url = `${config.baseUrl}/users/me/children`;
const params = (data: RunData) => ({
  headers: {
    Authorization: `Bearer ${bearer(data.families[0].guardian, data.families[0].tokens)}`,
  },
  tags: { name: '/users/me/children', kind: 'rate-limit' },
  // 429 is the expected answer here, not a failure.
  responseCallback: http.expectedStatuses(200, 429),
});

export function flood(data: RunData): void {
  const response = http.get(url, params(data));

  if (response.status >= 500 || response.status === 0) {
    serverErrors.add(1, { name: '/users/me/children' });
  }

  if (response.status === 429) {
    rateLimited.add(1);
  }

  check(response, {
    'answered 200 or 429': (r) => r.status === 200 || r.status === 429,
    '429 carries Retry-After and rate_limited': (r) =>
      r.status !== 429 ||
      (Boolean(r.headers['Retry-After']) && String(r.body).includes('rate_limited')),
  });
}

// The bucket refills at 5 tokens per second, so well after the flood the user is served again.
export function recovery(data: RunData): void {
  sleep(2);
  check(http.get(url, params(data)), {
    'served again after the bucket refilled': (r) => r.status === 200,
  });
}
