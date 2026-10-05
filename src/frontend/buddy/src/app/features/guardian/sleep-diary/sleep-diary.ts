import { Component, computed, inject, linkedSignal, resource, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';

import { firstAndLast } from '../../../core/array-utils';
import { addDaysIso, buildDateRangeIso, todayIsoDate } from '../../../core/date-utils';
import { ChildSummary, GuardiansService } from '../../../core/guardians.service';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import {
  SleepDiaryEntries,
  SleepDiaryService,
  SleepEntry,
} from '../../../core/sleep-diary.service';
import { HygieneNotes } from './hygiene-notes/hygiene-notes';
import { SleepShareLinks } from './share-links/share-links';
import { SleepEntryForm } from './sleep-entry-form/sleep-entry-form';
import { SleepHistory } from './sleep-history/sleep-history';

// The paper form a sleep clinic hands out covers 14 nights; the history pages by the same window.
export const NIGHTS_SHOWN = 14;

interface DiaryRequest {
  childId: string;
  from: string;
  to: string;
}

const EMPTY_DIARY: SleepDiaryEntries = { sleepHygieneNotes: '', entries: [] };

// Guardian-only, like the backend (no child view): log a night, review the last 14, keep the
// diary-wide hygiene notes and hand a read-only link to a clinician.
@Component({
  selector: 'app-guardian-sleep-diary',
  imports: [
    FormsModule,
    RouterLink,
    HygieneNotes,
    SleepEntryForm,
    SleepHistory,
    SleepShareLinks,
    TranslatePipe,
  ],
  templateUrl: './sleep-diary.html',
})
export class GuardianSleepDiary {
  private readonly guardians = inject(GuardiansService);
  private readonly sleepDiary = inject(SleepDiaryService);

  protected readonly children = resource({ loader: () => this.guardians.listMyChildren() });
  protected readonly childList = computed((): ChildSummary[] =>
    this.children.hasValue() ? this.children.value() : [],
  );
  protected readonly selectedChildId = linkedSignal(() => this.childList()[0]?.id);

  private readonly today = todayIsoDate();
  // The last night of the shown window, and the night open in the form.
  protected readonly rangeEnd = signal(this.today);
  protected readonly selectedDate = signal(this.today);
  protected readonly days = computed(() =>
    buildDateRangeIso(addDaysIso(this.rangeEnd(), -(NIGHTS_SHOWN - 1)), NIGHTS_SHOWN),
  );
  protected readonly canGoForward = computed(() => this.rangeEnd() < this.today);

  private readonly request = computed(
    (): DiaryRequest | undefined => {
      const childId = this.selectedChildId();
      const [from, to] = firstAndLast(this.days());
      return childId ? { childId, from, to } : undefined;
    },
    { equal: (a, b) => a?.childId === b?.childId && a?.from === b?.from && a?.to === b?.to },
  );

  protected readonly diary = resource({
    params: () => this.request(),
    loader: ({ params }) => this.sleepDiary.listEntries(params.childId, params.from, params.to),
  });
  protected readonly loaded = computed(() =>
    this.diary.hasValue() ? this.diary.value() : EMPTY_DIARY,
  );
  protected readonly entriesByDate = computed(() => {
    const byDate: Partial<Record<string, SleepEntry>> = {};

    for (const entry of this.loaded().entries) {
      byDate[entry.date] = entry;
    }

    return byDate;
  });
  protected readonly selectedEntry = computed(
    () => this.entriesByDate()[this.selectedDate()] ?? null,
  );

  protected selectChild(childId: string): void {
    this.selectedChildId.set(childId);
  }

  // A date picked in the form outside the window moves the window to end on it, so the form
  // always edits a night whose entry has been loaded.
  protected selectDate(date: string): void {
    this.selectedDate.set(date);
    const [from, to] = firstAndLast(this.days());

    if (date < from || date > to) {
      this.rangeEnd.set(date);
    }
  }

  protected shiftRange(nights: number): void {
    const end = addDaysIso(this.rangeEnd(), nights);
    this.rangeEnd.set(end > this.today ? this.today : end);
  }

  protected onSaved(entry: SleepEntry): void {
    this.updateLoaded((diary) => ({
      ...diary,
      entries: [...diary.entries.filter((e) => e.date !== entry.date), entry],
    }));
  }

  protected onCleared(date: string): void {
    this.updateLoaded((diary) => ({
      ...diary,
      entries: diary.entries.filter((e) => e.date !== date),
    }));
  }

  protected onNotesSaved(sleepHygieneNotes: string): void {
    this.updateLoaded((diary) => ({ ...diary, sleepHygieneNotes }));
  }

  // Only once loaded: setting the resource mid-load would cancel the load.
  private updateLoaded(change: (diary: SleepDiaryEntries) => SleepDiaryEntries): void {
    if (this.diary.hasValue() && !this.diary.isLoading()) {
      this.diary.set(change(this.diary.value()));
    }
  }
}
