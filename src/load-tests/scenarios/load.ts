import { thresholds } from '../lib/config.ts';
import { mix, setupRun, teardownRun } from '../lib/run.ts';

// Does a normal busy morning meet the thresholds? Ramp to the expected peak (VUS, default 50
// families active at once), hold it for HOLD (default 10m), ramp down.
export { childBoard, guardianMorning, guardianWrites, icalPoll } from '../journeys/index.ts';

const vus = Number(__ENV.VUS || 50);

export const options = {
  scenarios: mix([
    { duration: '2m', target: vus },
    { duration: __ENV.HOLD || '10m', target: vus },
    { duration: '1m', target: 0 },
  ]),
  thresholds,
  setupTimeout: '30m',
  teardownTimeout: '30m',
};

export const setup = () => setupRun(Number(__ENV.FAMILIES || vus));
export const teardown = teardownRun;
