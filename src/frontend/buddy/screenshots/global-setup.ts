import { request } from '@playwright/test';

import { API_URL, seedDemoFamily } from './demo-family';

// Builds a fresh demo family before any page is captured and hands its ids to the capture spec
// through the environment (Playwright workers inherit process.env from global setup).
export default async function globalSetup(): Promise<void> {
  // Flash cards are on by default and the screenshots show them, so a reused API
  // (BUDDY_SCREENSHOTS_REUSE_API=1) must not have Features__FlashCards=false.
  const api = await request.newContext({ baseURL: API_URL, ignoreHTTPSErrors: true });
  const features = (await (await api.get('/features')).json()) as { flashCards?: boolean };
  await api.dispose();

  if (features.flashCards !== true) {
    throw new Error(
      `The API at ${API_URL} has flash cards off. Remove Features__FlashCards=false and restart it.`,
    );
  }

  const demo = await seedDemoFamily();
  process.env['BUDDY_DEMO_FAMILY'] = JSON.stringify(demo);
}
