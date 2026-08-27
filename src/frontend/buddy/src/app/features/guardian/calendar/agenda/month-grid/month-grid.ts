import { Component, computed, input, output } from '@angular/core';

import { CalendarOccurrence } from '../../../../../core/calendars.service';
import { TranslatePipe } from '../../../../../core/i18n/translate.pipe';
import { AgendaEntry, groupTaskRuns, isTaskRun } from '../../../../../core/task-run';
import { AgendaDay } from '../agenda';

const MAX_CHIPS_PER_DAY = 3;

interface MonthGridWeek {
  days: AgendaDay[];
}

// Purely presentational: CalendarAgenda owns every signal here (view mode, anchor date, loaded
// occurrences) -- this component only lays out whatever days/occurrences it's given as a grid.
// See the "Rendering" section of docs/frontend/analysis/guardian-full-calendar-views.md for why
// a day cell only shows chips (no inline edit/delete/complete) and clicking one drills into Day
// view instead.
@Component({
  selector: 'app-month-grid',
  imports: [TranslatePipe],
  templateUrl: './month-grid.html'
})
export class MonthGrid {
  readonly days = input.required<AgendaDay[]>();
  readonly weekdayLabels = input.required<readonly string[]>();
  readonly occurrencesByDate = input.required<Record<string, CalendarOccurrence[]>>();
  readonly today = input.required<string>();

  readonly daySelected = output<string>();

  protected readonly weeks = computed<MonthGridWeek[]>(() => {
    const days = this.days();
    const weeks: MonthGridWeek[] = [];

    for (let index = 0; index < days.length; index += 7) {
      weeks.push({ days: days.slice(index, index + 7) });
    }

    return weeks;
  });

  protected occurrencesFor(date: string): CalendarOccurrence[] {
    return this.occurrencesByDate()[date] ?? [];
  }

  // Grouped the same way the day/week list is (see core/task-run.ts) -- without this, a
  // template-scheduled task's several same-day subtask occurrences would render as several
  // identical-itemId chips (an Angular @for track collision) instead of one.
  protected groupedOccurrencesFor(date: string): AgendaEntry[] {
    return groupTaskRuns(this.occurrencesFor(date));
  }

  protected visibleChips(date: string): AgendaEntry[] {
    return this.groupedOccurrencesFor(date).slice(0, MAX_CHIPS_PER_DAY);
  }

  protected overflowCount(date: string): number {
    return Math.max(0, this.groupedOccurrencesFor(date).length - MAX_CHIPS_PER_DAY);
  }

  protected titleFor(entry: AgendaEntry): string {
    return isTaskRun(entry) ? entry.parentTitle : entry.title;
  }

  protected selectDay(date: string): void {
    this.daySelected.emit(date);
  }
}
