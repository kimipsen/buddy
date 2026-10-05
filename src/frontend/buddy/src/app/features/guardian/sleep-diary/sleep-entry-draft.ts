import { SleepEntry, SleepEntryRequest, SleepInterval } from '../../../core/sleep-diary.service';

// The day-entry form's editable state. Times are "HH:mm" or '' for blank (what app-time-select
// binds); totalSleepMinutes is null until the guardian types one, so the form can show the
// suggestion instead.
export interface SleepEntryDraft {
  routineStartTime: string;
  ritualStartTime: string;
  ritualEndTime: string;
  bedTime: string;
  fellAsleepTime: string;
  nightWakeUps: SleepInterval[];
  morningWakeTime: string;
  isTired: boolean;
  naps: SleepInterval[];
  totalSleepMinutes: number | null;
  remarks: string;
}

export type SleepTimeField =
  | 'routineStartTime'
  | 'ritualStartTime'
  | 'ritualEndTime'
  | 'bedTime'
  | 'fellAsleepTime'
  | 'morningWakeTime';

export type SleepIntervalField = 'nightWakeUps' | 'naps';

export const DEFAULT_INTERVAL_MINUTES = 15;
const MINUTES_PER_DAY = 24 * 60;

export function emptyDraft(): SleepEntryDraft {
  return {
    routineStartTime: '',
    ritualStartTime: '',
    ritualEndTime: '',
    bedTime: '',
    fellAsleepTime: '',
    nightWakeUps: [],
    morningWakeTime: '',
    isTired: false,
    naps: [],
    totalSleepMinutes: null,
    remarks: '',
  };
}

export function draftFromEntry(entry: SleepEntry): SleepEntryDraft {
  return {
    routineStartTime: entry.routineStartTime ?? '',
    ritualStartTime: entry.ritualStartTime ?? '',
    ritualEndTime: entry.ritualEndTime ?? '',
    bedTime: entry.bedTime ?? '',
    fellAsleepTime: entry.fellAsleepTime ?? '',
    nightWakeUps: entry.nightWakeUps.map((interval) => ({ ...interval })),
    morningWakeTime: entry.morningWakeTime ?? '',
    isTired: entry.isTired,
    naps: entry.naps.map((interval) => ({ ...interval })),
    totalSleepMinutes: entry.totalSleepMinutes,
    remarks: entry.remarks,
  };
}

function orNull(time: string): string | null {
  return time === '' ? null : time;
}

// Rows without a start time are dropped rather than sent: the API requires one per interval.
function completeIntervals(intervals: SleepInterval[]): SleepInterval[] {
  return intervals.filter((interval) => interval.startTime !== '');
}

export function draftToRequest(
  draft: SleepEntryDraft,
  totalSleepMinutes: number | null,
): SleepEntryRequest {
  return {
    routineStartTime: orNull(draft.routineStartTime),
    ritualStartTime: orNull(draft.ritualStartTime),
    ritualEndTime: orNull(draft.ritualEndTime),
    bedTime: orNull(draft.bedTime),
    fellAsleepTime: orNull(draft.fellAsleepTime),
    nightWakeUps: completeIntervals(draft.nightWakeUps),
    morningWakeTime: orNull(draft.morningWakeTime),
    isTired: draft.isTired,
    naps: completeIntervals(draft.naps),
    totalSleepMinutes,
    remarks: draft.remarks.trim(),
  };
}

function toMinutes(time: string): number {
  const [hours = 0, minutes = 0] = time.split(':').map(Number);
  return hours * 60 + minutes;
}

// A starting point for "total time slept", never a rule: falling asleep (or, failing that, lying
// down) to the morning wake-up, wrapping past midnight, minus the night's wake-ups. Null when
// either end is missing. Naps are daytime and aren't counted.
export function suggestTotalSleepMinutes(draft: SleepEntryDraft): number | null {
  const start = draft.fellAsleepTime || draft.bedTime;

  if (!start || !draft.morningWakeTime) {
    return null;
  }

  const night =
    (toMinutes(draft.morningWakeTime) - toMinutes(start) + MINUTES_PER_DAY) % MINUTES_PER_DAY;
  const awake = draft.nightWakeUps.reduce((sum, interval) => sum + interval.durationMinutes, 0);

  return Math.max(0, night - awake);
}

export function splitMinutes(total: number): { hours: number; minutes: number } {
  return { hours: Math.floor(total / 60), minutes: total % 60 };
}
