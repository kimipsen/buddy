import { abortingThresholds } from '../lib/config.ts';
import { mix, setupRun, teardownRun } from '../lib/run.ts';

// Where does p95 start to bend, and what gives out first: CPU, the 50-connection pool, or Marten
// append latency? Steps of 50, 100, 200 and 300 families, 5 minutes each. A breached threshold
// stops the run and records the step it happened at.
export { childBoard, guardianMorning, guardianWrites, icalPoll } from '../journeys/index.ts';

const steps = (__ENV.STEPS || '50,100,200,300').split(',').map(Number);
const hold = __ENV.HOLD || '5m';

export const options = {
  scenarios: mix([
    ...steps.flatMap((target) => [
      { duration: '1m', target },
      { duration: hold, target },
    ]),
    { duration: '1m', target: 0 },
  ]),
  thresholds: abortingThresholds(),
  setupTimeout: '60m',
  teardownTimeout: '60m',
};

export const setup = () => setupRun(Number(__ENV.FAMILIES || Math.max(...steps)));
export const teardown = teardownRun;
