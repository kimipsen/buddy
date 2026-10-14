import { TestBed } from '@angular/core/testing';
import { describe, expect, it, vi } from 'vitest';

import { MedicineEffectNote } from '../../../../../core/medicines.service';
import { EffectDays } from './effect-days';

function note(date: string, overrides: Partial<MedicineEffectNote> = {}): MedicineEffectNote {
  return {
    date,
    effects: [{ area: 'Focus', level: 'Good' }],
    sideEffects: [{ kind: 'ReducedAppetite', severity: 'Moderate' }],
    woreOffAt: '15:30',
    note: 'Skipped lunch',
    regimen: [
      {
        medicineId: 'med-1',
        name: 'Ritalin',
        dosage: '10 mg',
        doses: [
          { time: '07:30', status: 'Taken' },
          { time: '12:00', status: 'Skipped' },
        ],
      },
    ],
    loggedBy: 'guardian-1',
    ...overrides,
  };
}

describe('EffectDays', () => {
  async function setup(notes: Partial<Record<string, MedicineEffectNote>>) {
    await TestBed.configureTestingModule({ imports: [EffectDays] }).compileComponents();
    const fixture = TestBed.createComponent(EffectDays);
    fixture.componentRef.setInput('days', ['2026-03-01', '2026-03-02', '2026-03-03']);
    fixture.componentRef.setInput('notes', notes);
    fixture.componentRef.setInput('selectedDate', '2026-03-02');
    const selectDate = vi.fn();
    fixture.componentInstance.selectDate.subscribe(selectDate);
    fixture.detectChanges();
    return { fixture, compiled: fixture.nativeElement as HTMLElement, selectDate };
  }

  it('lists every day newest first, with rated items as chips and blanks for unrated days', async () => {
    const { compiled } = await setup({
      '2026-03-02': note('2026-03-02'),
      '2026-03-01': note('2026-03-01', { regimen: [], sideEffects: [], woreOffAt: null, note: '' }),
    });

    const rows = Array.from(compiled.querySelectorAll('li'));
    expect(rows.map((r) => r.querySelector('h4')?.textContent?.trim())).toEqual([
      'Tue, Mar 3',
      'Mon, Mar 2',
      'Sun, Mar 1',
    ]);
    expect(rows[0].textContent).toContain('Not rated');
    expect(rows[1].textContent).toContain('Ritalin 10 mg');
    expect(rows[1].textContent).toContain('7:30 AM taken');
    expect(rows[1].textContent).toContain('12:00 PM skipped');
    expect(rows[1].textContent).toContain('Focus: Good');
    expect(rows[1].textContent).toContain('Less appetite: Moderate');
    expect(rows[1].textContent).toContain('Wore off 3:30 PM');
    expect(rows[1].textContent).toContain('Skipped lunch');
    expect(rows[1].getAttribute('aria-current')).toBe('date');
    expect(rows[2].textContent).toContain('No medicine');
    expect(rows[2].textContent).not.toContain('Wore off');
  });

  it('hands the chosen day to the form', async () => {
    const { compiled, selectDate } = await setup({});

    compiled.querySelector<HTMLButtonElement>('button[aria-label="Open Sun, Mar 1"]')!.click();

    expect(selectDate).toHaveBeenCalledWith('2026-03-01');
  });
});
