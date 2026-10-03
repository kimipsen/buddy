import { seedDemoFamily } from './demo-family';

// Builds a fresh demo family before any page is captured and hands its ids to the capture spec
// through the environment (Playwright workers inherit process.env from global setup).
export default async function globalSetup(): Promise<void> {
  const demo = await seedDemoFamily();
  process.env['BUDDY_DEMO_FAMILY'] = JSON.stringify(demo);
}
