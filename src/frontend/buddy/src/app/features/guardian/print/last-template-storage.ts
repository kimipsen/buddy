// The last template used on this device -- a convenience, not configuration, so it isn't synced
// (same lightweight approach as theme-storage.ts). Storage can be unavailable (private mode,
// blocked site data), so every access is guarded and failure just means "no preference".
const KEY = 'buddy_print_last_template';

export function readLastTemplateId(): string | null {
  try {
    return localStorage.getItem(KEY);
  } catch {
    return null;
  }
}

export function writeLastTemplateId(templateId: string): void {
  try {
    localStorage.setItem(KEY, templateId);
  } catch {
    // Not persisted; the picker just won't preselect it next time.
  }
}
