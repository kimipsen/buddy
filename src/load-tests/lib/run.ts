import { check } from 'k6';
import http from 'k6/http';

import type { RunData } from '../journeys/common.ts';
import { serverErrors } from './checks.ts';
import { assertAllowedTarget, config } from './config.ts';
import { cleanupFamilies, seedFamilies } from './seed.ts';

// setup() and teardown() shared by every profile, plus the weighted journey mix.

function ready(): boolean {
  const response = http.get(`${config.baseUrl}/health/ready`, {
    tags: { name: '/health/ready', kind: 'seed' },
  });
  return check(response, { 'API is ready (/health/ready 200)': (r) => r.status === 200 });
}

export function setupRun(families = config.families): RunData {
  assertAllowedTarget();

  if (!ready()) {
    throw new Error(
      `${config.baseUrl}/health/ready is not 200; is the stack up? (task load:stack:up)`,
    );
  }

  // The prefix travels with the data: setup and teardown run in separate VU instances, and without
  // RUN_ID each one would derive its own.
  return { userPrefix: config.userPrefix, families: seedFamilies(families) };
}

// The stack must still be healthy after the run; a failed readiness check counts as a server
// error, which fails the server_errors threshold. The 53300 log check and the container restart
// check need Docker, so `task test:load` does those after k6 exits.
export function teardownRun(data: RunData): void {
  if (!ready()) {
    serverErrors.add(1, { name: '/health/ready' });
  }

  if (config.cleanup) {
    cleanupFamilies(data.families, data.userPrefix);
  }
}

type Stage = { duration: string; target: number };

// The journey weights from load-testing-k6.md, "Workload model".
const weights = {
  guardianMorning: 0.5,
  childBoard: 0.25,
  guardianWrites: 0.2,
  icalPoll: 0.05,
} as const;

// One ramping-vus scenario per journey, each with its share of every stage's target (at least
// one VU while the target is above zero, so every journey runs even at low levels).
export function mix(stages: Stage[], startTime = '0s') {
  return Object.fromEntries(
    Object.entries(weights).map(([exec, weight]) => [
      exec,
      {
        executor: 'ramping-vus',
        exec,
        startTime,
        startVUs: 0,
        stages: stages.map(({ duration, target }) => ({
          duration,
          target: target === 0 ? 0 : Math.max(1, Math.round(target * weight)),
        })),
        gracefulRampDown: '30s',
      },
    ]),
  );
}
