import { describe, expect, it } from 'vitest';

import type { FeelingCheckIn } from './feelings.service';
import type {
  MedicineDoseOccurrence,
  MedicineEffectNote,
  MedicineSchedule,
} from './medicines.service';
import type { SleepEntry } from './sleep-diary.service';
import type { VisitSummaryMedicineSection } from './visit-summaries.service';
import {
  dayPartOf,
  feelingsStats,
  medicineAdherence,
  overviewWeeks,
  periodDays,
  sleepStats,
} from './visit-summary-stats';

function night(date: string, overrides: Partial<SleepEntry> = {}): SleepEntry {
  return {
    date,
    isWeekend: false,
    routineStartTime: null,
    ritualStartTime: null,
    ritualEndTime: null,
    bedTime: '20:00:00',
    fellAsleepTime: '20:30:00',
    nightWakeUps: [],
    morningWakeTime: '07:00:00',
    isTired: false,
    naps: [],
    totalSleepMinutes: 600,
    remarks: '',
    loggedBy: 'guardian-1',
    ...overrides,
  };
}

function schedule(id: string, name = 'Concerta', dosage = '18 mg'): MedicineSchedule {
  return {
    id,
    childId: 'child-1',
    name,
    dosage,
    icon: '💊',
    color: '#3b82f6',
    times: ['07:15:00'],
    startDate: '2026-03-01',
    endDate: null,
    isStopped: false,
    createdBy: 'guardian-1',
    lastModifiedBy: 'guardian-1',
  };
}

function dose(
  medicineId: string,
  date: string,
  status: MedicineDoseOccurrence['status'],
): MedicineDoseOccurrence {
  return {
    medicineId,
    name: 'Concerta',
    dosage: '18 mg',
    icon: '💊',
    color: '#3b82f6',
    date,
    time: '07:15:00',
    status,
  };
}

function note(date: string, dosage: string): MedicineEffectNote {
  return {
    date,
    effects: [],
    sideEffects: [],
    woreOffAt: null,
    note: '',
    regimen: [{ medicineId: 'm1', name: 'Concerta', dosage, doses: [] }],
    loggedBy: 'guardian-1',
  };
}

function checkIn(
  date: string,
  time: string,
  overrides: Partial<FeelingCheckIn> = {},
): FeelingCheckIn {
  return {
    id: `${date}-${time}`,
    zone: 'Green',
    tags: [],
    note: '',
    date,
    time,
    loggedBy: 'guardian-1',
    isSelfReport: false,
    recordedAt: `${date}T${time}:00Z`,
    ...overrides,
  };
}

describe('visit summary stats', () => {
  it('averages sleep overall and for weekdays and weekends, wrapping a sleep onset past midnight', () => {
    const stats = sleepStats([
      night('2026-03-02', { totalSleepMinutes: 540, isTired: true }),
      night('2026-03-03', {
        totalSleepMinutes: null,
        bedTime: '23:50:00',
        fellAsleepTime: '00:20:00',
      }),
      night('2026-03-07', {
        isWeekend: true,
        totalSleepMinutes: 660,
        fellAsleepTime: null,
        nightWakeUps: [
          { startTime: '02:00:00', durationMinutes: 10 },
          { startTime: '04:00:00', durationMinutes: 5 },
        ],
      }),
    ]);

    expect(stats).toEqual({
      nights: 3,
      averageMinutes: 600,
      weekdayAverageMinutes: 540,
      weekendAverageMinutes: 660,
      averageMinutesToFallAsleep: 30,
      nightWakeUps: 2,
      tiredMornings: 1,
    });
  });

  it('has no averages without nights', () => {
    expect(sleepStats([]).averageMinutes).toBeNull();
    expect(sleepStats([]).averageMinutesToFallAsleep).toBeNull();
  });

  it('counts taken, skipped and unmarked past doses per medicine, ignoring future ones', () => {
    const section: VisitSummaryMedicineSection = {
      schedules: [schedule('m1'), schedule('m2', 'Melatonin', '3 mg')],
      doses: [
        dose('m1', '2026-03-02', 'Taken'),
        dose('m1', '2026-03-03', 'Skipped'),
        dose('m1', '2026-03-04', 'Pending'),
        dose('m1', '2026-03-05', 'Pending'),
        dose('m2', '2026-03-02', 'Taken'),
      ],
      effectNotes: [],
    };

    const adherence = medicineAdherence(section, '2026-03-04');

    expect(adherence.map((a) => [a.schedule.id, a.taken, a.skipped, a.notMarked])).toEqual([
      ['m1', 1, 1, 1],
      ['m2', 1, 0, 0],
    ]);
  });

  it('splits the day at noon and five o’clock', () => {
    expect([
      dayPartOf('11:59'),
      dayPartOf('12:00'),
      dayPartOf('16:59'),
      dayPartOf('17:00'),
    ]).toEqual(['morning', 'afternoon', 'afternoon', 'evening']);
  });

  it('counts zones per part of the day, ranks words and keeps only adults’ notes', () => {
    const stats = feelingsStats(
      [
        checkIn('2026-03-02', '07:40', { tags: ['Calm'] }),
        checkIn('2026-03-02', '16:10', {
          zone: 'Red',
          tags: ['Overwhelmed', 'School'],
          note: 'Bus',
        }),
        checkIn('2026-03-03', '16:00', {
          zone: 'Yellow',
          tags: ['School'],
          isSelfReport: true,
          note: '',
        }),
      ],
      2,
    );

    expect(stats.total).toBe(3);
    expect(stats.selfReports).toBe(1);
    expect(stats.byDayPart[1]).toEqual({
      part: 'afternoon',
      total: 2,
      zones: [
        { zone: 'Green', count: 0 },
        { zone: 'Blue', count: 0 },
        { zone: 'Yellow', count: 1 },
        { zone: 'Red', count: 1 },
      ],
    });
    expect(stats.topTags).toEqual([
      { tag: 'School', count: 2 },
      { tag: 'Calm', count: 1 },
    ]);
    expect(stats.adultNotes).toEqual([{ date: '2026-03-02', time: '16:10', note: 'Bus' }]);
  });

  it('lays the period out as Monday-first weeks with padding days outside it', () => {
    const medicine: VisitSummaryMedicineSection = {
      schedules: [schedule('m1')],
      doses: [dose('m1', '2026-03-04', 'Taken'), dose('m1', '2026-03-05', 'Pending')],
      effectNotes: [note('2026-03-04', '18 mg'), note('2026-03-05', '27 mg')],
    };

    const weeks = overviewWeeks(
      '2026-03-04',
      '2026-03-10',
      [night('2026-03-04', { totalSleepMinutes: 545 })],
      medicine,
      [checkIn('2026-03-04', '16:00', { zone: 'Red' }), checkIn('2026-03-04', '08:00')],
      '2026-03-04',
    );

    expect(weeks).toHaveLength(2);
    expect(weeks[0][0]).toMatchObject({ date: '2026-03-02', inPeriod: false, doses: null });
    expect(weeks[0][2]).toMatchObject({
      date: '2026-03-04',
      inPeriod: true,
      sleepMinutes: 545,
      doses: { taken: 1, due: 1 },
      zones: ['Green', 'Red'],
      doseChange: false,
    });
    // Tomorrow's dose isn't due yet; the regimen changed from the day before.
    expect(weeks[0][3]).toMatchObject({ doses: null, doseChange: true });
    expect(weeks[1][1]).toMatchObject({ date: '2026-03-10', inPeriod: true });
    expect(weeks[1][2]).toMatchObject({ inPeriod: false });
  });

  it('counts the days of a period inclusively', () => {
    expect(periodDays('2026-03-02', '2026-03-29')).toBe(28);
  });
});
