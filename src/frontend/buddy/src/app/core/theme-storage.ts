import { ThemeMode, isThemeMode } from './theme';

// Keep in sync with the pre-boot theme script in index.html, which reads this same key to apply
// the right theme before Angular loads (avoiding a flash of the wrong theme).
export const THEME_STORAGE_KEY = 'buddy_theme_mode';

export function readStoredThemeMode(storage: Storage): ThemeMode | null {
  const raw = storage.getItem(THEME_STORAGE_KEY);

  return raw && isThemeMode(raw) ? raw : null;
}

export function writeStoredThemeMode(storage: Storage, mode: ThemeMode): void {
  storage.setItem(THEME_STORAGE_KEY, mode);
}
