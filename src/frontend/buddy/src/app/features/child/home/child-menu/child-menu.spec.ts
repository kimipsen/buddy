import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { describe, expect, it, vi } from 'vitest';

import { AuthService } from '../../../../core/auth.service';
import { ThemeMode } from '../../../../core/theme';
import { ThemeService } from '../../../../core/theme.service';
import { ChildMenu } from './child-menu';

// TranslatePipe/TranslationService are used unstubbed (same pattern as profile-menu.spec.ts), so
// assertions check the real English copy from core/i18n/translations/en/child.ts.
describe('ChildMenu', () => {
  async function setup(initialMode: ThemeMode = 'system') {
    const logout = vi.fn();
    const authStub: Partial<AuthService> = { logout };
    const setMode = vi.fn();
    const modeState = signal<ThemeMode>(initialMode);
    const themeStub: Partial<ThemeService> = { mode: modeState.asReadonly(), setMode };

    await TestBed.configureTestingModule({
      imports: [ChildMenu],
      providers: [
        { provide: AuthService, useValue: authStub },
        { provide: ThemeService, useValue: themeStub },
      ],
    }).compileComponents();

    const fixture = TestBed.createComponent(ChildMenu);
    fixture.detectChanges();

    return { fixture, compiled: fixture.nativeElement as HTMLElement, logout, setMode };
  }

  function fireClick(fixture: ComponentFixture<ChildMenu>, element: Element): void {
    element.dispatchEvent(new Event('click', { bubbles: true }));
    fixture.detectChanges();
  }

  function toggleButton(compiled: HTMLElement): HTMLButtonElement {
    return compiled.querySelector('button[aria-haspopup="true"]')!;
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

  it('renders closed with the toggle collapsed and no menu content', async () => {
    const { compiled } = await setup();

    const toggle = toggleButton(compiled);
    expect(toggle.getAttribute('aria-expanded')).toBe('false');
    expect(toggle.querySelector('.sr-only')?.textContent?.trim()).toBe('Open menu');
    expect(signOutButton(compiled)).toBeNull();
  });

  it('opens the menu with theme options and sign out when the toggle is clicked', async () => {
    const { fixture, compiled } = await setup();

    fireClick(fixture, toggleButton(compiled));

    expect(toggleButton(compiled).getAttribute('aria-expanded')).toBe('true');
    expect(themeButton(compiled, 'Light')).not.toBeNull();
    expect(themeButton(compiled, 'Dark')).not.toBeNull();
    expect(themeButton(compiled, 'System')).not.toBeNull();
    expect(signOutButton(compiled)).not.toBeNull();
  });

  it('closes the menu when the toggle is clicked again', async () => {
    const { fixture, compiled } = await setup();

    fireClick(fixture, toggleButton(compiled));
    expect(toggleButton(compiled).getAttribute('aria-expanded')).toBe('true');

    fireClick(fixture, toggleButton(compiled));

    expect(toggleButton(compiled).getAttribute('aria-expanded')).toBe('false');
    expect(signOutButton(compiled)).toBeNull();
  });

  it('closes the menu when the backdrop is clicked', async () => {
    const { fixture, compiled } = await setup();

    fireClick(fixture, toggleButton(compiled));
    const backdrop = compiled.querySelector('.fixed.inset-0');
    expect(backdrop).not.toBeNull();

    fireClick(fixture, backdrop!);

    expect(toggleButton(compiled).getAttribute('aria-expanded')).toBe('false');
  });

  it('closes the open menu when Escape is pressed', async () => {
    const { fixture, compiled } = await setup();

    fireClick(fixture, toggleButton(compiled));
    expect(toggleButton(compiled).getAttribute('aria-expanded')).toBe('true');

    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }));
    fixture.detectChanges();

    expect(toggleButton(compiled).getAttribute('aria-expanded')).toBe('false');
    expect(signOutButton(compiled)).toBeNull();
  });

  it('logs out and closes the menu when sign out is clicked', async () => {
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
});
