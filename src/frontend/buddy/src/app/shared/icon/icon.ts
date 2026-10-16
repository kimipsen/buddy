import { Component, computed, input, linkedSignal } from '@angular/core';

import { ParsedIcon, parseIcon } from '../../core/pictograms';

// Shown when a pictogram's file can't be loaded (a name this build doesn't ship, or offline).
const FALLBACK = '🖼️';

/**
 * Any icon value: an emoji as text, or a pictogram ("pictogram:<slug>") as its SVG
 * (docs/frontend/analysis/pictograms.md, Question 3). Decorative by default, because it sits next
 * to a visible title; pass `label` where it stands alone. `inline` sizes a pictogram like the text
 * around it, `tile` at the 48px the pictograms are drawn for (child task, routine and First-Then
 * surfaces). An emoji keeps the font size the caller sets.
 */
@Component({
  selector: 'app-icon',
  templateUrl: './icon.html',
  host: { class: 'inline-flex shrink-0 items-center justify-center' },
})
export class Icon {
  readonly value = input<string | null | undefined>(null);
  readonly size = input<'inline' | 'tile'>('inline');
  readonly label = input<string | null>(null);

  private readonly failed = linkedSignal({ source: this.value, computation: () => false });

  protected readonly icon = computed<ParsedIcon | null>(() => {
    const parsed = parseIcon(this.value());
    return parsed?.kind === 'pictogram' && this.failed()
      ? { kind: 'emoji', text: FALLBACK }
      : parsed;
  });

  protected readonly imageClass = computed(() =>
    this.size() === 'tile' ? 'h-12 w-12' : 'h-[1.25em] w-[1.25em]',
  );

  protected onError(): void {
    this.failed.set(true);
  }
}
