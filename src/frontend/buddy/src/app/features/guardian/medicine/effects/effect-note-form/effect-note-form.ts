import {
  Component,
  computed,
  inject,
  input,
  linkedSignal,
  output,
  resource,
  signal,
} from '@angular/core';
import { FormsModule } from '@angular/forms';

import { TranslatePipe } from '../../../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../../../core/i18n/translation.service';
import {
  COMMON_SIDE_EFFECTS,
  EFFECT_AREAS,
  EFFECT_LEVELS,
  SIDE_EFFECTS,
  SIDE_EFFECT_SEVERITIES,
} from '../../../../../core/medicine-effects';
import {
  DoseStatus,
  EffectArea,
  EffectLevel,
  MedicineEffectNote,
  MedicineEffectNoteRequest,
  MedicinesService,
  SideEffect,
  SideEffectSeverity,
} from '../../../../../core/medicines.service';
import { TimeOfDayPipe } from '../../../../../core/time-of-day.pipe';
import { createAction } from '../../../../../shared/action-state/action-state';
import { Card } from '../../../../../shared/card/card';
import { DateSelect } from '../../../../../shared/date-select/date-select';
import {
  SegmentedControl,
  SegmentedControlOption,
} from '../../../../../shared/segmented-control/segmented-control';
import { TimeSelect } from '../../../../../shared/time-select/time-select';

type FormAction = 'save' | 'clear';

interface EffectDraft {
  effects: Partial<Record<EffectArea, EffectLevel>>;
  sideEffects: Partial<Record<SideEffect, SideEffectSeverity>>;
  woreOffAt: string;
  note: string;
}

export interface RegimenPreview {
  medicineId: string;
  name: string;
  dosage: string;
  doses: { time: string; status: DoseStatus }[];
}

function draftFromNote(note: MedicineEffectNote | null): EffectDraft {
  return {
    effects: Object.fromEntries((note?.effects ?? []).map((e) => [e.area, e.level])),
    sideEffects: Object.fromEntries((note?.sideEffects ?? []).map((s) => [s.kind, s.severity])),
    woreOffAt: note?.woreOffAt ?? '',
    note: note?.note ?? '',
  };
}

// Only what was rated travels: an unrated area is "not observed", never a default level.
function draftToRequest(draft: EffectDraft): MedicineEffectNoteRequest {
  return {
    effects: EFFECT_AREAS.flatMap((area) => {
      const level = draft.effects[area];
      return level ? [{ area, level }] : [];
    }),
    sideEffects: SIDE_EFFECTS.flatMap((kind) => {
      const severity = draft.sideEffects[kind];
      return severity ? [{ kind, severity }] : [];
    }),
    woreOffAt: draft.woreOffAt || null,
    note: draft.note.trim(),
  };
}

function isEmpty(request: MedicineEffectNoteRequest): boolean {
  return (
    !request.effects?.length && !request.sideEffects?.length && !request.woreOffAt && !request.note
  );
}

// One day's ratings (docs/backend/analysis/medicine-effect-notes.md, Question 3). Owns only the
// draft; the page owns which child and date are shown and the loaded notes.
@Component({
  selector: 'app-effect-note-form',
  imports: [
    FormsModule,
    Card,
    DateSelect,
    SegmentedControl,
    TimeOfDayPipe,
    TimeSelect,
    TranslatePipe,
  ],
  templateUrl: './effect-note-form.html',
})
export class EffectNoteForm {
  private readonly medicines = inject(MedicinesService);
  private readonly translation = inject(TranslationService);

  readonly childId = input.required<string>();
  readonly date = input.required<string>();
  // The latest day that can be rated: a future day is a 400 from the API.
  readonly maxDate = input('');
  // The note already saved for date, or null for a day not rated yet.
  readonly note = input<MedicineEffectNote | null>(null);
  // True while the page is still loading what's stored, so a save can't overwrite it unseen.
  readonly locked = input(false);

  readonly dateChange = output<string>();
  readonly saved = output<MedicineEffectNote>();
  readonly cleared = output<string>();

  private readonly shown = computed(() => ({
    childId: this.childId(),
    date: this.date(),
    note: this.note(),
  }));

  protected readonly draft = linkedSignal<{ note: MedicineEffectNote | null }, EffectDraft>({
    source: this.shown,
    computation: ({ note }) => draftFromNote(note),
  });
  // Opens by itself when a less common side effect is already rated, so it never hides a rating.
  protected readonly showAllSideEffects = linkedSignal<
    { note: MedicineEffectNote | null },
    boolean
  >({
    source: this.shown,
    computation: ({ note }) =>
      (note?.sideEffects ?? []).some((s) => !COMMON_SIDE_EFFECTS.includes(s.kind)),
  });

  // A rated day shows the regimen saved with it; an unrated one previews what the server will
  // resolve on save -- the medicines active that day, from the day's doses.
  private readonly dayDoses = resource({
    params: () => (this.note() ? undefined : { childId: this.childId(), date: this.date() }),
    loader: ({ params }) => this.medicines.listDoses(params.childId, params.date, params.date),
  });
  protected readonly regimen = computed((): RegimenPreview[] | null => {
    const note = this.note();
    if (note) {
      return note.regimen;
    }
    if (!this.dayDoses.hasValue()) {
      return null;
    }

    const byMedicine = new Map<string, RegimenPreview>();
    for (const dose of this.dayDoses.value()) {
      const medicine = byMedicine.get(dose.medicineId) ?? {
        medicineId: dose.medicineId,
        name: dose.name,
        dosage: dose.dosage,
        doses: [],
      };
      medicine.doses.push({ time: dose.time.slice(0, 5), status: dose.status });
      byMedicine.set(dose.medicineId, medicine);
    }
    return [...byMedicine.values()];
  });

  protected readonly areas = EFFECT_AREAS;
  protected readonly shownSideEffects = computed(() =>
    this.showAllSideEffects() ? SIDE_EFFECTS : COMMON_SIDE_EFFECTS,
  );

  protected readonly levelOptions = computed<SegmentedControlOption<EffectLevel | null>[]>(() => [
    { value: null, label: this.translation.translate('medicine.effects.notRated') },
    ...EFFECT_LEVELS.map((level) => ({
      value: level,
      label: this.translation.translate(`medicine.effects.levels.${level}`),
    })),
  ]);
  protected readonly severityOptions = computed<
    SegmentedControlOption<SideEffectSeverity | null>[]
  >(() => [
    { value: null, label: this.translation.translate('medicine.effects.notRated') },
    ...SIDE_EFFECT_SEVERITIES.map((severity) => ({
      value: severity,
      label: this.translation.translate(`medicine.effects.severities.${severity}`),
    })),
  ]);

  protected readonly action = createAction<FormAction>();
  // `${childId}|${date}` of the last successful save, until the next edit.
  private readonly savedKey = signal<string | null>(null);
  protected readonly justSaved = computed(
    () => this.savedKey() === `${this.childId()}|${this.date()}`,
  );

  protected setEffect(area: EffectArea, level: EffectLevel | null): void {
    this.patch({ effects: { ...this.draft().effects, [area]: level ?? undefined } });
  }

  protected setSideEffect(kind: SideEffect, severity: SideEffectSeverity | null): void {
    this.patch({ sideEffects: { ...this.draft().sideEffects, [kind]: severity ?? undefined } });
  }

  protected setWoreOffAt(woreOffAt: string): void {
    this.patch({ woreOffAt });
  }

  protected setNote(note: string): void {
    this.patch({ note });
  }

  protected toggleSideEffects(): void {
    this.showAllSideEffects.update((all) => !all);
  }

  protected async save(): Promise<void> {
    const childId = this.childId();
    const date = this.date();
    const request = draftToRequest(this.draft());

    if (isEmpty(request)) {
      this.action.fail('save', 'medicine.effects.form.nothingToSave');
      return;
    }

    await this.action.run(
      'save',
      async () => {
        const note = await this.medicines.logEffectNote(childId, date, request);
        this.saved.emit(note);
        this.savedKey.set(`${childId}|${date}`);
      },
      'medicine.effects.form.saveError',
    );
  }

  protected async clear(): Promise<void> {
    const childId = this.childId();
    const date = this.date();

    await this.action.run(
      'clear',
      async () => {
        await this.medicines.clearEffectNote(childId, date);
        this.cleared.emit(date);
      },
      'medicine.effects.form.clearError',
    );
  }

  private patch(change: Partial<EffectDraft>): void {
    this.draft.update((draft) => ({ ...draft, ...change }));
    this.savedKey.set(null);
    this.action.clearError();
  }
}
