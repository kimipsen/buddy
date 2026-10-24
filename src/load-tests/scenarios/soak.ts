import { thresholds } from '../lib/config.ts';
import { mix, setupRun, teardownRun } from '../lib/run.ts';

// The load level for 2 hours (HOLD). Do memory, the connection count or latency creep up? Do
// event streams grow long enough to slow rehydration? Combine with SEED_HISTORY_DAYS to start
// from families with long histories. Tokens are refreshed as they near expiry (auth.ts).
export { childBoard, guardianMorning, guardianWrites, icalPoll } from '../journeys/index.ts';

const vus = Number(__ENV.VUS || 50);

export const options = {
  scenarios: mix([
    { duration: '5m', target: vus },
    { duration: __ENV.HOLD || '2h', target: vus },
    { duration: '2m', target: 0 },
  ]),
  thresholds,
  setupTimeout: '60m',
  teardownTimeout: '30m',
};

export const setup = () => setupRun(Number(__ENV.FAMILIES || vus));
export const teardown = teardownRun;
