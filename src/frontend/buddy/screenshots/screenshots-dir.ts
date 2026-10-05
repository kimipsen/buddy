import { join } from 'node:path';

// docs/screenshots at the repo root; SCREENSHOTS_DIR overrides it (e.g. to compare against the
// committed set without touching it).
export const SCREENSHOTS_DIR =
  process.env['SCREENSHOTS_DIR'] ?? join(__dirname, '../../../../docs/screenshots');

// Where a Playwright project's PNGs go: desktop at the top level (the paths the docs link to),
// every other project (mobile) in a subfolder named after it.
export function screenshotsDirFor(project: string): string {
  return project === 'desktop' ? SCREENSHOTS_DIR : join(SCREENSHOTS_DIR, project);
}
