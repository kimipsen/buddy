import { defineConfig } from 'vitest/config';

// Loaded by the Angular unit-test builder (`runnerConfig` in angular.json) and merged with the
// options it sets itself.
//
// Many specs wait with settle() loops over real timers, so on a busy machine (dev servers,
// language servers, Stryker's parallel `npm test` runs) a spec can miss Vitest's 5s default even
// though it passes on its own. Half the cores for workers leaves room for everything else, and
// a 15s timeout absorbs the rest without hiding a spec that really hangs.
export default defineConfig({
  test: {
    maxWorkers: '50%',
    testTimeout: 15_000,
  },
});
