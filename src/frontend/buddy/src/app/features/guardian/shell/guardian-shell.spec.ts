import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { AuthService } from '../../../core/auth.service';
import { TranslationService } from '../../../core/i18n/translation.service';
import { GUARDIAN_ROUTES } from '../guardian.routes';
import { GuardianShell } from './guardian-shell';
import { disableFeatures } from '../../../../testing/features-fixture';

@Component({ template: '<p>page</p>' })
class Page {}

// The shell is the header (brand, profile menu, the current page's help button) plus the outlet.
// The help topic comes from the active page route's `data.helpTopic`.
describe('GuardianShell', () => {
  // jsdom has no layout, so scrollIntoView doesn't exist; record which elements were scrolled to.
  let scrolled: Element[];

  beforeEach(() => {
    scrolled = [];
    Element.prototype.scrollIntoView = vi.fn(function (this: Element) {
      scrolled.push(this);
    });
  });

  afterEach(() => {
    delete (Element.prototype as Partial<Element>).scrollIntoView;
  });

  async function setup(url: string) {
    const authStub: Partial<AuthService> = { logout: vi.fn() };

    TestBed.configureTestingModule({
      providers: [
        provideRouter([
          {
            path: '',
            component: GuardianShell,
            children: [
              { path: 'calendar', component: Page, data: { helpTopic: 'calendar' } },
              { path: 'medicine', component: Page, data: { helpTopic: 'medicine' } },
              { path: 'plain', component: Page },
              { path: 'onboarding', component: Page, data: { hideTabBar: true } },
            ],
          },
        ]),
        { provide: AuthService, useValue: authStub },
      ],
    });

    const harness = await RouterTestingHarness.create();
    await harness.navigateByUrl(url);
    const root = () => harness.fixture.nativeElement as HTMLElement;

    return { harness, root };
  }

  function helpButton(root: HTMLElement) {
    return root.querySelector<HTMLButtonElement>('button[aria-controls="page-help"]');
  }

  function topicTitle(id: string) {
    return TestBed.inject(TranslationService).translate(`help.topics.${id}.title`);
  }

  it('renders the brand header, the profile menu, and the page', async () => {
    const { root } = await setup('/plain');

    expect(root().textContent).toContain('Buddy');
    expect(root().textContent).toContain('Guardian dashboard');
    expect(root().querySelector('a[href="/guardian"]')).toBeTruthy();
    expect(root().querySelector('app-profile-menu')).toBeTruthy();
    expect(root().textContent).toContain('page');
  });

  it('shows no help button on a page without a help topic', async () => {
    const { root } = await setup('/plain');

    expect(helpButton(root())).toBeNull();
  });

  it('toggles the current page’s help panel from a labelled disclosure button', async () => {
    const { harness, root } = await setup('/calendar');

    const button = helpButton(root())!;
    expect(button.textContent).toContain('Help for this page');
    expect(button.getAttribute('aria-expanded')).toBe('false');
    expect(root().querySelector('app-help-panel')).toBeNull();

    button.click();
    harness.detectChanges();

    expect(button.getAttribute('aria-expanded')).toBe('true');
    expect(root().querySelector('#page-help h2')?.textContent).toContain(topicTitle('calendar'));

    button.click();
    harness.detectChanges();

    expect(button.getAttribute('aria-expanded')).toBe('false');
    expect(root().querySelector('app-help-panel')).toBeNull();
  });

  it('keeps the header, with its help button and profile menu, at the top while the page scrolls', async () => {
    const { root } = await setup('/calendar');

    const header = root().querySelector('header')!;
    expect(header.classList).toContain('sticky');
    expect(header.classList).toContain('top-0');
    expect(header.classList).toContain('bg-white');
    expect(header.classList).toContain('dark:bg-slate-900');
  });

  it('scrolls the help panel into view when it opens, and not when it closes', async () => {
    const { harness, root } = await setup('/calendar');

    helpButton(root())!.click();
    harness.detectChanges();

    expect(scrolled).toHaveLength(1);
    expect(scrolled[0].querySelector('#page-help')).not.toBeNull();

    helpButton(root())!.click();
    harness.detectChanges();

    expect(scrolled).toHaveLength(1);
  });

  it('closes the panel and switches topic when another page opens', async () => {
    const { harness, root } = await setup('/calendar');
    helpButton(root())!.click();
    harness.detectChanges();

    await harness.navigateByUrl('/medicine');

    expect(root().querySelector('app-help-panel')).toBeNull();
    helpButton(root())!.click();
    harness.detectChanges();
    expect(root().querySelector('#page-help h2')?.textContent).toContain(topicTitle('medicine'));
  });

  function pressEscape() {
    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }));
  }

  it('closes on Escape from the help button and keeps focus there', async () => {
    const { harness, root } = await setup('/calendar');
    helpButton(root())!.focus();
    helpButton(root())!.click();
    harness.detectChanges();

    pressEscape();
    harness.detectChanges();

    expect(root().querySelector('app-help-panel')).toBeNull();
    expect(document.activeElement).toBe(helpButton(root()));
  });

  it('closes on Escape from inside the panel and returns focus to the help button', async () => {
    const { harness, root } = await setup('/calendar');
    helpButton(root())!.click();
    harness.detectChanges();
    root().querySelector<HTMLAnchorElement>('#page-help a')!.focus();

    pressEscape();
    harness.detectChanges();

    expect(root().querySelector('app-help-panel')).toBeNull();
    expect(document.activeElement).toBe(helpButton(root()));
  });

  it('leaves the panel open on Escape while focus is elsewhere on the page', async () => {
    const { harness, root } = await setup('/calendar');
    helpButton(root())!.click();
    harness.detectChanges();
    const elsewhere = root().querySelector<HTMLAnchorElement>('a[href="/guardian"]')!;
    elsewhere.focus();

    pressEscape();
    harness.detectChanges();

    expect(root().querySelector('app-help-panel')).not.toBeNull();
    expect(document.activeElement).toBe(elsewhere);
  });

  it('closes from the panel’s close button and returns focus to the help button', async () => {
    const { harness, root } = await setup('/calendar');
    helpButton(root())!.click();
    harness.detectChanges();

    const close = Array.from(root().querySelectorAll('#page-help button')).find(
      (button) => button.textContent?.trim() === 'Close help',
    ) as HTMLButtonElement;
    close.click();
    harness.detectChanges();

    expect(root().querySelector('app-help-panel')).toBeNull();
    expect(document.activeElement).toBe(helpButton(root()));
  });

  it('renders the phone tab bar below the page, with room left for it on phones', async () => {
    const { root } = await setup('/plain');

    expect(root().querySelector('app-tab-bar nav')).toBeTruthy();
    const outlet = root().querySelector('app-guardian-shell router-outlet')!;
    expect(outlet.parentElement!.classList).toContain('max-sm:pb-20');
    expect(outlet.parentElement!.textContent).toContain('page');
  });

  it('leaves the tab bar off a page whose route hides it, and brings it back on the next page', async () => {
    const { harness, root } = await setup('/onboarding');

    expect(root().querySelector('app-tab-bar')).toBeNull();
    expect(root().textContent).toContain('page');

    await harness.navigateByUrl('/calendar');

    expect(root().querySelector('app-tab-bar')).toBeTruthy();
  });

  it('hides the tab bar on the real onboarding route', () => {
    const shell = GUARDIAN_ROUTES.find((route) => route.component === GuardianShell)!;
    const onboarding = shell.children!.find((route) => route.path === 'onboarding')!;
    const others = shell.children!.filter((route) => route.path !== 'onboarding');

    expect(onboarding.data?.['hideTabBar']).toBe(true);
    expect(others.filter((route) => route.data?.['hideTabBar'])).toEqual([]);
  });

  it('shows no help button anywhere while help is turned off', async () => {
    disableFeatures('help');
    const { root } = await setup('/calendar');

    expect(helpButton(root())).toBeNull();
  });
});
