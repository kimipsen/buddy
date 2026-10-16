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
import {
  PICTOGRAMS,
  parseIcon,
  pictogramIcon,
  pictogramNameKey,
  PictogramSlug,
} from '../../core/pictograms';
import { Icon } from '../icon/icon';

interface PictogramOption {
  readonly slug: PictogramSlug;
  readonly value: string;
  readonly name: string;
}

let nextId = 0;

/**
 * An icon field: the emoji text input every form already had, plus a "Pictures" button that opens
 * a searchable grid of the bundled pictograms (docs/frontend/analysis/pictograms.md, Question 4).
 * A chosen pictogram replaces the input with its picture and name and a remove button. The input
 * keeps the caller's `name`, `label`, `placeholder` and classes, so the surrounding form and its
 * specs see the same field as before.
 */
@Component({
  selector: 'app-icon-picker',
  imports: [Icon, TranslatePipe],
  templateUrl: './icon-picker.html',
  host: { class: 'relative inline-flex items-center gap-1', '(keydown.escape)': 'close(true)' },
})
export class IconPicker {
  private readonly translation = inject(TranslationService);
  private readonly injector = inject(Injector);

  readonly value = model('');
  readonly name = input<string | null>(null);
  // The input's accessible name, already translated; ignored when `inputId` has a <label for>.
  readonly label = input<string | null>(null);
  readonly inputId = input<string | null>(null);
  readonly placeholder = input('');
  readonly inputClass = input(
    'w-16 rounded-lg border border-slate-300 dark:border-slate-600 px-2 py-1.5 text-center text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500 dark:bg-slate-800 dark:text-slate-100',
  );

  protected readonly panelId = `icon-picker-${nextId++}`;
  protected readonly open = signal(false);
  protected readonly query = signal('');

  private readonly trigger = viewChild<ElementRef<HTMLButtonElement>>('trigger');
  private readonly search = viewChild<ElementRef<HTMLInputElement>>('search');

  protected readonly pictogramNameKey = computed(() => {
    const parsed = parseIcon(this.value());
    return parsed?.kind === 'pictogram' ? pictogramNameKey(parsed.slug) : null;
  });

  private readonly options = computed<PictogramOption[]>(() => {
    // Read so the names follow a language switch.
    this.translation.language();
    return PICTOGRAMS.map((slug) => ({
      slug,
      value: pictogramIcon(slug),
      name: this.translation.translate(pictogramNameKey(slug)),
    })).sort((a, b) => a.name.localeCompare(b.name, this.translation.language()));
  });

  protected readonly matches = computed(() => {
    const query = this.query().trim().toLocaleLowerCase();
    return query
      ? this.options().filter(
          (option) =>
            option.name.toLocaleLowerCase().includes(query) || option.slug.includes(query),
        )
      : this.options();
  });

  protected toggle(): void {
    if (this.open()) {
      this.close(false);
      return;
    }

    this.query.set('');
    this.open.set(true);
    afterNextRender(() => this.search()?.nativeElement.focus(), { injector: this.injector });
  }

  protected close(returnFocus: boolean): void {
    if (!this.open()) {
      return;
    }

    this.open.set(false);
    if (returnFocus) {
      this.trigger()?.nativeElement.focus();
    }
  }

  protected pick(option: PictogramOption): void {
    this.value.set(option.value);
    this.close(true);
  }

  protected clear(): void {
    this.value.set('');
  }

  protected onInput(event: Event): void {
    this.value.set((event.target as HTMLInputElement).value);
  }

  protected onSearch(event: Event): void {
    this.query.set((event.target as HTMLInputElement).value);
  }
}
