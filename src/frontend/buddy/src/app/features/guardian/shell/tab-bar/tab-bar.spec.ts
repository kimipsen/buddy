import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { describe, expect, it } from 'vitest';

import { TabBar } from './tab-bar';
import { disableFeatures } from '../../../../../testing/features-fixture';

@Component({ template: '<app-tab-bar />', imports: [TabBar] })
class Host {}

// TranslatePipe is used unstubbed, so the labels below are the real English copy from
// core/i18n/translations/en/shell.ts.
describe('TabBar', () => {
  async function setup(url = '/guardian') {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([
          {
            path: 'guardian',
            children: [
              { path: '', component: Host },
              { path: 'calendar', component: Host },
              { path: 'mealplan', component: Host },
              { path: 'medicine', component: Host },
            ],
          },
        ]),
      ],
    });

    const harness = await RouterTestingHarness.create();
    await harness.navigateByUrl(url);
    const root = () => harness.fixture.nativeElement as HTMLElement;

    return { harness, root };
  }

  function links(root: HTMLElement) {
    return Array.from(root.querySelectorAll<HTMLAnchorElement>('nav a'));
  }

  it('links to Today, Calendar, Meals and Medicine, in that order', async () => {
    const { root } = await setup();

    expect(links(root()).map((link) => link.getAttribute('href'))).toEqual([
      '/guardian',
      '/guardian/calendar',
      '/guardian/mealplan',
      '/guardian/medicine',
    ]);
    expect(links(root()).map((link) => link.textContent?.trim())).toEqual([
      'Today',
      'Calendar',
      'Meals',
      'Medicine',
    ]);
  });

  it('drops Meals and Medicine while their features are turned off', async () => {
    disableFeatures('mealplans', 'medicines');
    const { root } = await setup();

    expect(links(root()).map((link) => link.getAttribute('href'))).toEqual([
      '/guardian',
      '/guardian/calendar',
    ]);
  });

  it('drops only the tab whose feature is off', async () => {
    disableFeatures('medicines');
    const { root } = await setup();

    expect(links(root()).map((link) => link.textContent?.trim())).toEqual([
      'Today',
      'Calendar',
      'Meals',
    ]);
  });

  it('is a labelled navigation landmark', async () => {
    const { root } = await setup();

    expect(root().querySelector('nav')?.getAttribute('aria-label')).toBe('Main');
  });

  it('marks Today as the current page on the dashboard only', async () => {
    const { root } = await setup('/guardian');

    const current = root().querySelectorAll('[aria-current="page"]');
    expect(current).toHaveLength(1);
    expect(current[0].getAttribute('href')).toBe('/guardian');
    expect(current[0].classList).toContain('font-semibold');
    expect(current[0].classList).toContain('text-emerald-700');
    expect(current[0].classList).toContain('dark:text-emerald-400');
  });

  it('marks Calendar, and not Today, as the current page on the calendar', async () => {
    const { root } = await setup('/guardian/calendar');

    const current = root().querySelectorAll('[aria-current="page"]');
    expect(current).toHaveLength(1);
    expect(current[0].getAttribute('href')).toBe('/guardian/calendar');

    const today = links(root())[0];
    expect(today.hasAttribute('aria-current')).toBe(false);
    expect(today.classList).not.toContain('font-semibold');
    expect(today.classList).not.toContain('text-emerald-700');
  });

  it('moves the current page along when the guardian navigates', async () => {
    const { harness, root } = await setup('/guardian/calendar');

    await harness.navigateByUrl('/guardian/medicine');

    const current = root().querySelectorAll('[aria-current="page"]');
    expect(current).toHaveLength(1);
    expect(current[0].getAttribute('href')).toBe('/guardian/medicine');
  });

  it('shows on phones only, sits at the bottom above the page, and is left off paper', async () => {
    const { root } = await setup();

    const nav = root().querySelector('nav')!;
    for (const name of [
      'sm:hidden',
      'print:hidden',
      'fixed',
      'inset-x-0',
      'bottom-0',
      'z-30',
      'bg-white',
      'dark:bg-slate-900',
      'pb-[env(safe-area-inset-bottom)]',
    ]) {
      expect(nav.classList, name).toContain(name);
    }
  });

  it('gives every tab a touch target taller than 44px', async () => {
    const { root } = await setup();

    for (const link of links(root())) {
      expect(link.classList).toContain('min-h-14');
    }
  });

  it('hides the decorative icons from assistive technology', async () => {
    const { root } = await setup();

    const icons = root().querySelectorAll('nav svg');
    expect(icons).toHaveLength(4);
    for (const icon of Array.from(icons)) {
      expect(icon.getAttribute('aria-hidden')).toBe('true');
      expect(icon.getAttribute('stroke')).toBe('currentColor');
      expect(icon.querySelector('path')?.namespaceURI).toBe('http://www.w3.org/2000/svg');
    }
  });
});
