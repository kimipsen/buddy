import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { describe, expect, it, vi } from 'vitest';

import { AuthService } from '../../../../core/auth.service';
import { RuntimeConfigService } from '../../../../core/runtime-config.service';
import { ThemeMode } from '../../../../core/theme';
import { ThemeService } from '../../../../core/theme.service';
import { ProfileMenu } from './profile-menu';
import { disableFeatures } from '../../../../../testing/features-fixture';

// TranslatePipe/TranslationService are used unstubbed throughout (the same pattern as the other
// component specs in this app), so assertions below check the real English copy from
// core/i18n/translations/en/shell.ts rather than raw translation keys.
describe('ProfileMenu', () => {
  async function setup(
    initialMode: ThemeMode = 'system',
    version: string | null = null,
    repositoryUrl = 'https://github.com/kimipsen/buddy',
  ) {
    const logout = vi.fn();
    const authStub: Partial<AuthService> = { logout };
    const setMode = vi.fn();
    const modeState = signal<ThemeMode>(initialMode);
    const themeStub: Partial<ThemeService> = { mode: modeState.asReadonly(), setMode };

    await TestBed.configureTestingModule({
      imports: [ProfileMenu],
      providers: [
        provideRouter([]),
        { provide: AuthService, useValue: authStub },
        { provide: ThemeService, useValue: themeStub },
        { provide: RuntimeConfigService, useValue: { version, repositoryUrl } },
      ],
    }).compileComponents();

    const fixture = TestBed.createComponent(ProfileMenu);
    fixture.detectChanges();

    return { fixture, compiled: fixture.nativeElement as HTMLElement, logout, setMode };
  }

  // Everything in ProfileMenu is driven by a plain signal (no promises, no HttpClient), so there's
  // no async work to wait out -- but Angular's zoneless event-listener wrapper only *schedules*
  // change detection after a handler runs rather than applying it inline, so the DOM needs an
  // explicit detectChanges() call after each dispatched event to reflect it synchronously.
  function fireClick(fixture: ComponentFixture<ProfileMenu>, element: Element): void {
    element.dispatchEvent(new Event('click', { bubbles: true }));
    fixture.detectChanges();
  }

  function toggleButton(compiled: HTMLElement): HTMLButtonElement {
    return compiled.querySelector('button[aria-haspopup="true"]')!;
  }

  function menuLink(compiled: HTMLElement, href: string): HTMLAnchorElement | null {
    return compiled.querySelector<HTMLAnchorElement>(`a[href="${href}"]`);
  }

  function signOutButton(compiled: HTMLElement): HTMLButtonElement | null {
    return (
      Array.from(compiled.querySelectorAll<HTMLButtonElement>('button')).find(
        (button) => button.textContent?.trim() === 'Sign out',
      ) ?? null
    );
  }

  function themeButton(compiled: HTMLElement, label: string): HTMLButtonElement | null {
    return (
      Array.from(compiled.querySelectorAll<HTMLButtonElement>('button[aria-pressed]')).find(
        (button) => button.textContent?.trim() === label,
      ) ?? null
    );
  }

  it('renders closed with the toggle collapsed and no menu items', async () => {
    const { compiled } = await setup();

    const toggle = toggleButton(compiled);
    expect(toggle.getAttribute('aria-expanded')).toBe('false');
    expect(toggle.querySelector('.sr-only')?.textContent?.trim()).toBe('Open account menu');
    expect(menuLink(compiled, '/guardian/mealplan')).toBeNull();
    expect(signOutButton(compiled)).toBeNull();
  });

  it('opens the menu with all links and the sign-out action when the toggle is clicked', async () => {
    const { fixture, compiled } = await setup();

    fireClick(fixture, toggleButton(compiled));

    expect(toggleButton(compiled).getAttribute('aria-expanded')).toBe('true');
    expect(menuLink(compiled, '/guardian/mealplan')?.textContent?.trim()).toBe('Meal planner');
    expect(menuLink(compiled, '/guardian/medicine')?.textContent?.trim()).toBe('Medicine');
    expect(menuLink(compiled, '/guardian/pickup')?.textContent?.trim()).toBe('Pickup & drop-off');
    expect(menuLink(compiled, '/guardian/calendar')?.textContent?.trim()).toBe('Calendar');
    expect(menuLink(compiled, '/guardian/babysitters')?.textContent?.trim()).toBe('Babysitters');
    expect(menuLink(compiled, '/guardian/admin')?.textContent?.trim()).toBe('Settings');
    expect(menuLink(compiled, '/guardian/help')?.textContent?.trim()).toBe('Help');
    expect(signOutButton(compiled)).not.toBeNull();
  });

  it('shows the build version at the bottom of the menu when the runtime config has one', async () => {
    const { fixture, compiled } = await setup('system', '1.2.0');

    fireClick(fixture, toggleButton(compiled));

    expect(compiled.querySelector('.border-t p')?.textContent?.trim()).toBe('Version 1.2.0');
  });

  it('shows no version line when the runtime config has none', async () => {
    const { fixture, compiled } = await setup();

    fireClick(fixture, toggleButton(compiled));

    expect(compiled.textContent).not.toContain('Version');
  });

  it('links to the repository from the runtime config in a new tab', async () => {
    const { fixture, compiled } = await setup('system', null, 'https://example.test/fork');

    fireClick(fixture, toggleButton(compiled));

    const link = menuLink(compiled, 'https://example.test/fork');
    expect(link?.textContent?.replace(/\s+/g, ' ').trim()).toBe('GitHub (opens in a new tab)');
    expect(link?.getAttribute('target')).toBe('_blank');
    expect(link?.getAttribute('rel')).toBe('noopener noreferrer');
  });

  it('closes the menu when the repository link is clicked', async () => {
    const { fixture, compiled } = await setup();

    fireClick(fixture, toggleButton(compiled));
    const link = menuLink(compiled, 'https://github.com/kimipsen/buddy')!;
    // Keep jsdom from attempting navigation for target="_blank".
    link.addEventListener('click', (event) => event.preventDefault());
    fireClick(fixture, link);

    expect(toggleButton(compiled).getAttribute('aria-expanded')).toBe('false');
  });

  it('closes the menu when the toggle is clicked again', async () => {
    const { fixture, compiled } = await setup();

    fireClick(fixture, toggleButton(compiled));
    expect(toggleButton(compiled).getAttribute('aria-expanded')).toBe('true');

    fireClick(fixture, toggleButton(compiled));

    expect(toggleButton(compiled).getAttribute('aria-expanded')).toBe('false');
    expect(menuLink(compiled, '/guardian/mealplan')).toBeNull();
  });

  it('closes the menu when the backdrop is clicked', async () => {
    const { fixture, compiled } = await setup();

    fireClick(fixture, toggleButton(compiled));
    const backdrop = compiled.querySelector('.fixed.inset-0');
    expect(backdrop).not.toBeNull();

    fireClick(fixture, backdrop!);

    expect(toggleButton(compiled).getAttribute('aria-expanded')).toBe('false');
  });

  it('closes the menu when a navigation link is clicked', async () => {
    const { fixture, compiled } = await setup();

    fireClick(fixture, toggleButton(compiled));
    const mealPlanLink = menuLink(compiled, '/guardian/mealplan')!;

    fireClick(fixture, mealPlanLink);

    expect(toggleButton(compiled).getAttribute('aria-expanded')).toBe('false');
    expect(menuLink(compiled, '/guardian/mealplan')).toBeNull();
  });

  it('logs out and closes the menu when sign out is clicked, without navigating away', async () => {
    const { fixture, compiled, logout } = await setup();

    fireClick(fixture, toggleButton(compiled));
    fireClick(fixture, signOutButton(compiled)!);

    expect(logout).toHaveBeenCalledTimes(1);
    expect(toggleButton(compiled).getAttribute('aria-expanded')).toBe('false');
    expect(signOutButton(compiled)).toBeNull();
  });

  it('renders a button for each theme mode, marking only the active mode as pressed', async () => {
    const { fixture, compiled } = await setup('dark');

    fireClick(fixture, toggleButton(compiled));

    expect(themeButton(compiled, 'Light')?.getAttribute('aria-pressed')).toBe('false');
    expect(themeButton(compiled, 'Dark')?.getAttribute('aria-pressed')).toBe('true');
    expect(themeButton(compiled, 'System')?.getAttribute('aria-pressed')).toBe('false');
  });

  it('switches the theme mode when a theme button is clicked, without closing the menu', async () => {
    const { fixture, compiled, setMode } = await setup('system');

    fireClick(fixture, toggleButton(compiled));
    fireClick(fixture, themeButton(compiled, 'Dark')!);

    expect(setMode).toHaveBeenCalledTimes(1);
    expect(setMode).toHaveBeenCalledWith('dark');
    expect(toggleButton(compiled).getAttribute('aria-expanded')).toBe('true');
  });

  it('keeps the open panel within a phone screen, scrolling when it is taller', async () => {
    const { fixture, compiled } = await setup();

    fireClick(fixture, toggleButton(compiled));

    const panel = themeButton(compiled, 'Light')!.closest('.absolute')!;
    expect(panel.classList).toContain('max-w-[calc(100vw-2rem)]');
    expect(panel.classList).toContain('max-h-[calc(100dvh-5rem)]');
    expect(panel.classList).toContain('overflow-y-auto');
  });

  it('hides the pointer-only backdrop from assistive tech', async () => {
    const { fixture, compiled } = await setup();

    fireClick(fixture, toggleButton(compiled));

    expect(compiled.querySelector('.fixed.inset-0')?.getAttribute('aria-hidden')).toBe('true');
  });

  it('moves focus to the first theme button when the menu opens', async () => {
    const { fixture, compiled } = await setup();

    fireClick(fixture, toggleButton(compiled));

    expect(document.activeElement).toBe(themeButton(compiled, 'Light'));
  });

  it('does not close when clicking inside the menu panel', async () => {
    const { fixture, compiled } = await setup();

    fireClick(fixture, toggleButton(compiled));
    const panel = themeButton(compiled, 'Light')!.parentElement!.parentElement!;
    fireClick(fixture, panel);

    expect(toggleButton(compiled).getAttribute('aria-expanded')).toBe('true');
  });

  it.each([
    ['Escape', () => document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }))],
    [
      'a backdrop click',
      (compiled: HTMLElement) =>
        compiled
          .querySelector('.fixed.inset-0')!
          .dispatchEvent(new Event('click', { bubbles: true })),
    ],
  ])('returns focus to the toggle after closing via %s', async (_, close) => {
    const { fixture, compiled } = await setup();

    fireClick(fixture, toggleButton(compiled));
    close(compiled);
    fixture.detectChanges();

    expect(toggleButton(compiled).getAttribute('aria-expanded')).toBe('false');
    expect(document.activeElement).toBe(toggleButton(compiled));
  });

  it('ignores Escape while the menu is closed, leaving focus where it is', async () => {
    const { fixture, compiled } = await setup();

    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }));
    fixture.detectChanges();

    expect(toggleButton(compiled).getAttribute('aria-expanded')).toBe('false');
    expect(document.activeElement).not.toBe(toggleButton(compiled));
  });

  it('leaves out the links to features that are turned off', async () => {
    disableFeatures(
      'mealplans',
      'taskLibrary',
      'medicines',
      'sleepDiary',
      'pickups',
      'progress',
      'workLocations',
      'babysitters',
      'printing',
      'help',
    );
    const { fixture, compiled } = await setup();
    fireClick(fixture, toggleButton(compiled));

    const hrefs = Array.from(compiled.querySelectorAll('a[href^="/guardian"]')).map((a) =>
      a.getAttribute('href'),
    );
    expect(hrefs).toEqual(['/guardian/calendar', '/guardian/admin']);
  });
});
