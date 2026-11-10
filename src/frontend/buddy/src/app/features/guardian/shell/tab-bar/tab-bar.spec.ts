import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { afterEach, describe, expect, it } from 'vitest';

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
              { path: 'sleep-diary', component: Host },
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

  it('links to Today, Calendar, Meals and Sleep, in that order', async () => {
    const { root } = await setup();

    expect(links(root()).map((link) => link.getAttribute('href'))).toEqual([
      '/guardian',
      '/guardian/calendar',
      '/guardian/mealplan',
      '/guardian/sleep-diary',
    ]);
    expect(links(root()).map((link) => link.textContent?.trim())).toEqual([
      'Today',
      'Calendar',
      'Meals',
      'Sleep',
    ]);
  });

  it('drops Meals and Sleep while their features are turned off', async () => {
    disableFeatures('mealplans', 'sleepDiary');
    const { root } = await setup();

    expect(links(root()).map((link) => link.getAttribute('href'))).toEqual([
      '/guardian',
      '/guardian/calendar',
    ]);
  });

  it('drops only the tab whose feature is off', async () => {
    disableFeatures('sleepDiary');
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

    await harness.navigateByUrl('/guardian/sleep-diary');

    const current = root().querySelectorAll('[aria-current="page"]');
    expect(current).toHaveLength(1);
    expect(current[0].getAttribute('href')).toBe('/guardian/sleep-diary');
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
      'pl-[env(safe-area-inset-left)]',
      'pr-[env(safe-area-inset-right)]',
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

  describe('while the guardian types', () => {
    const hosts: HTMLElement[] = [];
    afterEach(() => hosts.splice(0).forEach((host) => host.remove()));

    function field(html: string): HTMLElement {
      const host = document.createElement('div');
      host.innerHTML = html;
      document.body.appendChild(host);
      hosts.push(host);
      return host.firstElementChild as HTMLElement;
    }

    function focusIn(target: Element) {
      target.dispatchEvent(new FocusEvent('focusin', { bubbles: true }));
    }

    function focusOut(target: Element, next: Element | null) {
      target.dispatchEvent(new FocusEvent('focusout', { bubbles: true, relatedTarget: next }));
    }

    async function hidden(harness: RouterTestingHarness, root: () => HTMLElement) {
      await harness.fixture.whenStable();
      return root().querySelector('nav')!.classList.contains('pointer-coarse:hidden');
    }

    it('is shown on touch screens until a field has focus', async () => {
      const { harness, root } = await setup();

      expect(await hidden(harness, root)).toBe(false);
    });

    for (const html of [
      '<input type="text" />',
      '<input />',
      '<input type="time" />',
      '<textarea></textarea>',
      '<select></select>',
    ]) {
      it(`hides on touch screens while ${html} has the keyboard up`, async () => {
        const { harness, root } = await setup();
        const input = field(html);

        focusIn(input);

        expect(await hidden(harness, root)).toBe(true);
      });
    }

    for (const html of [
      '<input type="checkbox" />',
      '<input type="radio" />',
      '<input type="range" />',
      '<input type="color" />',
      '<input type="file" />',
      '<input type="button" />',
      '<input type="submit" />',
      '<input type="reset" />',
      '<input type="image" />',
      '<button type="button"></button>',
    ]) {
      it(`stays when ${html}, which brings up no keyboard, has focus`, async () => {
        const { harness, root } = await setup();

        focusIn(field(html));

        expect(await hidden(harness, root)).toBe(false);
      });
    }

    it('starts hidden when a field already has focus as the bar appears', async () => {
      field('<input type="text" />').focus();

      const { harness, root } = await setup();

      expect(await hidden(harness, root)).toBe(true);
    });

    it('starts shown when the focused element brings up no keyboard', async () => {
      field('<button type="button"></button>').focus();

      const { harness, root } = await setup();

      expect(await hidden(harness, root)).toBe(false);
    });

    it('comes back when focus leaves the field', async () => {
      const { harness, root } = await setup();
      const input = field('<input type="text" />');
      focusIn(input);

      focusOut(input, null);

      expect(await hidden(harness, root)).toBe(false);
    });

    it('comes back when focus moves on to a button', async () => {
      const { harness, root } = await setup();
      const input = field('<input type="text" />');
      focusIn(input);

      focusOut(input, field('<button type="button"></button>'));

      expect(await hidden(harness, root)).toBe(false);
    });

    it('stays away while focus moves from one field to the next', async () => {
      const { harness, root } = await setup();
      const first = field('<input type="text" />');
      focusIn(first);

      focusOut(first, field('<textarea></textarea>'));

      expect(await hidden(harness, root)).toBe(true);
    });
  });
});
