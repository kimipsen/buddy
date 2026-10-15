// Whether the child's calendar shows the meal plan on this device -- a view preference, not
// configuration, so it isn't synced (same lightweight approach as theme-storage.ts). Storage can
// be unavailable (private mode, blocked site data), so every access is guarded and failure just
// means the default: meals shown.
const KEY = 'buddy_child_calendar_show_meals';

export function readShowMeals(): boolean {
  try {
    return localStorage.getItem(KEY) !== 'false';
  } catch {
    return true;
  }
}

export function writeShowMeals(show: boolean): void {
  try {
    localStorage.setItem(KEY, String(show));
  } catch {
    // Not persisted; the calendar just starts with meals shown next time.
  }
}
