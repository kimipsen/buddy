import { abortingThresholds, config, thresholds } from '../lib/config.ts';
import { setupRun, teardownRun } from '../lib/run.ts';

// How many dashboard loads per second does the stack handle at its current size? An open model:
// guardian-morning iterations arrive at a rate that keeps rising (by RATE_STEP per minute) until a
// threshold aborts the run. No think time, so a VU is a request source, not a person. Needs the
// load stack's per-user limit raised (task test:load PROFILE=breakpoint does this), or the
// per-user rate limiter, not the stack's capacity, ends the run.
export { guardianMorning } from '../journeys/index.ts';

config.thinkTime = false;

const step = Number(__ENV.RATE_STEP || 5);
const minutes = Number(__ENV.MINUTES || 30);

export const options = {
  scenarios: {
    guardianMorning: {
      executor: 'ramping-arrival-rate',
      exec: 'guardianMorning',
      startRate: 1,
      timeUnit: '1s',
      preAllocatedVUs: 50,
      maxVUs: Number(__ENV.MAX_VUS || 1000),
      stages: Array.from({ length: minutes }, (_, i) => ({
        duration: '1m',
        target: (i + 1) * step,
      })),
    },
  },
  // Only the journey this profile runs; the other journeys' thresholds would have no data.
  thresholds: abortingThresholds({
    http_req_failed: thresholds.http_req_failed,
    'http_req_duration{kind:read}': thresholds['http_req_duration{kind:read}'],
    'group_duration{group:::guardian-morning}':
      thresholds['group_duration{group:::guardian-morning}'],
    server_errors: thresholds.server_errors,
  }),
  setupTimeout: '30m',
  teardownTimeout: '30m',
};

export const setup = () => setupRun(Number(__ENV.FAMILIES || 100));
export const teardown = teardownRun;
