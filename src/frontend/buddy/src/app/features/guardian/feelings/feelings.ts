import { Component, computed, inject, linkedSignal, resource, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { addDaysIso, buildDateRangeIso, todayIsoDate } from '../../../core/date-utils';
import { FeaturesService } from '../../../core/features.service';
import {
  FeelingCheckIn,
  FeelingCheckIns,
  FeelingsDisplayMode,
  FeelingsService,
} from '../../../core/feelings.service';
import { ChildSummary, GuardiansService } from '../../../core/guardians.service';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../core/i18n/translation.service';
import { MedicinesService } from '../../../core/medicines.service';
import { SleepDiaryService } from '../../../core/sleep-diary.service';
import { Card } from '../../../shared/card/card';
import { FeelingsTrend } from '../../../shared/feelings-trend/feelings-trend';
import { Page } from '../../../shared/page/page';
import {
  SegmentedControl,
  SegmentedControlOption,
} from '../../../shared/segmented-control/segmented-control';
import { FeelingsCheckInForm } from './check-in-form/check-in-form';
import { FeelingsCheckInSettings } from './check-in-settings/check-in-settings';
import { FeelingsDayDetail } from './day-detail/day-detail';
import { FeelingsWeeks, WEEKS_SHOWN } from './feelings-weeks/feelings-weeks';
import { FeelingsShareLinks } from './share-links/share-links';

export const DAYS_SHOWN = 14;

type View = 'days' | 'weeks';

interface Window {
  childId: string;
  from: string;
  to: string;
}

const sameWindow = (a: Window | undefined, b: Window | undefined) =>
  a?.childId === b?.childId && a?.from === b?.from && a?.to === b?.to;

/**
 * The guardian's feelings page (docs/backend/analysis/feelings-check-in.md, Question 7 and
 * Frontend): a 14-day grid next to sleep and doses, an 8-week summary, a day's check-ins with edit
 * and remove, logging an observation, the child's check-in settings and doctor share links. The
 * trend is composed here from three reads -- feelings, the sleep diary and doses -- so each feature
 * logs its own health-data read; a feature turned off just leaves its column or markers out.
 */
@Component({
  selector: 'app-guardian-feelings',
  imports: [
    Card,
    FeelingsCheckInForm,
    FeelingsCheckInSettings,
    FeelingsDayDetail,
    FeelingsShareLinks,
    FeelingsTrend,
    FeelingsWeeks,
    FormsModule,
    Page,
    SegmentedControl,
    TranslatePipe,
  ],
  templateUrl: './feelings.html',
})
export class GuardianFeelings {
  private readonly guardians = inject(GuardiansService);
  private readonly feelings = inject(FeelingsService);
  private readonly sleepDiary = inject(SleepDiaryService);
  private readonly medicines = inject(MedicinesService);
  private readonly features = inject(FeaturesService);
  private readonly translation = inject(TranslationService);

  protected readonly children = resource({ loader: () => this.guardians.listMyChildren() });
  protected readonly childList = computed((): ChildSummary[] =>
    this.children.hasValue() ? this.children.value() : [],
  );
  protected readonly selectedChildId = linkedSignal(() => this.childList()[0]?.id);
  protected readonly selectedChildName = computed(
    () => this.childList().find((c) => c.id === this.selectedChildId())?.name.givenName ?? '',
  );

  // A guardian's "today" is their own device's, as on every guardian page.
  private readonly today = todayIsoDate();
  protected readonly view = signal<View>('days');
  protected readonly viewOptions = computed((): SegmentedControlOption<View>[] => [
    { value: 'days', label: this.translation.translate('feelings.guardian.views.days') },
    { value: 'weeks', label: this.translation.translate('feelings.guardian.views.weeks') },
  ]);
  protected readonly rangeEnd = signal(this.today);
  protected readonly selectedDate = signal(this.today);
  protected readonly editing = signal<FeelingCheckIn | null>(null);
  protected readonly days = computed(() =>
    buildDateRangeIso(addDaysIso(this.rangeEnd(), -(DAYS_SHOWN - 1)), DAYS_SHOWN),
  );
  protected readonly canGoForward = computed(() => this.rangeEnd() < this.today);

  // One feelings read covers the 8-week summary and the 14-day grid inside it (56 <= 92 days).
  private readonly feelingsWindow = computed(
    (): Window | undefined => {
      const childId = this.selectedChildId();
      const to = this.rangeEnd();
      return childId ? { childId, from: addDaysIso(to, -(WEEKS_SHOWN * 7 - 1)), to } : undefined;
    },
    { equal: sameWindow },
  );
  // Sleep and doses only for the 14 days drawn; day D pairs with the night of D-1.
  private readonly gridWindow = computed(
    (): Window | undefined => {
      const childId = this.selectedChildId();
      const to = this.rangeEnd();
      return childId ? { childId, from: addDaysIso(to, -(DAYS_SHOWN - 1)), to } : undefined;
    },
    { equal: sameWindow },
  );

  protected readonly journal = resource({
    params: () => this.feelingsWindow(),
    loader: ({ params }) => this.feelings.listCheckIns(params.childId, params.from, params.to),
  });

  protected readonly sleep = resource({
    params: () => (this.features.enabled('sleepDiary') ? this.gridWindow() : undefined),
    loader: async ({ params }) => {
      const diary = await this.sleepDiary.listEntries(
        params.childId,
        addDaysIso(params.from, -1),
        params.to,
      );
      return Object.fromEntries(diary.entries.map((e) => [e.date, e.totalSleepMinutes]));
    },
  });

  protected readonly doses = resource({
    params: () => (this.features.enabled('medicines') ? this.gridWindow() : undefined),
    loader: async ({ params }) => {
      const doses = await this.medicines.listDoses(params.childId, params.from, params.to);
      const byDate: Record<string, string[]> = {};

      for (const dose of doses.filter((d) => d.status === 'Taken')) {
        byDate[dose.date] = [...(byDate[dose.date] ?? []), dose.time.slice(0, 5)];
      }

      return byDate;
    },
  });

  protected readonly loaded = computed((): FeelingCheckIns | null =>
    this.journal.hasValue() ? this.journal.value() : null,
  );
  protected readonly checkIns = computed(() => this.loaded()?.checkIns ?? []);
  protected readonly gridCheckIns = computed(() => {
    const from = addDaysIso(this.rangeEnd(), -(DAYS_SHOWN - 1));
    return this.checkIns().filter((c) => c.date >= from);
  });
  // A sleep or dose read that failed degrades to no column, like a feature turned off.
  protected readonly sleepByNight = computed(() =>
    this.features.enabled('sleepDiary') ? (this.sleep.hasValue() ? this.sleep.value() : {}) : null,
  );
  protected readonly dosesByDate = computed(() =>
    this.features.enabled('medicines') ? (this.doses.hasValue() ? this.doses.value() : {}) : null,
  );

  protected selectChild(childId: string): void {
    this.selectedChildId.set(childId);
    this.editing.set(null);
  }

  protected selectDate(date: string): void {
    this.selectedDate.set(date);
    this.editing.set(null);
  }

  protected shiftRange(days: number): void {
    const end = addDaysIso(this.rangeEnd(), days);
    this.rangeEnd.set(end > this.today ? this.today : end);
  }

  protected onSaved(checkIn: FeelingCheckIn): void {
    this.editing.set(null);
    this.selectedDate.set(checkIn.date);
    this.updateLoaded((journal) => ({
      ...journal,
      checkIns: sortCheckIns([...journal.checkIns.filter((c) => c.id !== checkIn.id), checkIn]),
    }));
  }

  protected onRemoved(checkInId: string): void {
    if (this.editing()?.id === checkInId) {
      this.editing.set(null);
    }

    this.updateLoaded((journal) => ({
      ...journal,
      checkIns: journal.checkIns.filter((c) => c.id !== checkInId),
    }));
  }

  protected onPromptTimesSaved(promptTimes: string[]): void {
    this.updateLoaded((journal) => ({ ...journal, promptTimes }));
  }

  protected onDisplayModeSaved(displayMode: FeelingsDisplayMode): void {
    this.updateLoaded((journal) => ({ ...journal, displayMode }));
  }

  // Only once loaded: setting the resource mid-load would cancel the load.
  private updateLoaded(change: (journal: FeelingCheckIns) => FeelingCheckIns): void {
    if (this.journal.hasValue() && !this.journal.isLoading()) {
      this.journal.set(change(this.journal.value()));
    }
  }
}

function sortCheckIns(checkIns: FeelingCheckIn[]): FeelingCheckIn[] {
  return [...checkIns].sort(
    (a, b) =>
      a.date.localeCompare(b.date) ||
      a.time.localeCompare(b.time) ||
      a.recordedAt.localeCompare(b.recordedAt),
  );
}
