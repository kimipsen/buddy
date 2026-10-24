import { config, thresholds } from '../lib/config.ts';
import { setupRun, teardownRun } from '../lib/run.ts';

// Do the scripts still match the API? Every journey runs a few times with 2 seeded families and
// every check must pass. Runs before every other profile, and on pull requests.
export { childBoard, guardianMorning, guardianWrites, icalPoll } from '../journeys/index.ts';

const once = (exec: string) => ({
  executor: 'per-vu-iterations',
  exec,
  vus: 1,
  iterations: 2,
  maxDuration: '3m',
});

export const options = {
  scenarios: {
    guardianMorning: once('guardianMorning'),
    guardianWrites: once('guardianWrites'),
    childBoard: once('childBoard'),
    icalPoll: once('icalPoll'),
  },
  // Correctness only. Eight iterations, often against an API that has just started (the PR job
  // starts one), are too few and too cold for the latency thresholds to mean anything.
  thresholds: {
    checks: ['rate==1'],
    http_req_failed: thresholds.http_req_failed,
    server_errors: thresholds.server_errors,
  },
  setupTimeout: '5m',
  teardownTimeout: '5m',
};

export const setup = () => setupRun(Number(__ENV.FAMILIES || 2));
export const teardown = teardownRun;

// Short and quiet by default: a smoke run doesn't need people-like pauses.
config.thinkTime = __ENV.THINK_TIME === '1';
