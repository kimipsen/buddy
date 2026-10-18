import {
  Component,
  ElementRef,
  HostListener,
  Injector,
  OnInit,
  afterNextRender,
  computed,
  inject,
  input,
  output,
  signal,
  viewChild,
} from '@angular/core';

import { Rule, RuleContent, workingContent } from '../../../../core/house-rules.service';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { MarkdownEditor } from '../../../../shared/markdown-editor/markdown-editor';
import { Toggle } from '../../../../shared/toggle/toggle';

export const MAX_TITLE_LENGTH = 100;
export const MAX_BODY_LENGTH = 4000;

export interface RuleEditorResult {
  content: RuleContent;
  requireReacknowledgement: boolean;
  // Publish right after saving, rather than leaving a draft to refine.
  publish: boolean;
}

// The add/edit dialog: a plain-text title, the markdown body, and -- when editing a published rule --
// the "small fix" switch that keeps the children's acknowledgements (house-rules.md, Question 3).
// Saving writes a draft the children don't see; "Save and publish" also publishes it
// (house-rules-publishing.md). The parent owns saving; this only collects the input and reports it.
@Component({
  selector: 'app-rule-editor',
  imports: [MarkdownEditor, Toggle, TranslatePipe],
  templateUrl: './rule-editor.html',
})
export class RuleEditor implements OnInit {
  private readonly injector = inject(Injector);

  /** The rule being edited, or null for a new one. */
  readonly rule = input<Rule | null>(null);
  readonly saving = input(false);
  /** An i18n key, or the API's message, shown above the buttons. */
  readonly error = input<string | null>(null);

  readonly save = output<RuleEditorResult>();
  readonly closed = output<void>();

  private readonly panel = viewChild<ElementRef<HTMLElement>>('panel');
  private readonly titleInput = viewChild<ElementRef<HTMLInputElement>>('titleInput');

  protected readonly title = signal('');
  protected readonly body = signal('');
  protected readonly minorFix = signal(false);
  protected readonly maxTitleLength = MAX_TITLE_LENGTH;
  protected readonly maxBodyLength = MAX_BODY_LENGTH;

  protected readonly isEdit = computed(() => this.rule() !== null);
  // A rule the children have never seen is always new to them, so the switch would mean nothing.
  protected readonly canBeMinorFix = computed(() => this.rule()?.isPublished === true);
  protected readonly canSave = computed(
    () =>
      !this.saving() &&
      this.title().trim().length > 0 &&
      this.title().length <= MAX_TITLE_LENGTH &&
      this.body().length <= MAX_BODY_LENGTH,
  );

  ngOnInit(): void {
    const rule = this.rule();
    const content = rule ? workingContent(rule) : { title: '', body: '' };
    this.title.set(content.title);
    this.body.set(content.body);
    this.minorFix.set(rule?.draft ? !rule.draft.requiresReacknowledgement : false);
    afterNextRender(() => this.titleInput()?.nativeElement.focus(), { injector: this.injector });
  }

  protected onTitleInput(event: Event): void {
    this.title.set((event.target as HTMLInputElement).value);
  }

  protected submit(publish = false): void {
    if (!this.canSave()) {
      return;
    }

    this.save.emit({
      content: { title: this.title().trim(), body: this.body() },
      requireReacknowledgement: !(this.canBeMinorFix() && this.minorFix()),
      publish,
    });
  }

  protected close(): void {
    if (!this.saving()) {
      this.closed.emit();
    }
  }

  @HostListener('document:keydown.escape')
  protected onEscape(): void {
    this.close();
  }

  // aria-modal promises the page behind is inert, so keep Tab / Shift+Tab inside the dialog.
  @HostListener('document:keydown.tab', ['$event'])
  @HostListener('document:keydown.shift.tab', ['$event'])
  protected onTab(event: Event): void {
    const panel = this.panel()?.nativeElement;

    if (!panel || !(event instanceof KeyboardEvent)) {
      return;
    }

    const focusable: HTMLElement[] = Array.from(
      panel.querySelectorAll<HTMLElement>(
        'input:not(:disabled), textarea:not(:disabled), button:not(:disabled)',
      ),
    );
    const first = focusable.at(0) ?? panel;
    const last = focusable.at(-1) ?? panel;
    const active = document.activeElement as HTMLElement | null;
    const inside = active !== null && focusable.includes(active);

    if (!inside || active === (event.shiftKey ? first : last)) {
      event.preventDefault();
      (event.shiftKey ? last : first).focus();
    }
  }
}
