import { Component, computed, input, output } from '@angular/core';

// A curated, evenly-spaced set of hues (Tailwind's 500 shade) -- distinguishable at a glance and
// legible as both a small dot and a fill, in light and dark mode alike.
export const DEFAULT_COLOR_SWATCHES: readonly string[] = [
  '#f43f5e', // rose
  '#f97316', // orange
  '#f59e0b', // amber
  '#84cc16', // lime
  '#10b981', // emerald
  '#14b8a6', // teal
  '#0ea5e9', // sky
  '#6366f1', // indigo
  '#a855f7', // purple
  '#ec4899', // pink
];

// Replaces the native `<input type="color">` used for medicine schedules, calendars, task
// templates, and meals (see docs/frontend/analysis/visual-specification.md) -- a preset grid keeps
// every color-tagged item in the app visually consistent instead of whatever the OS color picker
// happens to default to.
@Component({
  selector: 'app-color-swatch-picker',
  imports: [],
  templateUrl: './color-swatch-picker.html',
})
export class ColorSwatchPicker {
  readonly value = input<string>('');
  readonly swatches = input<readonly string[]>(DEFAULT_COLOR_SWATCHES);
  readonly ariaLabel = input('');

  readonly valueChange = output<string>();

  // A schedule/template/meal created before this component existed (or before this exact preset
  // list) may hold a color outside the grid -- shown as an extra, already-selected swatch rather
  // than silently dropped, so opening the editor doesn't look like the color was never set.
  protected readonly allSwatches = computed(() => {
    const value = this.value();
    const swatches = this.swatches();
    return value && !swatches.includes(value) ? [...swatches, value] : swatches;
  });

  protected select(color: string): void {
    this.valueChange.emit(color);
  }
}
