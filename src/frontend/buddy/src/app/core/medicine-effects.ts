import { NonEmptyArray, firstAndLast } from './array-utils';
import type {
  EffectArea,
  EffectLevel,
  MedicineEffectNote,
  SideEffect,
  SideEffectSeverity,
} from './medicines.service';

// The catalog in the backend's enum order (MedicineEffectNote.cs), which is also the order the API
// stores and returns ratings in (docs/backend/analysis/medicine-effect-notes.md, Question 3).
export const EFFECT_AREAS: readonly EffectArea[] = ['Focus', 'Calm', 'ImpulseControl', 'Mood'];
export const EFFECT_LEVELS: readonly EffectLevel[] = ['Poor', 'Okay', 'Good'];
export const SIDE_EFFECTS: readonly SideEffect[] = [
  'ReducedAppetite',
  'TroubleFallingAsleep',
  'Stomachache',
  'Headache',
  'Irritability',
  'ReboundWhenWearingOff',
  'Tics',
  'FlatOrNotThemselves',
  'Other',
];
export const SIDE_EFFECT_SEVERITIES: readonly SideEffectSeverity[] = [
  'None',
  'Mild',
  'Moderate',
  'Severe',
];
// Shown in the form without "More side effects": the commonest stimulant side effects.
export const COMMON_SIDE_EFFECTS: readonly SideEffect[] = [
  'ReducedAppetite',
  'TroubleFallingAsleep',
  'Irritability',
  'ReboundWhenWearingOff',
];

export interface PeriodMedicine {
  name: string;
  dosage: string;
}

export interface LevelCount<T> {
  value: T;
  count: number;
}

export interface DosePeriod {
  // Empty on baseline days with no medicine.
  regimen: PeriodMedicine[];
  from: string;
  to: string;
  days: number;
  // Only rated areas and side effects, in catalog order, each with a count per level (zeros kept,
  // so every bar lists the scale in the same order).
  effects: { area: EffectArea; counts: LevelCount<EffectLevel>[] }[];
  sideEffects: { kind: SideEffect; counts: LevelCount<SideEffectSeverity>[] }[];
  // "HH:mm", or null when no day in the period had a wear-off time.
  medianWoreOffAt: string | null;
}

function regimenOf(note: MedicineEffectNote): PeriodMedicine[] {
  return note.regimen
    .map(({ name, dosage }) => ({ name, dosage }))
    .sort((a, b) => a.name.localeCompare(b.name) || a.dosage.localeCompare(b.dosage));
}

function regimenKey(regimen: PeriodMedicine[]): string {
  return JSON.stringify(regimen.map((m) => [m.name, m.dosage]));
}

function countLevels<T>(scale: readonly T[], values: T[]): LevelCount<T>[] {
  return scale.map((value) => ({ value, count: values.filter((v) => v === value).length }));
}

function toMinutes(time: string): number {
  const [hours = 0, minutes = 0] = time.split(':').map(Number);
  return hours * 60 + minutes;
}

function fromMinutes(total: number): string {
  const hours = Math.floor(total / 60);
  const minutes = total % 60;
  return `${String(hours).padStart(2, '0')}:${String(minutes).padStart(2, '0')}`;
}

export function medianTime(times: string[]): string | null {
  if (times.length === 0) {
    return null;
  }

  const sorted = times.map(toMinutes).sort((a, b) => a - b);
  const middle = Math.floor(sorted.length / 2);
  const upper = sorted[middle] ?? 0;
  const lower = sorted.length % 2 === 1 ? upper : (sorted[middle - 1] ?? upper);

  return fromMinutes(Math.round((lower + upper) / 2));
}

function summarize(
  regimen: PeriodMedicine[],
  notes: NonEmptyArray<MedicineEffectNote>,
): DosePeriod {
  const [first, last] = firstAndLast(notes);
  const effects = EFFECT_AREAS.flatMap((area) => {
    const levels = notes.flatMap((n) =>
      n.effects.filter((e) => e.area === area).map((e) => e.level),
    );
    return levels.length === 0 ? [] : [{ area, counts: countLevels(EFFECT_LEVELS, levels) }];
  });
  const sideEffects = SIDE_EFFECTS.flatMap((kind) => {
    const severities = notes.flatMap((n) =>
      n.sideEffects.filter((s) => s.kind === kind).map((s) => s.severity),
    );
    return severities.length === 0
      ? []
      : [{ kind, counts: countLevels(SIDE_EFFECT_SEVERITIES, severities) }];
  });

  return {
    regimen,
    from: first.date,
    to: last.date,
    days: notes.length,
    effects,
    sideEffects,
    medianWoreOffAt: medianTime(notes.flatMap((n) => (n.woreOffAt ? [n.woreOffAt] : []))),
  };
}

// Walks the notes in date order and starts a new period whenever the regimen (the set of name +
// dosage pairs) changes, so "10 mg, then 15 mg, then back to 10 mg" is three periods. Days
// without a note don't break a period. Oldest period first.
export function groupByDosePeriod(notes: readonly MedicineEffectNote[]): DosePeriod[] {
  const sorted = [...notes].sort((a, b) => a.date.localeCompare(b.date));
  const groups: { regimen: PeriodMedicine[]; notes: NonEmptyArray<MedicineEffectNote> }[] = [];

  for (const note of sorted) {
    const regimen = regimenOf(note);
    const current = groups.at(-1);

    if (current && regimenKey(current.regimen) === regimenKey(regimen)) {
      current.notes.push(note);
    } else {
      groups.push({ regimen, notes: [note] });
    }
  }

  return groups.map((group) => summarize(group.regimen, group.notes));
}
