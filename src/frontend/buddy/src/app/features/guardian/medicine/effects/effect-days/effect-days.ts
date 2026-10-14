import { Component, computed, inject, input, output } from '@angular/core';

import { parseIsoDate } from '../../../../../core/date-utils';
import { TranslatePipe } from '../../../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../../../core/i18n/translation.service';
import { MedicineEffectNote } from '../../../../../core/medicines.service';
import { TimeOfDayPipe } from '../../../../../core/time-of-day.pipe';
import { Card } from '../../../../../shared/card/card';

interface DayRow {
  date: string;
  label: string;
  note: MedicineEffectNote | undefined;
}

// Every day in the shown range, newest first, with a blank line for days not rated. Choosing a day
// hands it to the form.
@Component({
  selector: 'app-effect-days',
  imports: [Card, TimeOfDayPipe, TranslatePipe],
  templateUrl: './effect-days.html',
})
export class EffectDays {
  private readonly translation = inject(TranslationService);

  readonly days = input.required<string[]>();
  readonly notes = input.required<Partial<Record<string, MedicineEffectNote>>>();
  readonly selectedDate = input<string>('');

  readonly selectDate = output<string>();

  protected readonly rows = computed((): DayRow[] => {
    const locale = this.translation.language();
    const notes = this.notes();

    return [...this.days()].reverse().map((date) => ({
      date,
      label: parseIsoDate(date).toLocaleDateString(locale, {
        weekday: 'short',
        month: 'short',
        day: 'numeric',
      }),
      note: notes[date],
    }));
  });
}
