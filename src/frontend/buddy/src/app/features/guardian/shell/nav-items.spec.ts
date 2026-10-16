import { describe, expect, it } from 'vitest';

import { FeatureName } from '../../../core/features.service';
import { GUARDIAN_ROUTES } from '../guardian.routes';
import { NAV_GROUPS, visibleNavGroups } from './nav-items';

// Reached from the profile menu (settings, help) or from inside another page.
const NOT_IN_NAV = ['onboarding', 'admin', 'help'];

describe('NAV_GROUPS', () => {
  const links = NAV_GROUPS.flatMap((group) => group.items.map((item) => item.link));

  it('links to every top-level guardian page, and to nothing else', () => {
    const pages = GUARDIAN_ROUTES.flatMap((route) => route.children ?? [])
      .map((route) => route.path!)
      .filter((path) => !path.includes('/') && !NOT_IN_NAV.includes(path))
      .map((path) => (path === '' ? '/guardian' : `/guardian/${path}`));

    expect([...links].sort()).toEqual([...pages].sort());
  });

  it('lists each page once', () => {
    expect(new Set(links).size).toBe(links.length);
  });

  it('groups the pages as Every day, Health, Routines, then Family & helpers', () => {
    expect(NAV_GROUPS.map((group) => group.label)).toEqual([
      'shell.nav.groups.everyDay',
      'shell.nav.groups.health',
      'shell.nav.groups.routines',
      'shell.nav.groups.family',
    ]);
  });
});

describe('visibleNavGroups', () => {
  function offering(...disabled: FeatureName[]) {
    return {
      offers: (item: { readonly feature?: FeatureName }) =>
        item.feature === undefined || !disabled.includes(item.feature),
    };
  }

  it('keeps every group and page while every feature is on', () => {
    expect(visibleNavGroups(offering())).toEqual(NAV_GROUPS);
  });

  it('drops the pages of turned-off features and keeps the rest of their group', () => {
    const health = visibleNavGroups(offering('medicines')).find(
      (group) => group.label === 'shell.nav.groups.health',
    );

    expect(health?.items.map((item) => item.link)).toEqual([
      '/guardian/sleep-diary',
      '/guardian/feelings',
      '/guardian/visit-summaries',
    ]);
  });

  it('drops a group whose pages are all turned off', () => {
    const groups = visibleNavGroups(
      offering('medicines', 'sleepDiary', 'feelings', 'visitSummaries'),
    );

    expect(groups.map((group) => group.label)).toEqual([
      'shell.nav.groups.everyDay',
      'shell.nav.groups.routines',
      'shell.nav.groups.family',
    ]);
  });
});
