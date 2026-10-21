import { TestBed } from '@angular/core/testing';
import { describe, expect, it } from 'vitest';

import type { VisitSummaryReport as Report } from '../../core/visit-summaries.service';
import { VisitSummaryReport } from './visit-summary-report';

function report(overrides: Partial<Report> = {}): Report {
  return {
    childGivenName: 'Emil',
    childFamilyName: 'Holm',
    title: 'Follow-up with Dr. Lund',
    visitDate: '2026-03-12',
    from: '2026-03-02',
    to: '2026-03-08',
    note: 'Afternoons are harder.',
    preparedBy: 'Sara Holm',
    sleep: {
      sleepHygieneNotes: 'No screens after 19:00',
      entries: [
        {
          date: '2026-03-02',
          isWeekend: false,
          routineStartTime: null,
          ritualStartTime: null,
          ritualEndTime: null,
          bedTime: '20:00:00',
          fellAsleepTime: '20:40:00',
          nightWakeUps: [{ startTime: '03:00:00', durationMinutes: 10 }],
          morningWakeTime: '06:30:00',
          isTired: true,
          naps: [],
          totalSleepMinutes: 545,
          remarks: '',
          loggedBy: 'guardian-1',
        },
      ],
    },
    medicine: {
      schedules: [
        {
          id: 'm1',
          childId: 'child-1',
          name: 'Concerta',
          dosage: '18 mg',
          icon: '💊',
          color: '#3b82f6',
          times: ['07:15:00'],
          startDate: '2026-02-01',
          endDate: null,
          isStopped: true,
          isExcludedFromEffects: false,
          createdBy: 'guardian-1',
          lastModifiedBy: 'guardian-1',
        },
      ],
      doses: [
        {
          medicineId: 'm1',
          name: 'Concerta',
          dosage: '18 mg',
          icon: '💊',
          color: '#3b82f6',
          date: '2026-03-02',
          time: '07:15:00',
          status: 'Taken',
        },
        {
          medicineId: 'm1',
          name: 'Concerta',
          dosage: '18 mg',
          icon: '💊',
          color: '#3b82f6',
          date: '2026-03-03',
          time: '07:15:00',
          status: 'Skipped',
        },
      ],
      effectNotes: [
        {
          date: '2026-03-02',
          effects: [{ area: 'Focus', level: 'Good' }],
          sideEffects: [],
          woreOffAt: null,
          note: '',
          regimen: [
            {
              medicineId: 'm1',
              name: 'Concerta',
              dosage: '18 mg',
              doses: [],
              isExcludedFromEffects: false,
            },
          ],
          loggedBy: 'guardian-1',
        },
      ],
      excludedMedicineCount: 0,
    },
    feelings: {
      from: '2026-03-02',
      to: '2026-03-08',
      promptTimes: [],
      displayMode: 'FacesAndWords',
      checkIns: [
        {
          id: 'c1',
          zone: 'Red',
          tags: ['Overwhelmed'],
          note: 'Meltdown after the bus',
          date: '2026-03-02',
          time: '16:10:00',
          loggedBy: 'guardian-1',
          isSelfReport: false,
          recordedAt: '2026-03-02T16:10:00Z',
        },
      ],
    },
    ...overrides,
  };
}

describe('VisitSummaryReport', () => {
  async function setup(value: Report) {
    await TestBed.configureTestingModule({ imports: [VisitSummaryReport] }).compileComponents();
    const fixture = TestBed.createComponent(VisitSummaryReport);
    fixture.componentRef.setInput('report', value);
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  it('shows the header, the guardian’s note and every chosen section', async () => {
    const compiled = await setup(report());
    const text = compiled.textContent ?? '';

    expect(compiled.querySelector('h1')?.textContent).toContain(
      'Emil Holm: Follow-up with Dr. Lund',
    );
    expect(text).toContain('7 days');
    expect(text).toContain('Sara Holm');
    expect(text).toContain('Afternoons are harder.');
    expect([...compiled.querySelectorAll('h2')].map((h) => h.textContent?.trim())).toEqual([
      'Questions and notes from home',
      'Overview',
      'Medicine',
      'Sleep',
      'Feelings',
    ]);
  });

  it('writes the medicine counts out and marks a stopped medicine', async () => {
    const compiled = await setup(report());
    // The first table is the overview.
    const medicineTable = [...compiled.querySelectorAll('table')][1];

    expect(medicineTable.textContent).toContain('Concerta');
    expect(medicineTable.textContent).toContain('Stopped');
    expect(medicineTable.textContent).toContain('1 of 2');
    expect(medicineTable.textContent).toContain('07:15');
    expect(compiled.textContent).toContain('By dose period');
    expect(compiled.textContent).not.toContain('The last 3 months');
  });

  it('says how many medicines the guardian left out, without naming them', async () => {
    const base = report();
    const withCount = (count: number) =>
      report({ medicine: { ...base.medicine!, excludedMedicineCount: count } });

    expect((await setup(base)).textContent).not.toContain('out of this summary');
    TestBed.resetTestingModule();
    expect((await setup(withCount(1))).textContent).toContain(
      'The guardian left 1 medicine out of this summary.',
    );
    TestBed.resetTestingModule();
    expect((await setup(withCount(2))).textContent).toContain(
      'The guardian left 2 medicines out of this summary.',
    );
  });

  it('summarises sleep and feelings', async () => {
    const compiled = await setup(report());
    const text = compiled.textContent ?? '';

    expect(text).toContain('1 of 7 nights logged');
    expect(text).toContain('9 h 05 min');
    expect(text).toContain('40 min');
    expect(text).toContain('No screens after 19:00');
    expect(text).toContain('1 check-ins · 0 by the child, 1 by an adult');
    expect(text).toContain('Overwhelmed');
    // Each part of the day writes its zone counts out, not only as coloured widths.
    expect(text).toContain('Too much 1');
    expect(text).toContain('Meltdown after the bus');
  });

  it('leaves out sections that are null and the note when empty', async () => {
    const compiled = await setup(report({ note: '', medicine: null, feelings: null }));

    expect([...compiled.querySelectorAll('h2')].map((h) => h.textContent?.trim())).toEqual([
      'Overview',
      'Sleep',
    ]);
  });

  it('says so when a chosen section has no data', async () => {
    const compiled = await setup(
      report({
        sleep: { sleepHygieneNotes: '', entries: [] },
        medicine: { schedules: [], doses: [], effectNotes: [], excludedMedicineCount: 0 },
        feelings: {
          from: '2026-03-02',
          to: '2026-03-08',
          promptTimes: [],
          displayMode: 'FacesAndWords',
          checkIns: [],
        },
      }),
    );
    const text = compiled.textContent ?? '';

    expect(text).toContain('No medicine in this period.');
    expect(text).toContain('No nights logged in this period.');
    expect(text).toContain('No check-ins in this period.');
  });
});
