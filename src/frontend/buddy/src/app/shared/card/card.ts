import { Component } from '@angular/core';

/**
 * The guardian card: a white, bordered box with responsive padding (`p-4` on a phone, `p-6` from
 * `sm` up). The classes sit on the host, so a caller's own classes (`class="mb-6"`, a flex
 * layout) merge with them. The host is a plain element, like the unnamed `<section>` it replaces;
 * a card that is a landmark adds `role="region"` and `aria-labelledby` itself.
 */
@Component({
  selector: 'app-card',
  templateUrl: './card.html',
  host: {
    class:
      'block rounded-lg border border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-900 p-4 shadow-sm sm:p-6',
  },
})
export class Card {}
