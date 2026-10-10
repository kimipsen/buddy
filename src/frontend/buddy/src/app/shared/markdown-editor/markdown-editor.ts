import {
  Component,
  ElementRef,
  Injector,
  afterNextRender,
  computed,
  inject,
  input,
  model,
  signal,
  viewChild,
} from '@angular/core';

import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { TranslationService } from '../../core/i18n/translation.service';
import { MarkdownView } from '../markdown-view/markdown-view';
import { SegmentedControl, SegmentedControlOption } from '../segmented-control/segmented-control';
import { MarkdownAction, applyMarkdownAction } from './markdown-edits';

type Mode = 'write' | 'preview';

interface ToolbarButton {
  action: MarkdownAction;
  labelKey: string;
  symbol: string;
}

const TOOLBAR: readonly ToolbarButton[] = [
  { action: 'bold', labelKey: 'common.markdown.bold', symbol: 'B' },
  { action: 'bulletList', labelKey: 'common.markdown.bulletList', symbol: '•' },
  { action: 'numberedList', labelKey: 'common.markdown.numberedList', symbol: '1.' },
  { action: 'checkList', labelKey: 'common.markdown.checkList', symbol: '☐' },
  { action: 'table', labelKey: 'common.markdown.table', symbol: '▦' },
  { action: 'link', labelKey: 'common.markdown.link', symbol: '🔗' },
];

// A plain <textarea> for markdown with a Write/Preview switch, a small toolbar that inserts syntax
// and a character counter -- deliberately not WYSIWYG (docs/backend/analysis/house-rules.md,
// Question 4). The preview is the same markdown-view every reader sees.
@Component({
  selector: 'app-markdown-editor',
  imports: [MarkdownView, SegmentedControl, TranslatePipe],
  templateUrl: './markdown-editor.html',
})
export class MarkdownEditor {
  private readonly translation = inject(TranslationService);
  private readonly injector = inject(Injector);

  readonly value = model('');
  readonly inputId = input.required<string>();
  /** Translated label for the textarea. */
  readonly label = input.required<string>();
  /** Translated hint shown under the label, tied to the textarea with aria-describedby. */
  readonly hint = input('');
  readonly maxLength = input(4000);

  private readonly textarea = viewChild<ElementRef<HTMLTextAreaElement>>('textarea');

  protected readonly toolbar = TOOLBAR;
  protected readonly mode = signal<Mode>('write');
  protected readonly modeOptions = computed((): SegmentedControlOption<Mode>[] => [
    { value: 'write', label: this.translation.translate('common.markdown.write') },
    { value: 'preview', label: this.translation.translate('common.markdown.preview') },
  ]);
  protected readonly length = computed(() => this.value().length);
  protected readonly overLimit = computed(() => this.length() > this.maxLength());

  protected setMode(mode: Mode): void {
    this.mode.set(mode);
  }

  protected onInput(event: Event): void {
    this.value.set((event.target as HTMLTextAreaElement).value);
  }

  protected apply(action: MarkdownAction): void {
    const textarea = this.textarea()?.nativeElement;
    const selection = textarea
      ? { start: textarea.selectionStart, end: textarea.selectionEnd }
      : { start: this.value().length, end: this.value().length };
    const edit = applyMarkdownAction(action, this.value(), selection, [
      this.translation.translate('common.markdown.tableHeaderDay'),
      this.translation.translate('common.markdown.tableHeaderTime'),
    ]);

    this.value.set(edit.text);
    afterNextRender(
      () => {
        const element = this.textarea()?.nativeElement;
        element?.focus();
        element?.setSelectionRange(edit.selection.start, edit.selection.end);
      },
      { injector: this.injector },
    );
  }
}
