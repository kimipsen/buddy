// Every page the documentation shows. To add a screenshot, add an entry here and re-run
// `task docs:screenshots`; the PNG and its section in docs/screenshots/README.md are generated.
//
// src/app/screenshot-coverage.spec.ts fails when an app route has neither an entry here nor a
// reason in UNCAPTURED_ROUTES, so a new page can't ship without its screenshot.
//
// No imports on purpose: that unit spec imports this file, and it must not pull in Node or
// Playwright code.

// What the demo seed (demo-family.ts) hands to the capture spec.
export interface DemoFamily {
  guardian: { username: string; password: string };
  child: { username: string; password: string };
  printTemplateId: string;
  groupInviteToken: string | null;
  guardianInviteToken: string | null;
  sleepDiaryShareToken: string | null;
}

export interface ScreenshotPage {
  // File name (without .png) under docs/screenshots/, also the test title.
  name: string;
  title: string;
  description: string;
  // Who is signed in: the demo guardian (Sara), the demo child (Emil), or nobody.
  as: 'guardian' | 'child' | 'anonymous';
  // The route pattern this page covers, exactly as the app's route config spells it out in full
  // (e.g. '/guardian/print/templates/:templateId'). Opened as-is unless `path` is given.
  route: string;
  // The URL to open when `route` has parameters, built from the seeded demo data. Returning null
  // skips the page (e.g. no invite token could be read from Mailpit).
  path?: (demo: DemoFamily) => string | null;
  // Text that must be visible before capturing, so the screenshot isn't taken mid-load.
  waitFor?: string;
  // Capture the whole scrollable page instead of just the viewport (default true).
  fullPage?: boolean;
}

export const SCREENSHOT_PAGES: readonly ScreenshotPage[] = [
  {
    name: 'login',
    title: 'Login',
    description: 'The sign-in page. Authentication is handled by Keycloak.',
    as: 'anonymous',
    route: '/login',
  },
  {
    name: 'login-session-expired',
    title: 'Login after an expired session',
    description:
      'Where an expired session lands: the sign-in page with a notice. Signing in returns to the page that was interrupted.',
    as: 'anonymous',
    route: '/login',
    path: () => '/login?reason=session-expired',
    waitFor: 'Your session expired',
  },
  {
    name: 'guardian-dashboard',
    title: 'Guardian dashboard',
    description: 'Today at a glance: meals, tasks, events, medicine, pickups and the children.',
    as: 'guardian',
    route: '/guardian',
    waitFor: 'Emil',
  },
  {
    name: 'guardian-onboarding',
    title: 'Guided setup',
    description:
      'The first-login guide for a guardian with no groups or children: group, children, other adults, a shared calendar, a first routine and a meal plan, one step at a time. Sara already has a group, so step 1 also offers to continue with it.',
    as: 'guardian',
    route: '/guardian/onboarding',
    waitFor: 'Use Holm family',
  },
  {
    name: 'guardian-calendar',
    title: 'Calendar',
    description: 'Shared family calendars with events and tasks.',
    as: 'guardian',
    route: '/guardian/calendar',
  },
  {
    name: 'guardian-task-library',
    title: 'Task library',
    description: 'Reusable routines broken into small, timed steps.',
    as: 'guardian',
    route: '/guardian/task-library',
    waitFor: 'Morning routine',
  },
  {
    name: 'guardian-progress',
    title: 'Progress',
    description: 'Completed tasks earn progress towards rewards the guardian sets up.',
    as: 'guardian',
    route: '/guardian/progress',
  },
  {
    name: 'guardian-mealplan',
    title: 'Meal plan',
    description: "The week's meals per slot, picked from the child's meal library.",
    as: 'guardian',
    route: '/guardian/mealplan',
    waitFor: 'Chicken tacos',
  },
  {
    name: 'guardian-mealplan-ai-assistant',
    title: 'Meal plan AI assistant',
    description:
      'Plan meals with an AI provider of your choice (needs an API key), optionally only from rated or recently served meals.',
    as: 'guardian',
    route: '/guardian/mealplan/ai-assistant',
    waitFor: 'Start a new session',
  },
  {
    name: 'guardian-mealplan-import',
    title: 'Import older meal plans',
    description:
      'Paste a note or upload a CSV of earlier plans, review the matched meals, and undo an import.',
    as: 'guardian',
    route: '/guardian/mealplan/import',
    waitFor: '2025-03-02 – 2025-03-08 · 7 days',
  },
  {
    name: 'guardian-medicine',
    title: 'Medicine',
    description: 'Medicine schedules and daily dose tracking.',
    as: 'guardian',
    route: '/guardian/medicine',
    waitFor: 'Methylphenidate',
  },
  {
    name: 'guardian-sleep-diary',
    title: 'Sleep diary',
    description:
      'Log a night in the same fields as a sleep clinic’s form, review the last 14 nights, keep the diary-wide sleep hygiene notes and share a read-only link with a doctor.',
    as: 'guardian',
    route: '/guardian/sleep-diary',
    waitFor: '9 h 25 min',
  },
  {
    name: 'guardian-pickup',
    title: 'Pickup & drop-off',
    description: 'Who takes and fetches the child each day.',
    as: 'guardian',
    route: '/guardian/pickup',
  },
  {
    name: 'guardian-babysitters',
    title: 'Babysitters',
    description:
      'Saved babysitters and nannies that any of a child’s guardians can plan for a pickup or drop-off.',
    as: 'guardian',
    route: '/guardian/babysitters',
    waitFor: 'Freja',
  },
  {
    name: 'guardian-work-locations',
    title: 'Work locations',
    description: 'Where each guardian works on which weekday, as a repeating pattern.',
    as: 'guardian',
    route: '/guardian/work-locations',
    waitFor: 'Home office',
  },
  {
    name: 'guardian-print',
    title: 'Print templates',
    description: 'Templates for a printable week plan.',
    as: 'guardian',
    route: '/guardian/print',
    waitFor: 'Edit template',
  },
  {
    name: 'guardian-print-template-editor',
    title: 'Print template editor',
    description: 'Choose which rows the printed week plan shows.',
    as: 'guardian',
    route: '/guardian/print/templates/:templateId',
    path: (demo) => `/guardian/print/templates/${demo.printTemplateId}`,
  },
  {
    name: 'guardian-print-sheet',
    title: 'Printable week plan',
    description: 'The week plan as it comes out on paper.',
    as: 'guardian',
    route: '/guardian/print/sheet/:templateId',
    path: (demo) => `/guardian/print/sheet/${demo.printTemplateId}?qr=1`,
  },
  {
    name: 'guardian-admin',
    title: 'Settings',
    description: 'Profile, children, groups, calendars and sharing.',
    as: 'guardian',
    route: '/guardian/admin',
    waitFor: 'Your profile',
  },
  {
    name: 'guardian-help',
    title: 'Help',
    description:
      'Every help topic on one page. The "?" button in the header opens the same help inline for the current page.',
    as: 'guardian',
    route: '/guardian/help',
    waitFor: 'How Buddy works',
  },
  {
    name: 'child-home',
    title: 'Child: today',
    description: "The child's own view of today's tasks and events.",
    as: 'child',
    route: '/child',
  },
  {
    name: 'child-mealplan',
    title: 'Child: meal plan',
    description: "The child's view of the week's meals.",
    as: 'child',
    route: '/child/mealplan',
  },
  {
    name: 'child-calendar',
    title: 'Child: calendar',
    description: "The child's calendar.",
    as: 'child',
    route: '/child/calendar',
  },
  {
    name: 'invite-group',
    title: 'Group invite',
    description: 'What someone invited to a group sees before signing in.',
    as: 'anonymous',
    route: '/invite/:token',
    path: (demo) => (demo.groupInviteToken ? `/invite/${demo.groupInviteToken}` : null),
  },
  {
    name: 'invite-guardian',
    title: 'Guardian invite',
    description: 'What an invited co-guardian sees before signing in.',
    as: 'anonymous',
    route: '/guardian-invite/:token',
    path: (demo) =>
      demo.guardianInviteToken ? `/guardian-invite/${demo.guardianInviteToken}` : null,
  },
  {
    name: 'shared-sleep-diary',
    title: 'Shared sleep diary',
    description:
      'What a doctor sees from a share link, with no login: a printable 14-day table laid out like the clinic’s paper sleep registration.',
    as: 'anonymous',
    route: '/shared/sleep-diary/:token',
    path: (demo) =>
      demo.sleepDiaryShareToken ? `/shared/sleep-diary/${demo.sleepDiaryShareToken}` : null,
    waitFor: 'Sleep diary for Emil',
  },
];

// Routes deliberately left out of the screenshots, each with the reason. Prefer adding a page
// above; only list a route here when it can't be captured with demo data.
export const UNCAPTURED_ROUTES: Readonly<Record<string, string>> = {
  '/verify-email/:token': 'Needs a fresh verification token from a real email-change flow.',
};
