import { Injectable, computed, signal } from '@angular/core';

import { DEFAULT_THEME_MODE, ThemeMode } from './theme';
import { readStoredThemeMode, writeStoredThemeMode } from './theme-storage';

@Injectable({ providedIn: 'root' })
export class ThemeService {
  private readonly media = window.matchMedia('(prefers-color-scheme: dark)');
  private readonly systemPrefersDark = signal(this.media.matches);

  private readonly modeState = signal<ThemeMode>(readStoredThemeMode(localStorage) ?? DEFAULT_THEME_MODE);
  readonly mode = this.modeState.asReadonly();

  // What App's effect actually applies to <html>: "system" resolves against the live OS
  // preference, "light"/"dark" override it outright.
  readonly isDark = computed(() => {
    const mode = this.modeState();
    return mode === 'system' ? this.systemPrefersDark() : mode === 'dark';
  });

  constructor() {
    this.media.addEventListener('change', (event) => this.systemPrefersDark.set(event.matches));
  }

  setMode(mode: ThemeMode): void {
    this.modeState.set(mode);
    writeStoredThemeMode(localStorage, mode);
  }
}
