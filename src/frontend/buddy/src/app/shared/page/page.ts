import { Component, computed, input } from '@angular/core';
import { RouterLink } from '@angular/router';

export type PageWidth = '3xl' | '4xl' | '5xl' | '7xl' | 'wide';

// `wide` (96rem, 1536px) is for the dashboard only, so it has room for three columns of cards.
const MAX_WIDTH: Record<PageWidth, string> = {
  '3xl': 'max-w-3xl',
  '4xl': 'max-w-4xl',
  '5xl': 'max-w-5xl',
  '7xl': 'max-w-7xl',
  wide: 'max-w-[96rem]',
};

/**
 * The guardian page container: centred, capped at `max-w-7xl` (or another `width`), with
 * responsive side padding (`px-4` on a phone, `sm:px-6`, `lg:px-8`) and an optional "back" link
 * above the content. Page headers differ too much between pages to fold in, so each page keeps
 * its own eyebrow and title as content.
 */
@Component({
  selector: 'app-page',
  imports: [RouterLink],
  templateUrl: './page.html',
  host: { class: 'block' },
})
export class Page {
  readonly width = input<PageWidth>('7xl');
  /** Route of the back link. No link is shown without one. */
  readonly backLink = input<string>();
  /** Translated text of the back link. */
  readonly backLabel = input('');

  protected readonly maxWidth = computed(() => MAX_WIDTH[this.width()]);
}
