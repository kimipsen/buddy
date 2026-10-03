import { join } from 'node:path';

// docs/screenshots at the repo root; SCREENSHOTS_DIR overrides it (e.g. to compare against the
// committed set without touching it).
export const SCREENSHOTS_DIR =
  process.env['SCREENSHOTS_DIR'] ?? join(__dirname, '../../../../docs/screenshots');
