// The Mealie shopping list last used on this device -- a convenience, not family state (same
// approach as last-template-storage.ts). Storage can be unavailable (private mode, blocked site
// data), so every access is guarded and failure just means "no preference".
const KEY = 'buddy_mealie_shopping_list';

export function readLastShoppingListId(): string | null {
  try {
    return localStorage.getItem(KEY);
  } catch {
    return null;
  }
}

export function writeLastShoppingListId(listId: string): void {
  try {
    localStorage.setItem(KEY, listId);
  } catch {
    // Not persisted; the picker just won't preselect it next time.
  }
}
