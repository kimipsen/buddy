import { Component, computed, inject, input, output } from '@angular/core';

import { parseIsoDate } from '../../../../core/date-utils';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../../core/i18n/translation.service';
import { SleepEntry } from '../../../../core/sleep-diary.service';
import { TimeOfDayPipe } from '../../../../core/time-of-day.pipe';
import { splitMinutes } from '../sleep-entry-draft';
import { Card } from '../../../../shared/card/card';

interface HistoryRow {
  date: string;
  label: string;
  isWeekend: boolean;
  entry: SleepEntry | undefined;
}

// Every night in the shown range, newest first, with blanks for nights not logged -- the same
// "empty cell" the paper form shows. Choosing a night hands it to the entry form.
@Component({
  selector: 'app-sleep-history',
  imports: [TimeOfDayPipe, TranslatePipe, Card],
  templateUrl: './sleep-history.html',
})
export class SleepHistory {
  private readonly translation = inject(TranslationService);

  readonly days = input.required<string[]>();
  readonly entries = input.required<Partial<Record<string, SleepEntry>>>();
  readonly selectedDate = input<string>('');

  readonly selectDate = output<string>();

  protected readonly rows = computed((): HistoryRow[] => {
    const locale = this.translation.language();
    const entries = this.entries();

    return [...this.days()].reverse().map((date) => {
      const day = parseIsoDate(date);

      return {
        date,
        label: day.toLocaleDateString(locale, { weekday: 'short', month: 'short', day: 'numeric' }),
        isWeekend: day.getDay() === 0 || day.getDay() === 6,
        entry: entries[date],
      };
    });
  });

  protected split(total: number): { hours: number; minutes: number } {
    return splitMinutes(total);
  }
}
