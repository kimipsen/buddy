import { Component, computed, effect, inject, input, signal, untracked } from '@angular/core';
import { FormsModule } from '@angular/forms';

import {
  addDaysIso,
  parseIsoDate,
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
import { DateSelect } from '../../../../shared/date-select/date-select';

const WEEKS_SHOWN = 4;
const FOLLOW_PATTERN = 'pattern';
const OFF = 'off';

// Four weeks of resolved days, each with a picker: follow the pattern (clears the exception), off,
// or a specific location. A separate range form covers holidays. Reloads its days whenever the page
// hands it a new schedule (a pattern or location change also changes how days resolve).
@Component({
  selector: 'app-work-day-overrides',
  imports: [FormsModule, TranslatePipe, DateSelect],
  templateUrl: './work-day-overrides.html',
})
export class WorkDayOverrides {
  private readonly workLocations = inject(WorkLocationsService);
  private readonly translation = inject(TranslationService);

  readonly schedule = input.required<WorkLocationSchedule>();

  protected readonly followPattern = FOLLOW_PATTERN;
  protected readonly off = OFF;

  protected readonly start = signal(startOfWeekIso(todayIsoDate()));
  protected readonly days = signal<WorkDay[]>([]);
  protected readonly loading = signal(true);
  protected readonly savingDate = signal<string | null>(null);
  protected readonly error = signal<string | null>(null);

  protected readonly rangeFrom = signal(todayIsoDate());
  protected readonly rangeTo = signal(todayIsoDate());
  protected readonly rangeChoice = signal(OFF);
  protected readonly rangeIsValid = computed(
    () => !!this.rangeFrom() && !!this.rangeTo() && this.rangeTo() >= this.rangeFrom(),
  );

  // Paging or picking quickly overlaps loads; only the latest one may write the grid, so an older
  // response arriving last can't show the wrong weeks.
  private latestLoad = 0;

  protected readonly active = computed(() =>
    this.schedule().locations.filter((l) => !l.isArchived),
  );

  protected readonly weeks = computed(() => {
    const days = this.days();
    return Array.from({ length: WEEKS_SHOWN }, (_, w) => days.slice(w * 7, w * 7 + 7));
  });

  constructor() {
    effect(() => {
      this.schedule();
      this.start();
      untracked(() => void this.load());
    });
  }

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
    this.savingDate.set(date);
    this.error.set(null);

    try {
      await action();
      await this.load();
    } catch {
      this.error.set('workLocations.overrides.saveError');
    } finally {
      this.savingDate.set(null);
    }
  }

  private async load(): Promise<void> {
    const start = this.start();
    const load = ++this.latestLoad;

    try {
      const days = await this.workLocations.listWorkDays(
        this.schedule().guardianId,
        start,
        addDaysIso(start, WEEKS_SHOWN * 7 - 1),
      );
      if (load === this.latestLoad) {
        this.days.set(days);
      }
    } catch {
      if (load === this.latestLoad) {
        this.error.set('workLocations.overrides.loadError');
      }
    } finally {
      if (load === this.latestLoad) {
        this.loading.set(false);
      }
    }
  }
}
