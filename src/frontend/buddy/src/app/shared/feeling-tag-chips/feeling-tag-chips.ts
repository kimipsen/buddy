import { Component, computed, input, output } from '@angular/core';

import { FEELING_WORDS, MAX_TAGS, SITUATION_TAGS, TAG_LABEL_KEYS } from '../../core/feelings';
import type { FeelingTag, FeelingZone } from '../../core/feelings.service';
import { TranslatePipe } from '../../core/i18n/translate.pipe';

/**
 * The chosen zone's feeling words and the situations, as toggle chips with `aria-pressed`, at most
 * three chosen (the backend's limit). A child who taps yellow sees the yellow words.
 */
@Component({
  selector: 'app-feeling-tag-chips',
  imports: [TranslatePipe],
  templateUrl: './feeling-tag-chips.html',
})
export class FeelingTagChips {
  readonly zone = input.required<FeelingZone>();
  readonly selected = input<readonly FeelingTag[]>([]);
  readonly size = input<'child' | 'guardian'>('child');

  readonly toggled = output<FeelingTag>();

  protected readonly words = computed(() => FEELING_WORDS[this.zone()]);
  protected readonly situations = SITUATION_TAGS;
  protected readonly labelKeys = TAG_LABEL_KEYS;
  protected readonly full = computed(() => this.selected().length >= MAX_TAGS);
  protected readonly maxTags = MAX_TAGS;

  protected isSelected(tag: FeelingTag): boolean {
    return this.selected().includes(tag);
  }
}
