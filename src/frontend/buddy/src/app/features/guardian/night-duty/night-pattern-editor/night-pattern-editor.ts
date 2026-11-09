import { Component, computed, inject, input, linkedSignal, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { parseIsoDate } from '../../../../core/date-utils';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../../core/i18n/translation.service';
import {
  NightDutyService,
  NightPattern,
  NightPatternDay,
  NightSchedule,
} from '../../../../core/night-duty.service';
import { createAction } from '../../../../shared/action-state/action-state';
import { Card } from '../../../../shared/card/card';
import {
  SegmentedControl,
  SegmentedControlOption,
} from '../../../../shared/segmented-control/segmented-control';
import {
  MAX_CYCLE_WEEKS,
  WEEKDAYS_MONDAY_FIRST,
  anchorForCurrentWeek,
  cycleWeekOf,
  patternKey,
  weekName,
  weekdayName,
} from '../../work-locations/work-pattern';
import {
  NOBODY_CHOICE,
  NightPeople,
  SLEEPOVER_CHOICE,
  babysitterChoice,
  choiceOf,
  isCompleteEntry,
  sleepoverOf,
  responsibleFromChoice,
} from '../night-people';
import { Weekday } from '../../../../core/work-locations.service';

// location and contactInfo aren't edited here but are carried through, so a save keeps them.
interface CellDraft {
  choice: string;
  hostName: string;
  location: string;
  contactInfo: string;
  note: string;
}

const EMPTY_CELL: CellDraft = {
  choice: NOBODY_CHOICE,
  hostName: '',
  location: '',
  contactInfo: '',
  note: '',
};

// The version the editor starts from: the upcoming one if there is one (the guardian is still
// preparing it), else the current one, else an empty one-week schedule.
function editedVersion(schedule: NightSchedule): NightPattern | undefined {
  return (
    schedule.patterns.find((p) => p.status === 'Upcoming') ??
    schedule.patterns.find((p) => p.status === 'Current')
  );
}

// Draft of one fixed-schedule version: cycle length, "which week is this week", the date it applies
// from (never before the child's today) and a week x weekday grid of who and a note. Nothing is
// sent until Save. Earlier nights keep the version they had (docs/backend/analysis/night-duty.md,
// Question 4).
@Component({
  selector: 'app-night-pattern-editor',
  imports: [FormsModule, TranslatePipe, SegmentedControl, Card],
  templateUrl: './night-pattern-editor.html',
})
export class NightPatternEditor {
  private readonly nightDuty = inject(NightDutyService);
  private readonly translation = inject(TranslationService);

  readonly childId = input.required<string>();
  readonly schedule = input.required<NightSchedule>();
  readonly people = input.required<NightPeople>();
  readonly saved = output<void>();

  protected readonly weekdays = WEEKDAYS_MONDAY_FIRST;
  protected readonly sleepoverChoice = SLEEPOVER_CHOICE;
  protected readonly nobody = NOBODY_CHOICE;
  protected readonly babysitterChoice = babysitterChoice;

  private readonly today = computed(() => this.schedule().today);
  private readonly base = computed(() => editedVersion(this.schedule()));

  protected readonly cycleWeeks = linkedSignal(() => this.base()?.cycleWeeks ?? 1);
  protected readonly currentWeek = linkedSignal(() => {
    const base = this.base();
    return base ? cycleWeekOf(base.anchorMonday, base.cycleWeeks, this.today()) : 0;
  });
  protected readonly effectiveFrom = linkedSignal(() => {
    const base = this.base();
    return base?.status === 'Upcoming' ? base.effectiveFrom : this.today();
  });

  protected readonly cells = linkedSignal(() => {
    const cells: Record<string, CellDraft> = {};
    for (const day of this.base()?.days ?? []) {
      const responsible = day.entry.responsible;
      cells[patternKey(day.week, day.day)] = {
        choice: choiceOf(responsible),
        ...sleepoverOf(responsible),
        note: day.entry.note ?? '',
      };
    }
    return cells;
  });

  protected readonly saving = createAction();
  protected readonly justSaved = signal(false);

  protected readonly weeks = computed(() =>
    Array.from({ length: this.cycleWeeks() }, (_, week) => week),
  );

  protected readonly cycleOptions = computed((): SegmentedControlOption<number>[] => {
    this.translation.language();
    return Array.from({ length: MAX_CYCLE_WEEKS }, (_, i) => i + 1).map((count) => ({
      value: count,
      label:
        count === 1
          ? this.translation.translate('nightDuty.pattern.cycleOne')
          : this.translation.translate('nightDuty.pattern.cycleMany', { count }),
    }));
  });

  protected readonly currentWeekOptions = computed((): SegmentedControlOption<number>[] =>
    this.weeks().map((week) => ({ value: week, label: this.weekLabel(week) })),
  );

  protected readonly dayNames = computed(() => {
    const locale = this.translation.language();
    return WEEKDAYS_MONDAY_FIRST.map((day) => weekdayName(day, locale, 'long'));
  });

  protected readonly versions = computed(() => {
    const locale = this.translation.language();
    return this.schedule().patterns.map((pattern) => ({
      key: pattern.effectiveFrom,
      date: this.formatDate(pattern.effectiveFrom, locale),
      cycle:
        pattern.cycleWeeks === 1
          ? this.translation.translate('nightDuty.pattern.cycleOne')
          : this.translation.translate('nightDuty.pattern.cycleMany', {
              count: pattern.cycleWeeks,
            }),
      status: pattern.status,
    }));
  });

  protected readonly effectiveFromLabel = computed(() =>
    this.formatDate(this.effectiveFrom(), this.translation.language()),
  );

  protected weekLabel(week: number): string {
    return this.translation.translate('nightDuty.pattern.weekName', { name: weekName(week) });
  }

  protected cellLabel(key: string, week: number, dayIndex: number): string {
    return this.translation.translate(`nightDuty.pattern.${key}`, {
      week: this.weekLabel(week),
      day: this.dayNames()[dayIndex] ?? '',
    });
  }

  protected cell(week: number, day: Weekday): CellDraft {
    return this.cells()[patternKey(week, day)] ?? EMPTY_CELL;
  }

  protected setCell(week: number, day: Weekday, change: Partial<CellDraft>): void {
    const key = patternKey(week, day);
    this.cells.update((cells) => ({
      ...cells,
      [key]: { ...(cells[key] ?? EMPTY_CELL), ...change },
    }));
    this.edited();
  }

  protected setCycleWeeks(count: number): void {
    this.cycleWeeks.set(count);
    this.currentWeek.update((week) => Math.min(week, count - 1));
    this.edited();
  }

  protected setCurrentWeek(week: number): void {
    this.currentWeek.set(week);
    this.edited();
  }

  protected setEffectiveFrom(date: string): void {
    // A cleared or earlier date falls back to today: the past is never rewritten.
    this.effectiveFrom.set(date && date >= this.today() ? date : this.today());
    this.edited();
  }

  protected minDate(): string {
    return this.today();
  }

  private edited(): void {
    this.justSaved.set(false);
    this.saving.clearError();
  }

  protected async save(): Promise<void> {
    const cycleWeeks = this.cycleWeeks();
    const days: NightPatternDay[] = [];

    for (let week = 0; week < cycleWeeks; week++) {
      for (const day of WEEKDAYS_MONDAY_FIRST) {
        const cell = this.cell(week, day);
        const entry = {
          responsible: responsibleFromChoice(cell.choice, cell),
          note: cell.note.trim(),
        };
        if (cell.choice === NOBODY_CHOICE && !entry.note) {
          continue;
        }
        if (!isCompleteEntry(entry)) {
          this.saving.fail(true, 'nightDuty.pattern.incomplete');
          return;
        }
        days.push({ week, day, entry });
      }
    }

    // Keep the edited version's anchor when it already makes this week the chosen cycle week, so
    // saving an unchanged version doesn't rewrite it to an equivalent Monday.
    const base = this.base();
    const anchorMonday =
      base?.cycleWeeks === cycleWeeks &&
      cycleWeekOf(base.anchorMonday, cycleWeeks, this.today()) === this.currentWeek()
        ? base.anchorMonday
        : anchorForCurrentWeek(this.today(), this.currentWeek());

    this.justSaved.set(false);
    const childId = this.childId();

    await this.saving.run(
      true,
      async () => {
        await this.nightDuty.replacePattern(childId, {
          effectiveFrom: this.effectiveFrom(),
          cycleWeeks,
          anchorMonday,
          days,
        });
        this.justSaved.set(true);
        this.saved.emit();
      },
      'nightDuty.pattern.saveError',
    );
  }

  private formatDate(isoDate: string, locale: string): string {
    return parseIsoDate(isoDate).toLocaleDateString(locale, {
      month: 'short',
      day: 'numeric',
      year: 'numeric',
    });
  }
}
