import { Component, computed, inject, input, linkedSignal, resource, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

import {
  addDaysIso,
  parseIsoDate,
  shiftRangeEndDate,
  startOfWeekIso,
  todayIsoDate,
} from '../../../../core/date-utils';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../../core/i18n/translation.service';
import {
  WorkDay,
  WorkLocationSchedule,
  WorkLocationsService,
  isWorkDayOverride,
  workDayLocation,
} from '../../../../core/work-locations.service';
import { createAction } from '../../../../shared/action-state/action-state';
import { DateSelect } from '../../../../shared/date-select/date-select';
import { Card } from '../../../../shared/card/card';

const WEEKS_SHOWN = 4;
const FOLLOW_PATTERN = 'pattern';
const OFF = 'off';

// Four weeks of resolved days, each with a picker: follow the pattern (clears the exception), off,
// or a specific location. A separate range form covers holidays. Reloads its days whenever the page
// hands it a new schedule (a pattern or location change also changes how days resolve).
@Component({
  selector: 'app-work-day-overrides',
  imports: [FormsModule, TranslatePipe, DateSelect, Card],
  templateUrl: './work-day-overrides.html',
})
export class WorkDayOverrides {
  private readonly workLocations = inject(WorkLocationsService);
  private readonly translation = inject(TranslationService);

  readonly schedule = input.required<WorkLocationSchedule>();

  protected readonly followPattern = FOLLOW_PATTERN;
  protected readonly off = OFF;

  protected readonly start = signal(startOfWeekIso(todayIsoDate()));
  // Reloads whenever the page hands over a new schedule or the window moves. Paging quickly
  // overlaps loads; resource only keeps the latest one, so an older response arriving last can't
  // show the wrong weeks.
  protected readonly days = resource({
    params: () => ({ schedule: this.schedule(), start: this.start() }),
    loader: ({ params }) =>
      this.workLocations.listWorkDays(
        params.schedule.guardianId,
        params.start,
        addDaysIso(params.start, WEEKS_SHOWN * 7 - 1),
      ),
  });
  // The weeks on screen: the last loaded ones stay up while the next weeks load (or fail to), so
  // paging doesn't blank the grid. Undefined only until the first load finishes.
  protected readonly shownDays = linkedSignal<WorkDay[] | undefined, WorkDay[] | undefined>({
    source: () => (this.days.hasValue() ? this.days.value() : undefined),
    computation: (days, previous) => days ?? previous?.value,
  });
  // Keyed by the day (or the range's first day) being saved.
  protected readonly saving = createAction<string>();

  protected readonly rangeFrom = signal(todayIsoDate());
  protected readonly rangeTo = signal(todayIsoDate());
  protected readonly rangeChoice = signal(OFF);
  protected readonly rangeIsValid = computed(
    () => !!this.rangeFrom() && !!this.rangeTo() && this.rangeTo() >= this.rangeFrom(),
  );

  // Moving the first day moves the last day with it, so the range keeps its length.
  protected setRangeFrom(from: string): void {
    this.rangeTo.set(shiftRangeEndDate(this.rangeFrom(), from, this.rangeTo()));
    this.rangeFrom.set(from);
  }

  protected readonly active = computed(() =>
    this.schedule().locations.filter((l) => !l.isArchived),
  );

  protected readonly weeks = computed(() => {
    const days = this.shownDays() ?? [];
    return Array.from({ length: WEEKS_SHOWN }, (_, w) => days.slice(w * 7, w * 7 + 7));
  });

  protected shift(weeks: number): void {
    this.start.update((start) => addDaysIso(start, weeks * 7));
  }

  protected dayLabel(date: string): string {
    return parseIsoDate(date).toLocaleDateString(this.translation.language(), {
      weekday: 'short',
      day: 'numeric',
      month: 'short',
    });
  }

  protected pickerLabel(date: string): string {
    return this.translation.translate('workLocations.overrides.dayLabel', {
      date: this.dayLabel(date),
    });
  }

  protected readonly isOverride = isWorkDayOverride;
  protected readonly locationOf = workDayLocation;

  protected choiceFor(day: WorkDay): string {
    if (!isWorkDayOverride(day)) {
      return FOLLOW_PATTERN;
    }
    return workDayLocation(day)?.id ?? OFF;
  }

  // An archived location can still be the day's current exception; keep it selectable so the
  // picker shows the real value instead of silently falling back to the first option.
  protected optionsFor(day: WorkDay): WorkLocationSchedule['locations'] {
    const active = this.active();
    const current = workDayLocation(day);
    return current?.isArchived && isWorkDayOverride(day) ? [...active, current] : active;
  }

  protected async choose(date: string, choice: string): Promise<void> {
    await this.save(date, () =>
      choice === FOLLOW_PATTERN
        ? this.workLocations.clearOverrides(date, date)
        : this.workLocations.setOverrides(date, date, choice === OFF ? null : choice),
    );
  }

  protected async applyRange(): Promise<void> {
    const choice = this.rangeChoice();
    const from = this.rangeFrom();
    const to = this.rangeTo();

    await this.save(from, () =>
      choice === FOLLOW_PATTERN
        ? this.workLocations.clearOverrides(from, to)
        : this.workLocations.setOverrides(from, to, choice === OFF ? null : choice),
    );
  }

  private async save(date: string, action: () => Promise<unknown>): Promise<void> {
    await this.saving.run(
      date,
      async () => {
        await action();
        this.days.reload();
      },
      'workLocations.overrides.saveError',
    );
  }
}
