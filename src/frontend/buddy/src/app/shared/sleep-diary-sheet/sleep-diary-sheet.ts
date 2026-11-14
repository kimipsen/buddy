import { Component, computed, inject, input } from '@angular/core';

import { parseIsoDate } from '../../core/date-utils';
import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { TranslationService } from '../../core/i18n/translation.service';
import { SleepEntry } from '../../core/sleep-diary.service';
import { TimeOfDayPipe } from '../../core/time-of-day.pipe';
import { splitMinutes } from '../../features/guardian/sleep-diary/sleep-entry-draft';
import type { NightDutyCell } from '../../features/guardian/sleep-diary/sleep-history/sleep-history';

interface SheetRow {
  date: string;
  label: string;
  isWeekend: boolean;
  entry: SleepEntry | undefined;
  // Only on a logged night; undefined when night duty has nothing planned for it.
  nightDuty: NightDutyCell | undefined;
}

const COLUMNS: readonly string[] = [
  'date',
  'weekend',
  'routineStart',
  'ritual',
  'bedTime',
  'fellAsleep',
  'wakeUps',
  'morningWake',
  'tired',
  'naps',
  'total',
  'remarks',
];

// The clinic's paper sleep-registration form as a table, plus the sleep hygiene notes. Shared by
// the no-login share page and the guardian's print page.
@Component({
  selector: 'app-sleep-diary-sheet',
  imports: [TimeOfDayPipe, TranslatePipe],
  templateUrl: './sleep-diary-sheet.html',
})
export class SleepDiarySheet {
  private readonly translation = inject(TranslationService);

  // Every night to show, oldest first ("YYYY-MM-DD"). A night without an entry gets a blank row.
  readonly days = input.required<readonly string[]>();
  readonly entries = input.required<readonly SleepEntry[]>();
  readonly hygieneNotes = input.required<string>();
  // Who was on duty, per night. null (the default) leaves the column out.
  readonly nightDuty = input<Partial<Record<string, NightDutyCell>> | null>(null);

  // The 12 column keys, plus 'nightDuty' after 'weekend' when nightDuty() is set.
  protected readonly columns = computed((): readonly string[] =>
    this.nightDuty() ? [...COLUMNS.slice(0, 2), 'nightDuty', ...COLUMNS.slice(2)] : COLUMNS,
  );

  protected readonly rows = computed((): SheetRow[] => {
    const locale = this.translation.language();
    const byDate = new Map(this.entries().map((entry) => [entry.date, entry]));
    const duty = this.nightDuty() ?? {};

    return this.days().map((date) => {
      const day = parseIsoDate(date);
      const entry = byDate.get(date);

      return {
        date,
        label: day.toLocaleDateString(locale, { weekday: 'short', day: 'numeric', month: 'short' }),
        isWeekend: day.getDay() === 0 || day.getDay() === 6,
        entry,
        nightDuty: entry ? duty[date] : undefined,
      };
    });
  });

  protected duration(total: number): { hours: number; minutes: number } {
    return splitMinutes(total);
  }
}
