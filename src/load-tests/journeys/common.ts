import { sleep } from 'k6';
import exec from 'k6/execution';

import { config } from '../lib/config.ts';
import type { SeededFamily } from '../lib/seed.ts';

// What setup() hands every VU.
export interface RunData {
  userPrefix: string;
  families: SeededFamily[];
}

// Each VU stands for one family, so "N VUs" reads as "N families active at once". VUs share the
// seeded families by index when there are more VUs than families.
export function familyFor(data: RunData): SeededFamily {
  return data.families[(exec.vu.idInTest - 1) % data.families.length];
}

export function pick<T>(items: readonly T[]): T {
  return items[Math.floor(Math.random() * items.length)];
}

// A person, not a busy loop: 3-10 s between actions (off with THINK_TIME=0 for breakpoint).
export function think(min = 3, max = 10): void {
  if (config.thinkTime) {
    sleep(min + Math.random() * (max - min));
  }
}
