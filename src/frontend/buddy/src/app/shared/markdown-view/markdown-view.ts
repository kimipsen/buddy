import { NgTemplateOutlet } from '@angular/common';
import { Component, computed, input } from '@angular/core';

import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { parseMarkdown } from './markdown';

// Renders a guardian's markdown (a house rule's body) as the allow-listed subset in markdown.ts.
// Every value reaches the DOM through text interpolation or a bound attribute -- never innerHTML --
// so a <script> or javascript: link in the source shows up as literal text. The one place markdown
// appears, so the guardian page, the child page and the print pages look the same.
@Component({
  selector: 'app-markdown-view',
  imports: [NgTemplateOutlet, TranslatePipe],
  templateUrl: './markdown-view.html',
})
export class MarkdownView {
  readonly markdown = input.required<string>();

  protected readonly blocks = computed(() => parseMarkdown(this.markdown()));
}
