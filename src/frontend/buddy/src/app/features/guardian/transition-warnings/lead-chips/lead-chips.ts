import { Component, computed, input, output } from '@angular/core';

import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { MAX_LEAD_TIMES, leadChoices, toggleLead } from '../warning-settings';

/** One to three lead times picked from a row of pressable chips (30, 20, 15 ... 1 min). */
@Component({
  selector: 'app-lead-chips',
  imports: [TranslatePipe],
  templateUrl: './lead-chips.html',
})
export class LeadChips {
  readonly selected = input<readonly number[]>([]);
  /** Translated name for the group. */
  readonly label = input('');
  readonly selectedChange = output<number[]>();

  protected readonly choices = computed(() => leadChoices(this.selected()));
  protected readonly full = computed(() => this.selected().length >= MAX_LEAD_TIMES);

  protected isOn(minutes: number): boolean {
    return this.selected().includes(minutes);
  }

  protected toggle(minutes: number): void {
    this.selectedChange.emit(toggleLead(this.selected(), minutes));
  }
}
