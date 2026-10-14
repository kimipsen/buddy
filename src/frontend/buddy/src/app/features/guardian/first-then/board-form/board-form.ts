import { Component, computed, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

import {
  STARTER_FIRSTS,
  STARTER_THENS,
  StarterCard,
  THEN_LENGTHS,
} from '../../../../core/first-then';
import { FirstThenCard } from '../../../../core/first-then.service';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../../core/i18n/translation.service';
import { CardSuggestions, FirstThenCardPicker } from '../card-picker/card-picker';

export interface BoardDraft {
  first: FirstThenCard;
  then: FirstThenCard;
  thenMinutes: number | null;
}

// The API's bounds (SetFirstThenBoardValidator).
const MAX_TITLE = 60;
const MAX_ICON = 16;

function isValidCard(title: string, icon: string): boolean {
  const t = title.trim();
  const i = icon.trim();
  return t.length > 0 && t.length <= MAX_TITLE && i.length > 0 && i.length <= MAX_ICON;
}

/**
 * "New board" on the guardian's First – Then page: two card pickers (recent, today's tasks for
 * the First, starters, or typed), an optional Then length and "Show to {child}". Setting a board
 * while one is current asks first, inline (first-then-board.md, Question 7).
 */
@Component({
  selector: 'app-first-then-board-form',
  imports: [FirstThenCardPicker, FormsModule, TranslatePipe],
  templateUrl: './board-form.html',
})
export class FirstThenBoardForm {
  private readonly translation = inject(TranslationService);

  readonly childName = input.required<string>();
  readonly recentFirsts = input<readonly FirstThenCard[]>([]);
  readonly recentThens = input<readonly FirstThenCard[]>([]);
  readonly todaysTasks = input<readonly FirstThenCard[]>([]);
  readonly hasCurrentBoard = input(false);
  readonly busy = input(false);

  readonly submitted = output<BoardDraft>();

  protected readonly firstTitle = signal('');
  protected readonly firstIcon = signal('');
  protected readonly thenTitle = signal('');
  protected readonly thenIcon = signal('');
  protected readonly thenMinutes = signal<number | null>(null);
  protected readonly confirming = signal(false);
  protected readonly lengths = THEN_LENGTHS;

  protected readonly firstSuggestions = computed((): CardSuggestions[] => [
    { labelKey: 'firstThen.guardian.recent', cards: this.recentFirsts() },
    { labelKey: 'firstThen.guardian.fromToday', cards: this.todaysTasks() },
    { labelKey: 'firstThen.guardian.starters', cards: this.starters(STARTER_FIRSTS) },
  ]);
  protected readonly thenSuggestions = computed((): CardSuggestions[] => [
    { labelKey: 'firstThen.guardian.recent', cards: this.recentThens() },
    { labelKey: 'firstThen.guardian.starters', cards: this.starters(STARTER_THENS) },
  ]);

  protected readonly valid = computed(
    () =>
      isValidCard(this.firstTitle(), this.firstIcon()) &&
      isValidCard(this.thenTitle(), this.thenIcon()),
  );

  protected submit(): void {
    if (!this.valid() || this.busy()) {
      return;
    }

    if (this.hasCurrentBoard() && !this.confirming()) {
      this.confirming.set(true);
      return;
    }

    this.confirming.set(false);
    this.submitted.emit({
      first: { title: this.firstTitle().trim(), icon: this.firstIcon().trim() },
      then: { title: this.thenTitle().trim(), icon: this.thenIcon().trim() },
      thenMinutes: this.thenMinutes(),
    });
  }

  protected cancel(): void {
    this.confirming.set(false);
  }

  // Called by the page once the board is saved, so the next board starts empty.
  reset(): void {
    this.firstTitle.set('');
    this.firstIcon.set('');
    this.thenTitle.set('');
    this.thenIcon.set('');
    this.thenMinutes.set(null);
    this.confirming.set(false);
  }

  protected lengthValue(minutes: number | null): string {
    return minutes === null ? '' : String(minutes);
  }

  protected setLength(value: string): void {
    this.thenMinutes.set(value === '' ? null : Number(value));
  }

  private starters(cards: readonly StarterCard[]): FirstThenCard[] {
    return cards.map((card) => ({
      title: this.translation.translate(card.titleKey),
      icon: card.icon,
    }));
  }
}
