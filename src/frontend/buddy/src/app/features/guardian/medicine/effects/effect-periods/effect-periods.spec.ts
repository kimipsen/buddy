import { TestBed } from '@angular/core/testing';
import { describe, expect, it } from 'vitest';

import {
  EffectLevel,
  MedicineEffectNote,
  SideEffectSeverity,
} from '../../../../../core/medicines.service';
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
    regimen:
      dosage === null
        ? []
        : [
            {
              medicineId: 'med-1',
              name: 'Ritalin',
              dosage,
              doses: [],
              isExcludedFromEffects: false,
            },
          ],
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

    expect(compiled.textContent).toContain('No days rated in the last 3 months yet.');
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

  it('shows the intro by default and leaves it out when asked', async () => {
    expect((await setup([])).textContent).toContain(
      'The last 3 months, grouped by the medicine and dosage your child was on.',
    );
    TestBed.resetTestingModule();

    await TestBed.configureTestingModule({ imports: [EffectPeriods] }).compileComponents();
    const fixture = TestBed.createComponent(EffectPeriods);
    fixture.componentRef.setInput('notes', []);
    fixture.componentRef.setInput('showIntro', false);
    fixture.detectChanges();

    expect((fixture.nativeElement as HTMLElement).textContent).not.toContain('The last 3 months');
  });

  it('writes a one-day period as one date and lists only the levels that occurred', async () => {
    const compiled = await setup([
      note('2026-09-29', '15 mg', {
        effects: [{ area: 'Focus', level: 'Good' }],
        sideEffects: [{ kind: 'ReducedAppetite', severity: 'Mild' }],
      }),
    ]);

    const section = compiled.querySelector('section')!;
    expect(section.textContent).toContain('Sep 29 · 1 days rated');
    expect(section.textContent).not.toContain('–');
    expect([...section.querySelectorAll('dt')].map((dt) => dt.textContent?.trim())).toEqual([
      'Focus',
      'Less appetite',
    ]);
    expect([...section.querySelectorAll('dd p')].map((p) => p.textContent?.trim())).toEqual([
      'Good 1',
      'Mild 1',
    ]);
  });

  it('draws each bar as coloured segments sized by their share of the days', async () => {
    const day = (date: string, level: EffectLevel, severity: SideEffectSeverity) =>
      note(date, '10 mg', {
        effects: [{ area: 'Focus', level }],
        sideEffects: [{ kind: 'ReducedAppetite', severity }],
      });
    const compiled = await setup([
      day('2026-09-01', 'Poor', 'None'),
      day('2026-09-02', 'Okay', 'Mild'),
      day('2026-09-03', 'Okay', 'Moderate'),
      day('2026-09-04', 'Good', 'Severe'),
    ]);

    const bars = [...compiled.querySelectorAll('dd > div[aria-hidden="true"]')].map((bar) =>
      [...bar.children].map((segment) => ({
        class: segment.getAttribute('class'),
        width: (segment as HTMLElement).style.width,
      })),
    );
    expect(bars).toEqual([
      [
        { class: 'bg-red-400 dark:bg-red-500', width: '25%' },
        { class: 'bg-amber-300 dark:bg-amber-500', width: '50%' },
        { class: 'bg-emerald-500 dark:bg-emerald-400', width: '25%' },
      ],
      [
        { class: 'bg-slate-300 dark:bg-slate-600', width: '25%' },
        { class: 'bg-amber-200 dark:bg-amber-700', width: '25%' },
        { class: 'bg-orange-400 dark:bg-orange-500', width: '25%' },
        { class: 'bg-red-500 dark:bg-red-400', width: '25%' },
      ],
    ]);
  });

  it('names the left-out medicines under the periods, and nothing when none are', async () => {
    const leftOut = (name: string, id: string) => ({
      medicineId: id,
      name,
      dosage: '1',
      doses: [],
      isExcludedFromEffects: true,
    });
    const ritalin = note('2026-03-02', '10 mg').regimen;

    const compiled = await setup([
      note('2026-03-02', '10 mg', {
        regimen: [...ritalin, leftOut('Vitamin D', 'v'), leftOut('Melatonin', 'm')],
      }),
    ]);

    expect(compiled.textContent).toContain('Not included: Melatonin, Vitamin D.');
    expect(compiled.querySelector('h4')?.textContent?.trim()).toBe('Ritalin 10 mg');
  });

  it('has no "Not included" line when every medicine is included', async () => {
    const compiled = await setup([note('2026-03-02', '10 mg')]);

    expect(compiled.textContent).not.toContain('Not included');
  });
});
