import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, resource, signal } from '@angular/core';
import { ActivatedRoute } from '@angular/router';

import { addDaysIso, buildDateRangeIso, parseIsoDate } from '../../core/date-utils';
import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { TranslationService } from '../../core/i18n/translation.service';
import { SleepDiaryService, SleepEntry } from '../../core/sleep-diary.service';
import { TimeOfDayPipe } from '../../core/time-of-day.pipe';
import { splitMinutes } from '../guardian/sleep-diary/sleep-entry-draft';

const DAYS_SHOWN = 14;

interface Range {
  from?: string;
  to?: string;
}

interface Row {
  date: string;
  label: string;
  isWeekend: boolean;
  entry: SleepEntry | undefined;
}

// The page a clinician opens from a share link: no login and no app chrome, a printable table
// with the same columns as the clinic's paper sleep-registration form. The token in the URL is the
// only credential (GetSharedSleepDiary on the backend).
@Component({
  selector: 'app-shared-sleep-diary',
  imports: [DatePipe, TimeOfDayPipe, TranslatePipe],
  templateUrl: './shared-sleep-diary.html',
})
export class SharedSleepDiary {
  private readonly route = inject(ActivatedRoute);
  private readonly sleepDiary = inject(SleepDiaryService);
  private readonly translation = inject(TranslationService);

  private readonly token = this.route.snapshot.paramMap.get('token') ?? '';

  // Empty until the reader pages: the server then picks the last 14 days.
  private readonly range = signal<Range>({});

  protected readonly diary = resource({
    params: () => this.range(),
    loader: ({ params }) => this.sleepDiary.getShared(this.token, params.from, params.to),
  });

  protected readonly notFound = computed(() => {
    const error = this.diary.error();
    return error instanceof HttpErrorResponse && error.status === 404;
  });

  protected readonly rows = computed((): Row[] => {
    if (!this.diary.hasValue()) {
      return [];
    }

    const shared = this.diary.value();
    const locale = this.translation.language();
    const byDate = new Map(shared.entries.map((entry) => [entry.date, entry]));
    const dayCount = parseIsoDate(shared.to).getTime() - parseIsoDate(shared.from).getTime();

    return buildDateRangeIso(shared.from, Math.round(dayCount / 86_400_000) + 1).map((date) => {
      const day = parseIsoDate(date);

      return {
        date,
        label: day.toLocaleDateString(locale, { weekday: 'short', day: 'numeric', month: 'short' }),
        isWeekend: day.getDay() === 0 || day.getDay() === 6,
        entry: byDate.get(date),
      };
    });
  });

  protected previous(): void {
    if (this.diary.hasValue()) {
      const from = this.diary.value().from;
      this.range.set({ from: addDaysIso(from, -DAYS_SHOWN), to: addDaysIso(from, -1) });
    }
  }

  protected next(): void {
    if (this.diary.hasValue()) {
      const to = this.diary.value().to;
      this.range.set({ from: addDaysIso(to, 1), to: addDaysIso(to, DAYS_SHOWN) });
    }
  }

  protected print(): void {
    window.print();
  }

  protected duration(total: number): { hours: number; minutes: number } {
    return splitMinutes(total);
  }
}
