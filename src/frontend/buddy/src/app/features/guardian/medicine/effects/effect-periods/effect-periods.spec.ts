import { TestBed } from '@angular/core/testing';
import { describe, expect, it } from 'vitest';

import { MedicineEffectNote } from '../../../../../core/medicines.service';
import { EffectPeriods } from './effect-periods';

function note(
  date: string,
  dosage: string | null,
  overrides: Partial<MedicineEffectNote> = {},
): MedicineEffectNote {
  return {
    date,
    effects: [],
    sideEffects: [],
    woreOffAt: null,
    note: '',
    regimen: dosage === null ? [] : [{ medicineId: 'med-1', name: 'Ritalin', dosage, doses: [] }],
    loggedBy: 'guardian-1',
    ...overrides,
  };
}

describe('EffectPeriods', () => {
  async function setup(notes: MedicineEffectNote[]) {
    await TestBed.configureTestingModule({ imports: [EffectPeriods] }).compileComponents();
    const fixture = TestBed.createComponent(EffectPeriods);
    fixture.componentRef.setInput('notes', notes);
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  it('says so when nothing was rated', async () => {
    const compiled = await setup([]);

    expect(compiled.textContent).toContain('No days rated in the last 92 days yet.');
  });

  it('shows the newest dose period first with counts written out and the median wear-off', async () => {
    const compiled = await setup([
      note('2026-09-01', null, { effects: [{ area: 'Focus', level: 'Poor' }] }),
      note('2026-09-08', '10 mg', {
        effects: [{ area: 'Focus', level: 'Okay' }],
        sideEffects: [{ kind: 'ReducedAppetite', severity: 'Mild' }],
        woreOffAt: '14:30',
      }),
      note('2026-09-09', '10 mg', {
        effects: [{ area: 'Focus', level: 'Good' }],
        sideEffects: [{ kind: 'ReducedAppetite', severity: 'Moderate' }],
        woreOffAt: '14:30',
      }),
      note('2026-09-29', '15 mg', {
        effects: [{ area: 'Focus', level: 'Good' }],
        woreOffAt: '16:00',
      }),
    ]);

    const sections = Array.from(compiled.querySelectorAll('section'));
    expect(sections.map((s) => s.querySelector('h4')?.textContent?.trim())).toEqual([
      'Ritalin 15 mg',
      'Ritalin 10 mg',
      'No medicine',
    ]);
    expect(sections[1].textContent).toContain('Sep 8 – Sep 9');
    expect(sections[1].textContent).toContain('2 days rated');
    expect(sections[1].textContent).toContain('Usually wore off around 2:30 PM');
    expect(sections[1].textContent).toContain('Okay 1, Good 1');
    expect(sections[1].textContent).toContain('Less appetite');
    expect(sections[1].textContent).toContain('Mild 1, Moderate 1');
    expect(sections[0].textContent).toContain('Sep 29 ·');
    expect(sections[2].textContent).not.toContain('wore off');
  });
});
