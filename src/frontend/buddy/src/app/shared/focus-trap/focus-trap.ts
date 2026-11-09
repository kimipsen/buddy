// The focusable controls of a dialog panel that Tab cycles through.
export const DIALOG_FOCUSABLE =
  'select:not(:disabled), input:not(:disabled), textarea:not(:disabled), button:not(:disabled)';

/**
 * Keeps Tab / Shift+Tab inside a modal panel: aria-modal promises the page behind is inert. Call it
 * from the dialog's `document:keydown.tab` and `document:keydown.shift.tab` host listeners. The
 * browser moves focus between the panel's enabled controls itself; this takes over at either edge
 * and whenever focus is anywhere else (the panel itself, a just-disabled control, or outside it).
 */
export function keepTabInside(
  panel: HTMLElement | undefined,
  event: Event,
  selector: string = DIALOG_FOCUSABLE,
): void {
  if (!panel || !(event instanceof KeyboardEvent)) {
    return;
  }

  const focusable = Array.from(panel.querySelectorAll<HTMLElement>(selector));
  const first = focusable.at(0) ?? panel;
  const last = focusable.at(-1) ?? panel;
  const active = document.activeElement as HTMLElement | null;
  const inside = active !== null && focusable.includes(active);

  if (!inside || active === (event.shiftKey ? first : last)) {
    event.preventDefault();
    (event.shiftKey ? last : first).focus();
  }
}
