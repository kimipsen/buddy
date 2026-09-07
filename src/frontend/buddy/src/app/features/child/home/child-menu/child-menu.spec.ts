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
        { provide: ThemeService, useValue: themeStub }
      ]
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
    return Array.from(compiled.querySelectorAll<HTMLButtonElement>('button')).find(
      (button) => button.textContent?.trim() === 'Sign out'
    ) ?? null;
  }

  function themeButton(compiled: HTMLElement, label: string): HTMLButtonElement | null {
    return Array.from(compiled.querySelectorAll<HTMLButtonElement>('button[aria-pressed]')).find(
      (button) => button.textContent?.trim() === label
    ) ?? null;
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
});
