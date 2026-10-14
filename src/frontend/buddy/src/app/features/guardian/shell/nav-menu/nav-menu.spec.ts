import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { describe, expect, it } from 'vitest';

import { NavMenu } from './nav-menu';
import { disableFeatures } from '../../../../../testing/features-fixture';

@Component({ template: '<app-nav-menu />', imports: [NavMenu] })
class Host {}

// TranslatePipe is used unstubbed, so the labels below are the real English copy from
// core/i18n/translations/en/shell.ts.
describe('NavMenu', () => {
  async function setup(url = '/guardian') {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([
          {
            path: 'guardian',
            children: [
              { path: '', component: Host },
              { path: 'calendar', component: Host },
              { path: 'medicine', component: Host },
            ],
          },
        ]),
      ],
    });

    const harness = await RouterTestingHarness.create();
    await harness.navigateByUrl(url);
    const root = () => harness.fixture.nativeElement as HTMLElement;
    const toggle = () => root().querySelector<HTMLButtonElement>('button[aria-haspopup="true"]')!;
    const click = (element: Element) => {
      element.dispatchEvent(new Event('click', { bubbles: true }));
      harness.fixture.detectChanges();
    };

    return { harness, root, toggle, click };
  }

  function groups(root: HTMLElement) {
    return Array.from(root.querySelectorAll('nav ul')).map((list) => ({
      heading: document.getElementById(list.getAttribute('aria-labelledby')!)?.textContent?.trim(),
      links: Array.from(list.querySelectorAll('a')).map((link) => link.textContent?.trim()),
    }));
  }

  it('renders closed, with a toggle named Menu', async () => {
    const { root, toggle } = await setup();

    expect(toggle().getAttribute('aria-expanded')).toBe('false');
    expect(toggle().textContent?.trim()).toBe('Menu');
    expect(root().querySelector('nav')).toBeNull();
  });

  it('keeps the toggle named on phones, where only its icon shows', async () => {
    const { toggle } = await setup();

    expect(toggle().querySelector('span')?.classList).toContain('max-sm:sr-only');
    expect(toggle().querySelector('svg')?.getAttribute('aria-hidden')).toBe('true');
  });

  it('opens a labelled navigation with every page under its group heading', async () => {
    const { root, toggle, click } = await setup();

    click(toggle());

    expect(toggle().getAttribute('aria-expanded')).toBe('true');
    expect(root().querySelector('nav')?.getAttribute('aria-label')).toBe('Pages');
    expect(groups(root())).toEqual([
      {
        heading: 'Every day',
        links: ['Today', 'Calendar', 'Meal planner', 'Pickup & drop-off'],
      },
      { heading: 'Health & well-being', links: ['Medicine', 'Sleep diary', 'Feelings'] },
      {
        heading: 'Routines',
        links: ['Task library', 'House rules', 'Heads-up before changes', 'Progress goals'],
      },
      {
        heading: 'Family & helpers',
        links: ['Babysitters', 'Work locations', 'Print week plan'],
      },
    ]);
  });

  it('leaves out turned-off features, and a group with nothing left in it', async () => {
    disableFeatures('medicines', 'sleepDiary', 'feelings', 'pickups');
    const { root, toggle, click } = await setup();

    click(toggle());

    expect(groups(root()).map((group) => group.heading)).toEqual([
      'Every day',
      'Routines',
      'Family & helpers',
    ]);
    expect(groups(root())[0].links).toEqual(['Today', 'Calendar', 'Meal planner']);
  });

  it('marks only the current page, matching Today exactly', async () => {
    const { harness, root, toggle, click } = await setup('/guardian/calendar');

    click(toggle());
    // routerLinkActive marks the links once the router state reaches them, a tick later.
    await harness.fixture.whenStable();
    harness.fixture.detectChanges();

    const current = root().querySelectorAll('[aria-current="page"]');
    expect(current).toHaveLength(1);
    expect(current[0].getAttribute('href')).toBe('/guardian/calendar');
    expect(current[0].classList).toContain('font-semibold');
    expect(current[0].classList).toContain('dark:text-emerald-300');
  });

  it('moves focus to the first page when it opens', async () => {
    const { root, toggle, click } = await setup();

    click(toggle());

    expect(document.activeElement).toBe(root().querySelector('nav a'));
  });

  it('closes when a page is picked, and navigates there', async () => {
    const { harness, root, toggle, click } = await setup();

    click(toggle());
    // A real left click (MouseEvent, button 0): RouterLink ignores other clicks.
    root().querySelector<HTMLAnchorElement>('a[href="/guardian/medicine"]')!.click();
    await harness.fixture.whenStable();
    harness.fixture.detectChanges();

    expect(toggle().getAttribute('aria-expanded')).toBe('false');
    expect(root().querySelector('nav')).toBeNull();
    expect(TestBed.inject(Router).url).toBe('/guardian/medicine');
  });

  it('closes again when the toggle is clicked a second time', async () => {
    const { root, toggle, click } = await setup();

    click(toggle());
    click(toggle());

    expect(toggle().getAttribute('aria-expanded')).toBe('false');
    expect(root().querySelector('nav')).toBeNull();
  });

  it.each([
    [
      'Escape',
      () => {
        document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }));
      },
    ],
    [
      'a click on the pointer-only backdrop',
      (root: HTMLElement) => {
        const backdrop = root.querySelector('.fixed.inset-0')!;
        expect(backdrop.getAttribute('aria-hidden')).toBe('true');
        backdrop.dispatchEvent(new Event('click', { bubbles: true }));
      },
    ],
  ])('closes on %s and hands focus back to the toggle', async (_, close) => {
    const { harness, root, toggle, click } = await setup();

    click(toggle());
    close(root());
    harness.fixture.detectChanges();

    expect(toggle().getAttribute('aria-expanded')).toBe('false');
    expect(document.activeElement).toBe(toggle());
  });

  it('ignores Escape while closed', async () => {
    const { harness, toggle } = await setup();

    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }));
    harness.fixture.detectChanges();

    expect(document.activeElement).not.toBe(toggle());
  });

  it('fits the open panel on a phone screen, scrolling when it is taller', async () => {
    const { root, toggle, click } = await setup();

    click(toggle());

    const panel = root().querySelector('nav')!;
    expect(panel.classList).toContain('max-w-[calc(100vw-2rem)]');
    expect(panel.classList).toContain('max-h-[calc(100dvh-5rem)]');
    expect(panel.classList).toContain('overflow-y-auto');
  });
});
