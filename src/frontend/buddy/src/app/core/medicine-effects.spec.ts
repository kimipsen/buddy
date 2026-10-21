import { describe, expect, it } from 'vitest';

import {
  COMMON_SIDE_EFFECTS,
  SIDE_EFFECTS,
  groupByDosePeriod,
  medianTime,
  regimenMedicines,
} from './medicine-effects';
import type { MedicineEffectNote } from './medicines.service';

function note(
  date: string,
  regimen: [string, string][],
  overrides: Partial<MedicineEffectNote> = {},
): MedicineEffectNote {
  return {
    date,
    effects: [],
    sideEffects: [],
    woreOffAt: null,
    note: '',
    regimen: regimen.map(([name, dosage], i) => ({
      medicineId: `med-${i}`,
      name,
      dosage,
      doses: [],
      isExcludedFromEffects: false,
    })),
    loggedBy: 'guardian-1',
    ...overrides,
  };
}

describe('groupByDosePeriod', () => {
  it('returns no periods for no notes', () => {
    expect(groupByDosePeriod([])).toEqual([]);
  });

  it('keeps baseline days as their own period with an empty regimen', () => {
    const periods = groupByDosePeriod([note('2026-09-01', []), note('2026-09-03', [])]);

    expect(periods).toHaveLength(1);
    expect(periods[0]).toMatchObject({
      regimen: [],
      from: '2026-09-01',
      to: '2026-09-03',
      days: 2,
    });
  });

  it('starts a new period when the dosage changes, sorting the notes by date first', () => {
    const periods = groupByDosePeriod([
      note('2026-09-10', [['Ritalin', '15 mg']]),
      note('2026-09-01', []),
      note('2026-09-05', [['Ritalin', '10 mg']]),
      note('2026-09-06', [['Ritalin', '10 mg']]),
    ]);

    expect(periods.map((p) => [p.regimen, p.from, p.to, p.days])).toEqual([
      [[], '2026-09-01', '2026-09-01', 1],
      [[{ name: 'Ritalin', dosage: '10 mg' }], '2026-09-05', '2026-09-06', 2],
      [[{ name: 'Ritalin', dosage: '15 mg' }], '2026-09-10', '2026-09-10', 1],
    ]);
  });

  it('makes a change and a change back three periods', () => {
    const periods = groupByDosePeriod([
      note('2026-09-01', [['Ritalin', '10 mg']]),
      note('2026-09-02', [['Ritalin', '15 mg']]),
      note('2026-09-03', [['Ritalin', '10 mg']]),
    ]);

    expect(periods.map((p) => p.regimen[0].dosage)).toEqual(['10 mg', '15 mg', '10 mg']);
  });

  it('treats the same medicines in another order as the same regimen', () => {
    const periods = groupByDosePeriod([
      note('2026-09-01', [
        ['Ritalin', '10 mg'],
        ['Melatonin', '3 mg'],
      ]),
      note('2026-09-02', [
        ['Melatonin', '3 mg'],
        ['Ritalin', '10 mg'],
      ]),
    ]);

    expect(periods).toHaveLength(1);
    expect(periods[0].regimen.map((m) => m.name)).toEqual(['Melatonin', 'Ritalin']);
  });

  it('counts each rated area and side effect per level, leaving unrated ones out', () => {
    const [period] = groupByDosePeriod([
      note('2026-09-01', [['Ritalin', '10 mg']], {
        effects: [{ area: 'Focus', level: 'Good' }],
        sideEffects: [{ kind: 'ReducedAppetite', severity: 'Moderate' }],
        woreOffAt: '14:00',
      }),
      note('2026-09-02', [['Ritalin', '10 mg']], {
        effects: [
          { area: 'Focus', level: 'Good' },
          { area: 'Mood', level: 'Poor' },
        ],
        sideEffects: [{ kind: 'ReducedAppetite', severity: 'None' }],
        woreOffAt: '15:00',
      }),
      note('2026-09-03', [['Ritalin', '10 mg']], { note: 'Only a note' }),
    ]);

    expect(period.effects).toEqual([
      {
        area: 'Focus',
        counts: [
          { value: 'Poor', count: 0 },
          { value: 'Okay', count: 0 },
          { value: 'Good', count: 2 },
        ],
      },
      {
        area: 'Mood',
        counts: [
          { value: 'Poor', count: 1 },
          { value: 'Okay', count: 0 },
          { value: 'Good', count: 0 },
        ],
      },
    ]);
    expect(period.sideEffects).toEqual([
      {
        kind: 'ReducedAppetite',
        counts: [
          { value: 'None', count: 1 },
          { value: 'Mild', count: 0 },
          { value: 'Moderate', count: 1 },
          { value: 'Severe', count: 0 },
        ],
      },
    ]);
    expect(period.medianWoreOffAt).toBe('14:30');
    expect(period.days).toBe(3);
  });
});

describe('left-out medicines', () => {
  function withRegimen(
    date: string,
    regimen: { id: string; name: string; dosage: string; excluded?: boolean }[],
  ): MedicineEffectNote {
    return note(date, [], {
      regimen: regimen.map(({ id, name, dosage, excluded = false }) => ({
        medicineId: id,
        name,
        dosage,
        doses: [],
        isExcludedFromEffects: excluded,
      })),
    });
  }

  it('merges periods that differ only by a left-out medicine and leaves it out of the regimen', () => {
    const ritalin = { id: 'r', name: 'Ritalin', dosage: '10 mg' };
    const melatonin = { id: 'm', name: 'Melatonin', dosage: '3 mg', excluded: true };

    const periods = groupByDosePeriod([
      withRegimen('2026-03-02', [ritalin]),
      withRegimen('2026-03-03', [ritalin, melatonin]),
      withRegimen('2026-03-04', [ritalin, { ...melatonin, dosage: '5 mg' }]),
    ]);

    expect(periods).toHaveLength(1);
    expect(periods[0]?.regimen).toEqual([{ name: 'Ritalin', dosage: '10 mg' }]);
    expect(periods[0]?.days).toBe(3);
  });

  it('makes a day with only left-out medicines a baseline day', () => {
    const periods = groupByDosePeriod([
      withRegimen('2026-03-02', [{ id: 'v', name: 'Vitamin D', dosage: '1', excluded: true }]),
      withRegimen('2026-03-03', []),
    ]);

    expect(periods).toHaveLength(1);
    expect(periods[0]?.regimen).toEqual([]);
  });

  it('lists every medicine once by its newest name, sorted by name', () => {
    expect(
      regimenMedicines([
        withRegimen('2026-03-03', [{ id: 'v', name: 'Vitamin D3', dosage: '1', excluded: true }]),
        withRegimen('2026-03-02', [
          { id: 'v', name: 'Vitamin D', dosage: '1' },
          { id: 'r', name: 'Ritalin', dosage: '10 mg' },
        ]),
      ]),
    ).toEqual([
      { medicineId: 'r', name: 'Ritalin', isExcludedFromEffects: false },
      { medicineId: 'v', name: 'Vitamin D3', isExcludedFromEffects: true },
    ]);
  });
});

describe('medianTime', () => {
  it('is null for no times', () => {
    expect(medianTime([])).toBeNull();
  });

  it('takes the middle of an odd count and the mean of the middle two of an even count', () => {
    expect(medianTime(['16:00', '09:05', '14:30'])).toBe('14:30');
    expect(medianTime(['14:00', '15:01'])).toBe('14:31');
  });

  it('pads hours and minutes to two digits', () => {
    expect(medianTime(['08:05'])).toBe('08:05');
  });
});

describe('COMMON_SIDE_EFFECTS', () => {
  it('lists the commonest stimulant side effects, all known side effects', () => {
    expect(COMMON_SIDE_EFFECTS).toEqual([
      'ReducedAppetite',
      'TroubleFallingAsleep',
      'Irritability',
      'ReboundWhenWearingOff',
    ]);
    expect(COMMON_SIDE_EFFECTS.every((kind) => SIDE_EFFECTS.includes(kind))).toBe(true);
  });
});
