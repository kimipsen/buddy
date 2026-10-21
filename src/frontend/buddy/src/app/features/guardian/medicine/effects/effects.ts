import { Component, computed, inject, linkedSignal, resource, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { firstAndLast } from '../../../../core/array-utils';
import { addDaysIso, buildDateRangeIso, todayIsoDate } from '../../../../core/date-utils';
import { ChildSummary, GuardiansService } from '../../../../core/guardians.service';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { MedicineEffectNote, MedicinesService } from '../../../../core/medicines.service';
import { Page } from '../../../../shared/page/page';
import { EffectDays } from './effect-days/effect-days';
import { EffectInclusion } from './effect-inclusion/effect-inclusion';
import { EffectNoteForm } from './effect-note-form/effect-note-form';
import { EffectPeriods } from './effect-periods/effect-periods';

// The day list pages by two weeks, the sleep diary's window.
export const DAYS_SHOWN = 14;
// The summary looks back over the API's longest range (ListMedicineEffectNotesHandler.MaxRangeDays).
// The intro calls this "the last 3 months" (a quarter: at most 31 + 31 + 30 days).
export const PERIOD_DAYS = 92;

interface NotesRequest {
  childId: string;
  from: string;
  to: string;
}

function sameRequest(a: NotesRequest | undefined, b: NotesRequest | undefined): boolean {
  return a?.childId === b?.childId && a?.from === b?.from && a?.to === b?.to;
}

// Guardian-only, like the backend (no child view): rate a day, review the last 14 and read the
// dose-period summary for a doctor's appointment (docs/backend/analysis/medicine-effect-notes.md).
@Component({
  selector: 'app-guardian-medicine-effects',
  imports: [
    FormsModule,
    EffectDays,
    EffectInclusion,
    EffectNoteForm,
    EffectPeriods,
    Page,
    TranslatePipe,
  ],
  templateUrl: './effects.html',
})
export class GuardianMedicineEffects {
  private readonly guardians = inject(GuardiansService);
  private readonly medicines = inject(MedicinesService);

  protected readonly children = resource({ loader: () => this.guardians.listMyChildren() });
  protected readonly childList = computed((): ChildSummary[] =>
    this.children.hasValue() ? this.children.value() : [],
  );
  protected readonly selectedChildId = linkedSignal(() => this.childList()[0]?.id);

  protected readonly today = todayIsoDate();
  protected readonly rangeEnd = signal(this.today);
  protected readonly selectedDate = signal(this.today);
  protected readonly days = computed(() =>
    buildDateRangeIso(addDaysIso(this.rangeEnd(), -(DAYS_SHOWN - 1)), DAYS_SHOWN),
  );
  protected readonly canGoForward = computed(() => this.rangeEnd() < this.today);

  private readonly daysRequest = computed(
    (): NotesRequest | undefined => {
      const childId = this.selectedChildId();
      const [from, to] = firstAndLast(this.days());
      return childId ? { childId, from, to } : undefined;
    },
    { equal: sameRequest },
  );
  protected readonly dayNotes = resource({
    params: () => this.daysRequest(),
    loader: ({ params }) => this.medicines.listEffectNotes(params.childId, params.from, params.to),
  });
  protected readonly notesByDate = computed(() => {
    const byDate: Partial<Record<string, MedicineEffectNote>> = {};
    for (const note of this.dayNotes.hasValue() ? this.dayNotes.value() : []) {
      byDate[note.date] = note;
    }
    return byDate;
  });
  protected readonly selectedNote = computed(() => this.notesByDate()[this.selectedDate()] ?? null);

  protected readonly periodNotes = resource({
    params: () => {
      const childId = this.selectedChildId();
      return childId
        ? { childId, from: addDaysIso(this.today, -PERIOD_DAYS), to: this.today }
        : undefined;
    },
    loader: ({ params }) => this.medicines.listEffectNotes(params.childId, params.from, params.to),
  });

  protected selectChild(childId: string): void {
    this.selectedChildId.set(childId);
  }

  // A date picked in the form outside the window moves the window to end on it, so the form
  // always edits a day whose note has been loaded. A future day can't be rated (the API rejects
  // it), so it falls back to today.
  protected selectDate(picked: string): void {
    const date = picked > this.today ? this.today : picked;
    this.selectedDate.set(date);
    const [from, to] = firstAndLast(this.days());

    if (date < from || date > to) {
      this.rangeEnd.set(date);
    }
  }

  // Paging moves the selected day along when it leaves the window; otherwise the form would show
  // an unloaded (blank) note for it, and saving would overwrite the day's stored note.
  protected shiftRange(days: number): void {
    const end = addDaysIso(this.rangeEnd(), days);
    this.rangeEnd.set(end > this.today ? this.today : end);
    const [from, to] = firstAndLast(this.days());
    const selected = this.selectedDate();

    if (selected < from || selected > to) {
      this.selectedDate.set(to);
    }
  }

  protected onSaved(note: MedicineEffectNote): void {
    this.updateDayNotes((notes) => [...notes.filter((n) => n.date !== note.date), note]);
    this.periodNotes.reload();
  }

  protected onCleared(date: string): void {
    this.updateDayNotes((notes) => notes.filter((n) => n.date !== date));
    this.periodNotes.reload();
  }

  protected print(): void {
    window.print();
  }

  // Only once loaded: setting the resource mid-load would cancel the load.
  private updateDayNotes(change: (notes: MedicineEffectNote[]) => MedicineEffectNote[]): void {
    if (this.dayNotes.hasValue() && !this.dayNotes.isLoading()) {
      this.dayNotes.set(change(this.dayNotes.value()));
    }
  }
}
