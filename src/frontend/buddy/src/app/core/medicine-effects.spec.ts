import { describe, expect, it } from 'vitest';

import { groupByDosePeriod, medianTime } from './medicine-effects';
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

describe('medianTime', () => {
  it('is null for no times', () => {
    expect(medianTime([])).toBeNull();
  });

  it('takes the middle of an odd count and the mean of the middle two of an even count', () => {
    expect(medianTime(['16:00', '09:05', '14:30'])).toBe('14:30');
    expect(medianTime(['14:00', '15:01'])).toBe('14:31');
  });
});
