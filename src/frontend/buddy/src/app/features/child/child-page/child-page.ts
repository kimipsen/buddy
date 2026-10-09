import { Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';

/**
 * The frame every child page shares: the warm background, a header with the page title, and the
 * content column. The header starts with a back link to the child home when `backLabel` is set,
 * and with the "B" logo otherwise (the home page itself). Header controls (week navigation, the
 * child menu) are projected with the `childPageActions` attribute.
 */
@Component({
  selector: 'app-child-page',
  imports: [RouterLink],
  templateUrl: './child-page.html',
  host: { class: 'block' },
})
export class ChildPage {
  /** Translated page title, shown as the page's h1. */
  readonly title = input.required<string>();
  /** Translated back-link text. Without it the header shows the logo instead. */
  readonly backLabel = input<string>();
}
