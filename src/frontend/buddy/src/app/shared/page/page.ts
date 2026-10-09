import { Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';

export type PageWidth = '3xl' | '4xl' | '5xl' | '7xl';

/**
 * The guardian page container: centred, capped at `max-w-7xl` (or a narrower `width`), with
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
}
