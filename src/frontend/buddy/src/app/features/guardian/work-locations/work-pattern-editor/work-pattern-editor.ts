import { Component, computed, inject, input, linkedSignal, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { dayOfWeekIndex, todayIsoDate } from '../../../../core/date-utils';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../../core/i18n/translation.service';
import {
  Weekday,
  WorkLocationSchedule,
  WorkLocationsService,
  WorkPatternDay,
} from '../../../../core/work-locations.service';
import {
  SegmentedControl,
  SegmentedControlOption,
} from '../../../../shared/segmented-control/segmented-control';
import { createAction } from '../../../../shared/action-state/action-state';
import {
  MAX_CYCLE_WEEKS,
  WEEKDAYS_MONDAY_FIRST,
  anchorForCurrentWeek,
  cycleWeekOf,
  patternKey,
  weekName,
} from '../work-pattern';
import { Card } from '../../../../shared/card/card';

// Sunday 2026-10-04 + n days walks Sunday..Saturday, matching Date.getDay().
const A_SUNDAY = new Date(2026, 9, 4);

function shortDayName(day: Weekday, locale: string): string {
  const date = new Date(
    A_SUNDAY.getFullYear(),
    A_SUNDAY.getMonth(),
    A_SUNDAY.getDate() + dayOfWeekIndex(day),
  );
  return date.toLocaleDateString(locale, { weekday: 'short' });
}

// Draft of the cycle length, "which week is this week" and the week x weekday grid. The draft
// resets whenever the page reloads the schedule; nothing is sent until Save.
@Component({
  selector: 'app-work-pattern-editor',
  imports: [FormsModule, TranslatePipe, SegmentedControl, Card],
  templateUrl: './work-pattern-editor.html',
})
export class WorkPatternEditor {
  private readonly workLocations = inject(WorkLocationsService);
  private readonly translation = inject(TranslationService);

  readonly schedule = input.required<WorkLocationSchedule>();
  readonly changed = output<void>();

  protected readonly weekdays = WEEKDAYS_MONDAY_FIRST;
  private readonly today = todayIsoDate();

  protected readonly cycleWeeks = linkedSignal(() => this.schedule().pattern.cycleWeeks);

  protected readonly currentWeek = linkedSignal(() => {
    const { anchorMonday, cycleWeeks } = this.schedule().pattern;
    return cycleWeekOf(anchorMonday, cycleWeeks, this.today);
  });

  protected readonly cells = linkedSignal(() => {
    const cells: Record<string, string> = {};
    for (const day of this.schedule().pattern.days) {
      cells[patternKey(day.week, day.day)] = day.locationId;
    }
    return cells;
  });

  protected readonly saving = createAction();
  // Whether the draft on screen is the one just saved; any edit clears it.
  protected readonly saved = signal(false);

  protected readonly active = computed(() =>
    this.schedule().locations.filter((l) => !l.isArchived),
  );

  protected readonly weeks = computed(() =>
    Array.from({ length: this.cycleWeeks() }, (_, week) => week),
  );

  protected readonly cycleOptions = computed((): SegmentedControlOption<number>[] => {
    this.translation.language();
    return Array.from({ length: MAX_CYCLE_WEEKS }, (_, i) => i + 1).map((count) => ({
      value: count,
      label:
        count === 1
          ? this.translation.translate('workLocations.pattern.cycleOne')
          : this.translation.translate('workLocations.pattern.cycleMany', { count }),
    }));
  });

  protected readonly currentWeekOptions = computed((): SegmentedControlOption<number>[] =>
    this.weeks().map((week) => ({ value: week, label: this.weekLabel(week) })),
  );

  protected readonly dayNames = computed(() => {
    const locale = this.translation.language();
    return WEEKDAYS_MONDAY_FIRST.map((day) => shortDayName(day, locale));
  });

  protected weekLabel(week: number): string {
    return this.translation.translate('workLocations.pattern.weekName', {
      name: weekName(week),
    });
  }

  protected cellLabel(week: number, day: Weekday): string {
    return this.translation.translate('workLocations.pattern.dayLabel', {
      week: this.weekLabel(week),
      day: shortDayName(day, this.translation.language()),
    });
  }

  protected cell(week: number, day: Weekday): string {
    return this.cells()[patternKey(week, day)] ?? '';
  }

  protected setCell(week: number, day: Weekday, locationId: string): void {
    this.cells.update((cells) => ({ ...cells, [patternKey(week, day)]: locationId }));
    this.saved.set(false);
  }

  protected setCycleWeeks(count: number): void {
    this.cycleWeeks.set(count);
    this.currentWeek.update((week) => Math.min(week, count - 1));
    this.saved.set(false);
  }

  protected setCurrentWeek(week: number): void {
    this.currentWeek.set(week);
    this.saved.set(false);
  }

  protected async save(): Promise<void> {
    const cycleWeeks = this.cycleWeeks();
    const days: WorkPatternDay[] = [];

    for (let week = 0; week < cycleWeeks; week++) {
      for (const day of WEEKDAYS_MONDAY_FIRST) {
        const locationId = this.cell(week, day);
        if (locationId) {
          days.push({ week, day, locationId });
        }
      }
    }

    // Keep the stored anchor when it already makes this week the chosen cycle week, so saving an
    // unchanged pattern doesn't rewrite it to an equivalent Monday.
    const stored = this.schedule().pattern;
    const anchorMonday =
      stored.cycleWeeks === cycleWeeks &&
      cycleWeekOf(stored.anchorMonday, cycleWeeks, this.today) === this.currentWeek()
        ? stored.anchorMonday
        : anchorForCurrentWeek(this.today, this.currentWeek());

    this.saved.set(false);

    await this.saving.run(
      true,
      async () => {
        await this.workLocations.replacePattern({ cycleWeeks, anchorMonday, days });
        this.saved.set(true);
        this.changed.emit();
      },
      'workLocations.pattern.saveError',
    );
  }
}
