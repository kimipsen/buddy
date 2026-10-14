import { Component, input, model } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { FirstThenCard } from '../../../../core/first-then.service';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';

export interface CardSuggestions {
  // i18n key of the group's heading ("Used before", "From today", "Suggestions").
  readonly labelKey: string;
  readonly cards: readonly FirstThenCard[];
}

/**
 * One side of a new First – Then board: a title and an emoji, typed or filled from a suggestion
 * chip. A picked card is a copy; editing it afterwards changes only this board.
 */
@Component({
  selector: 'app-first-then-card-picker',
  imports: [FormsModule, TranslatePipe],
  templateUrl: './card-picker.html',
  // Block, so the form's space-y gap applies between the two pickers.
  host: { class: 'block' },
})
export class FirstThenCardPicker {
  // "First" or "Then", already translated; also names the inputs.
  readonly card = input.required<string>();
  readonly hint = input.required<string>();
  readonly idPrefix = input.required<string>();
  readonly suggestions = input<readonly CardSuggestions[]>([]);

  readonly title = model('');
  readonly icon = model('');

  protected pick(card: FirstThenCard): void {
    this.title.set(card.title);
    this.icon.set(card.icon);
  }

  protected isPicked(card: FirstThenCard): boolean {
    return this.title() === card.title && this.icon() === card.icon;
  }
}
