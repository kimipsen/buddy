import { FeatureName, FeaturesService } from '../../../core/features.service';

export type TabIcon = 'today' | 'calendar' | 'meals' | 'sleep';

export interface NavItem {
  readonly link: string;
  readonly label: string;
  readonly feature?: FeatureName;
  // Today is the dashboard at /guardian, which every other guardian URL starts with.
  readonly exact?: boolean;
  // Set on the pages that also get a phone tab, with the tab's shorter label.
  readonly tab?: { readonly label: string; readonly icon: TabIcon };
}

export interface NavGroup {
  readonly label: string;
  readonly items: readonly NavItem[];
}

// Every guardian page a guardian navigates to, grouped by what it is for. The page menu shows the
// groups and the phone tab bar the items with a `tab`, so a page is added in one place.
// Settings and Help are account items and live in the profile menu instead.
export const NAV_GROUPS: readonly NavGroup[] = [
  {
    label: 'shell.nav.groups.everyDay',
    items: [
      {
        link: '/guardian',
        label: 'shell.nav.items.today',
        exact: true,
        tab: { label: 'shell.tabs.today', icon: 'today' },
      },
      {
        link: '/guardian/calendar',
        label: 'shell.nav.items.calendar',
        tab: { label: 'shell.tabs.calendar', icon: 'calendar' },
      },
      {
        link: '/guardian/mealplan',
        label: 'shell.nav.items.mealPlanner',
        feature: 'mealplans',
        tab: { label: 'shell.tabs.meals', icon: 'meals' },
      },
      { link: '/guardian/pickup', label: 'shell.nav.items.pickup', feature: 'pickups' },
    ],
  },
  {
    label: 'shell.nav.groups.health',
    items: [
      { link: '/guardian/medicine', label: 'shell.nav.items.medicine', feature: 'medicines' },
      // Sleep is logged every night, so it has a tab and Medicine does not.
      {
        link: '/guardian/sleep-diary',
        label: 'shell.nav.items.sleepDiary',
        feature: 'sleepDiary',
        tab: { label: 'shell.tabs.sleep', icon: 'sleep' },
      },
      { link: '/guardian/feelings', label: 'shell.nav.items.feelings', feature: 'feelings' },
    ],
  },
  {
    label: 'shell.nav.groups.routines',
    items: [
      {
        link: '/guardian/task-library',
        label: 'shell.nav.items.taskLibrary',
        feature: 'taskLibrary',
      },
      { link: '/guardian/house-rules', label: 'shell.nav.items.houseRules', feature: 'houseRules' },
      {
        link: '/guardian/transition-warnings',
        label: 'shell.nav.items.transitionWarnings',
        feature: 'transitionWarnings',
      },
      { link: '/guardian/progress', label: 'shell.nav.items.progress', feature: 'progress' },
    ],
  },
  {
    label: 'shell.nav.groups.family',
    items: [
      {
        link: '/guardian/babysitters',
        label: 'shell.nav.items.babysitters',
        feature: 'babysitters',
      },
      {
        link: '/guardian/work-locations',
        label: 'shell.nav.items.workLocations',
        feature: 'workLocations',
      },
      { link: '/guardian/print', label: 'shell.nav.items.print', feature: 'printing' },
    ],
  },
];

// The groups with the pages of turned-off features dropped, and groups left empty dropped too.
export function visibleNavGroups(features: Pick<FeaturesService, 'offers'>): NavGroup[] {
  return NAV_GROUPS.map((group) => ({
    ...group,
    items: group.items.filter((item) => features.offers(item)),
  })).filter((group) => group.items.length > 0);
}
