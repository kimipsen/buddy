import { Component, computed, inject, input, linkedSignal, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { todayIsoDate } from '../../../../core/date-utils';
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
import {
  MAX_CYCLE_WEEKS,
  WEEKDAYS_MONDAY_FIRST,
  WEEK_NAMES,
  anchorForCurrentWeek,
  cycleWeekOf,
  patternKey,
} from '../work-pattern';

// Sunday 2026-10-04 + n days walks Sunday..Saturday, matching DayOfWeek ordinals.
const A_SUNDAY = new Date(2026, 9, 4);

// Draft of the cycle length, "which week is this week" and the week x weekday grid. The draft
// resets whenever the page reloads the schedule; nothing is sent until Save.
@Component({
  selector: 'app-work-pattern-editor',
  imports: [FormsModule, TranslatePipe, SegmentedControl],
  templateUrl: './work-pattern-editor.html',
})
export class WorkPatternEditor {
  private readonly workLocations = inject(WorkLocationsService);
  private readonly translation = inject(TranslationService);

  readonly schedule = input.required<WorkLocationSchedule>();
  readonly changed = output<void>();

  protected readonly weekdays = WEEKDAYS_MONDAY_FIRST;
  protected readonly weekNames = WEEK_NAMES;
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

  protected readonly saving = signal(false);
  protected readonly status = signal<string | null>(null);
  protected readonly error = signal<string | null>(null);

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
    return WEEKDAYS_MONDAY_FIRST.map((day) => {
      const date = new Date(A_SUNDAY.getFullYear(), A_SUNDAY.getMonth(), A_SUNDAY.getDate() + day);
      return date.toLocaleDateString(locale, { weekday: 'short' });
    });
  });

  protected weekLabel(week: number): string {
    return this.translation.translate('workLocations.pattern.weekName', {
      name: WEEK_NAMES[week],
    });
  }

  protected cellLabel(week: number, dayIndex: number): string {
    return this.translation.translate('workLocations.pattern.dayLabel', {
      week: this.weekLabel(week),
      day: this.dayNames()[dayIndex],
    });
  }

  protected cell(week: number, day: Weekday): string {
    return this.cells()[patternKey(week, day)] ?? '';
  }

  protected setCell(week: number, day: Weekday, locationId: string): void {
    this.cells.update((cells) => ({ ...cells, [patternKey(week, day)]: locationId }));
    this.status.set(null);
  }

  protected setCycleWeeks(count: number): void {
    this.cycleWeeks.set(count);
    this.currentWeek.update((week) => Math.min(week, count - 1));
    this.status.set(null);
  }

  protected setCurrentWeek(week: number): void {
    this.currentWeek.set(week);
    this.status.set(null);
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

    this.saving.set(true);
    this.error.set(null);
    this.status.set(null);

    try {
      await this.workLocations.replacePattern({ cycleWeeks, anchorMonday, days });
      this.status.set('workLocations.pattern.saved');
      this.changed.emit();
    } catch {
      this.error.set('workLocations.pattern.saveError');
    } finally {
      this.saving.set(false);
    }
  }
}
