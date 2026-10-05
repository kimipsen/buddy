import { describe, expect, it } from 'vitest';

import { SleepEntry } from '../../../core/sleep-diary.service';
import {
  draftFromEntry,
  draftToRequest,
  emptyDraft,
  splitMinutes,
  suggestTotalSleepMinutes,
} from './sleep-entry-draft';

describe('sleep-entry-draft', () => {
  it('suggests fell-asleep to morning wake across midnight, minus the wake-ups', () => {
    const draft = {
      ...emptyDraft(),
      bedTime: '20:15',
      fellAsleepTime: '20:45',
      morningWakeTime: '06:30',
      nightWakeUps: [
        { startTime: '03:30', durationMinutes: 30 },
        { startTime: '05:00', durationMinutes: 10 },
      ],
      naps: [{ startTime: '13:00', durationMinutes: 60 }],
    };

    expect(suggestTotalSleepMinutes(draft)).toBe(9 * 60 + 45 - 40);
  });

  it('falls back to bedtime when the fall-asleep time is blank', () => {
    expect(
      suggestTotalSleepMinutes({ ...emptyDraft(), bedTime: '21:00', morningWakeTime: '07:00' }),
    ).toBe(600);
  });

  it('suggests nothing without both ends of the night, and never a negative total', () => {
    expect(suggestTotalSleepMinutes({ ...emptyDraft(), bedTime: '21:00' })).toBeNull();
    expect(suggestTotalSleepMinutes({ ...emptyDraft(), morningWakeTime: '07:00' })).toBeNull();
    expect(
      suggestTotalSleepMinutes({
        ...emptyDraft(),
        fellAsleepTime: '06:00',
        morningWakeTime: '06:30',
        nightWakeUps: [{ startTime: '06:10', durationMinutes: 60 }],
      }),
    ).toBe(0);
  });

  it('turns blanks into nulls, drops rows without a start time and trims remarks', () => {
    const request = draftToRequest(
      {
        ...emptyDraft(),
        bedTime: '20:00',
        nightWakeUps: [
          { startTime: '', durationMinutes: 15 },
          { startTime: '02:00', durationMinutes: 20 },
        ],
        isTired: true,
        remarks: '  Restless  ',
      },
      480,
    );

    expect(request).toEqual({
      routineStartTime: null,
      ritualStartTime: null,
      ritualEndTime: null,
      bedTime: '20:00',
      fellAsleepTime: null,
      nightWakeUps: [{ startTime: '02:00', durationMinutes: 20 }],
      morningWakeTime: null,
      isTired: true,
      naps: [],
      totalSleepMinutes: 480,
      remarks: 'Restless',
    });
  });

  it('round-trips a saved entry into a draft with blanks as empty strings', () => {
    const entry: SleepEntry = {
      date: '2026-03-02',
      isWeekend: false,
      loggedBy: 'g',
      routineStartTime: null,
      ritualStartTime: '19:30',
      ritualEndTime: '20:00',
      bedTime: '20:15',
      fellAsleepTime: null,
      nightWakeUps: [{ startTime: '03:00', durationMinutes: 10 }],
      morningWakeTime: '06:30',
      isTired: false,
      naps: [],
      totalSleepMinutes: 500,
      remarks: 'x',
    };

    const draft = draftFromEntry(entry);

    expect(draft.routineStartTime).toBe('');
    expect(draft.fellAsleepTime).toBe('');
    expect(draft.ritualStartTime).toBe('19:30');
    expect(draft.totalSleepMinutes).toBe(500);
    expect(draft.nightWakeUps).toEqual(entry.nightWakeUps);
    expect(draft.nightWakeUps).not.toBe(entry.nightWakeUps);
  });

  it('splits minutes into hours and minutes', () => {
    expect(splitMinutes(555)).toEqual({ hours: 9, minutes: 15 });
    expect(splitMinutes(45)).toEqual({ hours: 0, minutes: 45 });
  });
});
