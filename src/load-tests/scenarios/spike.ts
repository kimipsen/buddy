import { capacityThresholds } from '../lib/config.ts';
import { mix, setupRun, teardownRun } from '../lib/run.ts';

// The 07:00 school-morning rush: 0 to 200 families in 30 s, hold 3 minutes, back to 0. Does the
// API recover without 53300, without leaked connections and without a restart? Unlike the other
// profiles, every VU gets a fresh token from Keycloak when it starts, because a rush is also a
// burst of logins.
export { childBoard, guardianMorning, guardianWrites, icalPoll } from '../journeys/index.ts';

const peak = Number(__ENV.VUS || 200);

export const options = {
  scenarios: mix([
    { duration: '30s', target: peak },
    { duration: __ENV.HOLD || '3m', target: peak },
    { duration: '30s', target: 0 },
    // Quiet afterwards, so the recovery is visible on the dashboards before teardown checks health.
    { duration: '1m', target: 0 },
  ]),
  thresholds: capacityThresholds,
  setupTimeout: '60m',
  teardownTimeout: '60m',
};

export const setup = () => {
  const data = setupRun(Number(__ENV.FAMILIES || peak));

  // Mark the seeded tokens expired: each VU then refreshes its own on its first request
  // (auth.ts bearer()), so the token requests land in the measured window.
  for (const family of data.families) {
    family.tokens.expiresAt = 0;
    family.children.forEach((child) => (child.tokens.expiresAt = 0));
  }

  return data;
};
export const teardown = teardownRun;
